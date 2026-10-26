using System;
using System.Collections.Generic;
using dc;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Utils;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts
{
    /// <summary>
    /// 服务端 Ghost 动作管理
    /// </summary>
    internal sealed class ServerGhostManager
    {
        private const double MaxCastDistanceSq = 48.0 * 48.0;
        private const long MinSkillIntervalMs = 50;
        private const long MinWeaponIntervalMs = 25;
        private const long MinTalismanIntervalMs = 50;

        /// <summary>
        /// 客户端上报三系等级的上限
        /// </summary>
        private const int MaxTier = 255;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        private sealed class ValidatedAction
        {
            public GhostHero Ghost { get; }
            public HeroAction Request { get; }
            public string LevelId { get; }
            public int SubLevelIndex { get; }
            public long ServerTimeMs { get; }
            public double Ratio { get; }

            public ValidatedAction(
                GhostHero ghost,
                HeroAction request,
                string levelId,
                int subLevelIndex,
                long serverTimeMs,
                double ratio)
            {
                Ghost = ghost;
                Request = request;
                LevelId = levelId;
                SubLevelIndex = subLevelIndex;
                ServerTimeMs = serverTimeMs;
                Ratio = ratio;
            }
        }

        private readonly ServerSession session;
        private readonly Dictionary<string, GhostHero> ghosts = [];
        private readonly Dictionary<string, long> lastActionIds = [];
        private readonly Dictionary<string, long> lastActionMs = [];
        private readonly Dictionary<string, uint> appliedSpawnVersions = [];
        private readonly List<HeroActionEvent> pending = [];

        private int epoch = -1;

        public ServerGhostManager(ServerSession session)
        {
            this.session = session;
        }

        public int Count => ghosts.Count;

        public void BeginEpoch(int value)
        {
            epoch = value;
            Clear();
            Logger.Information("Ghost action epoch begin: {Epoch}", value);
        }

        public void Clear()
        {
            foreach (var ghost in ghosts.Values)
                ghost.Dispose();

            ghosts.Clear();
            lastActionIds.Clear();
            lastActionMs.Clear();
            appliedSpawnVersions.Clear();
            pending.Clear();
        }

        public void RemoveGuest(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;

            if (ghosts.Remove(guid, out var ghost))
                ghost.Dispose();

            lastActionIds.Remove(guid);
            lastActionMs.Remove(guid);
            lastActionMs.Remove(WeaponKey(guid));
            lastActionMs.Remove(TalismanKey(guid));
            appliedSpawnVersions.Remove(guid);
        }

        /// <summary>
        /// 处理客户端上报的英雄动作
        /// </summary>
        public void HandleAction(string senderGuid, HeroAction? action)
        {
            var validated = Validate(senderGuid, action);
            if (validated == null) return;

            Execute(validated);
        }

        /// <summary>
        /// 取出本帧待广播的动作事件
        /// </summary>
        public List<HeroActionEvent> DrainActions()
        {
            if (pending.Count == 0) return [];

            var result = new List<HeroActionEvent>(pending);
            pending.Clear();
            return result;
        }

        private ValidatedAction? Validate(string senderGuid, HeroAction? request)
        {
            if (string.IsNullOrEmpty(senderGuid) || request == null) return null;
            if (epoch < 0 || request.Epoch != epoch) return null;
            if (request.Slot is < 0 or > 1) return null;
            if (request.Kind == HeroActionKind.Talisman && request.Slot != 0)
                return null;
            if (request.Item == null || string.IsNullOrEmpty(request.Item.ItemId)) return null;
            if (!double.IsFinite(request.Ratio)) return null;
            if (request.ActionId <= 0) return null;

            if (lastActionIds.TryGetValue(senderGuid, out long lastId)
                && request.ActionId <= lastId)
                return null;

            var game = Game.Class.ME;
            if (game == null) return null;

            if (!session.ClientFrames.TryGetHeroState(senderGuid, out var state))
                return null;
            if (state.IsDead || state.Life <= 0) return null;

            // 客户端进入子关卡后 服务端 curLevel 仍可能停在主关卡
            string levelId = state.LevelId;
            int subLevelIndex = state.SubLevelIndex;

            var level = ResolveLevel(game, levelId, subLevelIndex);

            // 请求必须与发送者当前关卡一致 防止跨关卡执行
            if (request.LevelId != levelId || request.SubLevelIndex != subLevelIndex)
                return null;

            double dx = state.Pos.CX + state.Pos.XR - (request.CX + request.XR);
            double dy = state.Pos.CY + state.Pos.XY - (request.CY + request.YR);
            if (dx * dx + dy * dy > MaxCastDistanceSq)
            {
                Logger.Warning(
                    "Reject action: guid={Guid} kind={Kind} slot={Slot} distance too far",
                    senderGuid, request.Kind, request.Slot);
                return null;
            }

            long now = session.CurrentTimeStamp;
            string rateKey = request.Kind switch
            {
                HeroActionKind.Weapon => WeaponKey(senderGuid),
                HeroActionKind.Talisman => TalismanKey(senderGuid),
                _ => senderGuid,
            };
            long interval = request.Kind switch
            {
                HeroActionKind.Weapon => MinWeaponIntervalMs,
                HeroActionKind.Talisman => MinTalismanIntervalMs,
                _ => MinSkillIntervalMs,
            };

            if (lastActionMs.TryGetValue(rateKey, out long last)
                && now - last < interval)
                return null;

            var ghost = EnsureGhost(senderGuid, game, level!);
            if (ghost == null) return null;

            lastActionIds[senderGuid] = request.ActionId;
            lastActionMs[rateKey] = now;

            double ratio = request.Kind == HeroActionKind.ActiveSkill
                ? System.Math.Clamp(request.Ratio, 0.0, 1.0)
                : 1.0;

            return new ValidatedAction(
                ghost, request, levelId, subLevelIndex, now, ratio);
        }

        private void Execute(ValidatedAction action)
        {
            var request = action.Request;

            action.Ghost.SetTransform(
                request.CX, request.CY, request.XR, request.YR,
                request.Dir, request.DX, request.DY);

            //等级计算
            int brutality = System.Math.Clamp(request.BrutalityTier, 0, MaxTier);
            int survival = System.Math.Clamp(request.SurvivalTier, 0, MaxTier);
            int tactic = System.Math.Clamp(request.TacticTier, 0, MaxTier);

            if (action.Ghost.Hero.brutalityTier != brutality
                || action.Ghost.Hero.survivalTier != survival
                || action.Ghost.Hero.tacticTier != tactic)
            {
                action.Ghost.SetTiers(brutality, survival, tactic);
            }

            ApplySpawnTalisman(action.Ghost);

            switch (request.Kind)
            {
                case HeroActionKind.ActiveSkill:
                    ExecuteSkill(action);
                    break;
                case HeroActionKind.Weapon:
                    ExecuteWeapon(action);
                    break;
                case HeroActionKind.Talisman:
                    ExecuteTalisman(action);
                    break;
            }
        }

        /// <summary>
        /// 服务端释放技能
        /// </summary>
        /// <param name="action">传入数据</param>
        private void ExecuteSkill(ValidatedAction action)
        {
            var request = action.Request;
            var ghost = action.Ghost;

            if (!ghost.Actions.PrepareSkill(request.Slot, request.Item)) return;

            if (!ghost.Actions.IsSkillReady(request.Slot))
            {
                Logger.Debug(
                    "Reject skill cast: guid={Guid} slot={Slot} not ready",
                    ghost.OwnerGuid, request.Slot);
                return;
            }

            if (!ghost.Actions.CastSkill(request.Slot, action.Ratio)) return;

            pending.Add(BuildEvent(action));

            Logger.Information(
                "Skill cast accepted: guid={Guid} slot={Slot} item={Item} action={ActionId}",
                ghost.OwnerGuid, request.Slot, request.Item.ItemId, request.ActionId);
        }

        /// <summary>
        /// 服务端释放攻击
        /// </summary>
        /// <param name="action">数据</param>
        private void ExecuteWeapon(ValidatedAction action)
        {
            var request = action.Request;
            var ghost = action.Ghost;

            if (!ghost.Actions.PrepareWeapon(request.Slot, request.Item)) return;
            if (!ghost.Actions.UseWeapon(request.Slot, request.Cycle)) return;

            pending.Add(BuildEvent(action));

            Logger.Information(
                "Weapon use accepted: guid={Guid} slot={Slot} item={Item} cycle={Cycle} action={ActionId}",
                ghost.OwnerGuid, request.Slot, request.Item.ItemId,
                request.Cycle, request.ActionId);
        }

        /// <summary>
        /// 服务端装备项链
        /// </summary>
        private void ExecuteTalisman(ValidatedAction action)
        {
            var request = action.Request;
            var ghost = action.Ghost;

            if (!ghost.Actions.ApplyTalisman(request.Item))
            {
                Logger.Warning(
                    "Talisman rejected: guid={Guid} item={Item} action={ActionId}",
                    ghost.OwnerGuid, request.Item.ItemId, request.ActionId);
                return;
            }

            pending.Add(BuildEvent(action));

            Logger.Information(
                "Talisman accepted: guid={Guid} item={Item} action={ActionId}",
                ghost.OwnerGuid, request.Item.ItemId, request.ActionId);
        }

        /// <summary>
        /// 为其他客户端创建事件
        /// </summary>
        /// <param name="action">传入数据</param>
        /// <returns></returns>
        private static HeroActionEvent BuildEvent(ValidatedAction action)
        {
            var request = action.Request;
            var ghost = action.Ghost;

            return new HeroActionEvent
            {
                Epoch = request.Epoch,
                ActionId = request.ActionId,
                CasterGuid = ghost.OwnerGuid,
                Kind = request.Kind,
                Slot = request.Slot,
                Item = request.Item,
                CX = request.CX,
                CY = request.CY,
                XR = request.XR,
                YR = request.YR,
                Dir = request.Dir,
                DX = request.DX,
                DY = request.DY,
                Ratio = action.Ratio,
                Cycle = request.Cycle,
                BrutalityTier = ghost.Hero.brutalityTier,
                SurvivalTier = ghost.Hero.survivalTier,
                TacticTier = ghost.Hero.tacticTier,
                LevelId = action.LevelId,
                SubLevelIndex = action.SubLevelIndex,
                ServerTimeMs = action.ServerTimeMs,
            };
        }

        /// <summary>
        /// 按关卡 id 和子关卡索引查找服务端 Level
        /// </summary>
        private static Level? ResolveLevel(Game game, string levelId, int subLevelIndex)
        {
            var subLevels = game.subLevels;
            if (subLevels != null)
            {
                for (int i = 0; i < subLevels.length; i++)
                {
                    if (subLevels.getDyn(i) is not Level level) continue;
                    if ((level.map?.id?.ToString() ?? string.Empty) != levelId) continue;
                    if (level.GetSubLevelIndex() != subLevelIndex) continue;
                    return level;
                }
            }

            var current = game.curLevel;
            if (current != null
                && (current.map?.id?.ToString() ?? string.Empty) == levelId
                && current.GetSubLevelIndex() == subLevelIndex)
                return current;

            return null;
        }

        private GhostHero? EnsureGhost(string guid, Game game, Level level)
        {
            if (ghosts.TryGetValue(guid, out var ghost))
            {
                if (ghost.MatchesLevel(level)) return ghost;

                ghost.Dispose();
                ghosts.Remove(guid);
                appliedSpawnVersions.Remove(guid);
            }

            ghost = GhostHero.Create(game, level, guid, true);
            if (ghost == null) return null;

            ghosts[guid] = ghost;
            ApplySpawnTalisman(ghost);
            return ghost;
        }

        /// <summary>
        /// 用客户端最新 spawn 中的项链同步所有 GhostHero
        /// </summary>
        public void SyncTalismans()
        {
            foreach (var ghost in ghosts.Values)
                ApplySpawnTalisman(ghost);
        }

        private void ApplySpawnTalisman(GhostHero ghost)
        {
            if (!session.ClientFrames.TryGetHeroSpawn(
                    ghost.OwnerGuid, out var spawn))
                return;

            if (appliedSpawnVersions.TryGetValue(
                    ghost.OwnerGuid, out uint version)
                && version == spawn.Version)
                return;

            ghost.Actions.ApplyTalisman(spawn.Talisman);
            appliedSpawnVersions[ghost.OwnerGuid] = spawn.Version;
        }

        private static string WeaponKey(string guid) => guid + ":weapon";
        private static string TalismanKey(string guid) => guid + ":talisman";
    }
}
