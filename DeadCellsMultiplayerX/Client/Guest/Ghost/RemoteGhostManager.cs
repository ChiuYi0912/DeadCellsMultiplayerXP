using System;
using System.Collections.Generic;
using dc.pr;
using DeadCellsMultiplayerX.Client.Guest.WorldX;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Utils;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Guest.Ghost
{
    /// <summary>
    /// 客户端远端 Ghost 管理
    /// 负责 GhostHero 生命周期与 WorldFrame 动作重放
    /// </summary>
    internal sealed class RemoteGhostManager
    {
        private const long MaxEventAgeMs = 2000;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "RemoteGhostManager");

        private readonly GuestClientSession session;
        private readonly RemoteWorld world;

        /// <summary>
        /// 本地每个客户端对应的技能释放hero
        /// </summary>
        private readonly Dictionary<string, GhostHero> ghosts = [];

        /// <summary>
        /// 已释放的事件
        /// </summary>
        private readonly HashSet<(string Caster, long ActionId)> appliedActions = [];

        private int lastEpoch = int.MinValue;

        public RemoteGhostManager(GuestClientSession session, RemoteWorld world)
        {
            this.session = session;
            this.world = world;
        }

        public int Count => ghosts.Count;


        /// <summary>
        /// 确保指定玩家在关卡中有 GhostHero
        /// 仅由 RemoteHeroManager 在创建 RemoteHero 时调用
        /// </summary>
        /// <param name="guid">客户id</param>
        /// <param name="level">对应关卡</param>
        /// <returns></returns>
        public GhostHero? Ensure(string guid, Level level)
        {
            if (string.IsNullOrEmpty(guid) || level == null) return null;

            if (ghosts.TryGetValue(guid, out var ghost))
            {
                if (ghost.MatchesLevel(level)) return ghost;

                ghost.Dispose();
                ghosts.Remove(guid);
            }

            ghost = GhostHero.Create(session.Game, level, guid);
            if (ghost == null) return null;

            ghosts[guid] = ghost;
            return ghost;
        }

        public bool TryGet(string guid, out GhostHero ghost)
            => ghosts.TryGetValue(guid, out ghost!);

        public void Remove(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;

            if (ghosts.Remove(guid, out var ghost))
                ghost.Dispose();
        }

        public void Clear()
        {
            foreach (var ghost in ghosts.Values)
                ghost.Dispose();

            ghosts.Clear();
        }

        /// <summary>
        /// 应用事件结果
        /// </summary>
        /// <param name="frame">世界帧</param>
        public void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;

            if (frame.Epoch != lastEpoch)
            {
                Clear();
                appliedActions.Clear();
                lastEpoch = frame.Epoch;
            }

            if (frame.Actions == null || frame.Actions.Count == 0) return;

            var game = session.Game;
            var level = game?.curLevel;
            if (game == null || level == null) return;

            string levelId = level.map?.id?.ToString() ?? string.Empty;
            int subLevelIndex = level.GetSubLevelIndex();
            string localGuid = world.LocalGuid;

            foreach (var evt in frame.Actions)
            {
                if (evt == null) continue;
                if (evt.Epoch != frame.Epoch) continue;
                if (!appliedActions.Add((evt.CasterGuid, evt.ActionId))) continue;
                if (string.Equals(evt.CasterGuid, localGuid, StringComparison.Ordinal)) continue;
                if (frame.ServerTimeMs - evt.ServerTimeMs > MaxEventAgeMs) continue;
                if (evt.LevelId != levelId || evt.SubLevelIndex != subLevelIndex) continue;
                if (evt.Slot is < 0 or > 1) continue;

                // GhostHero 生命周期由 RemoteHeroManager 管理 这里只重放已存在的实例
                if (!TryGet(evt.CasterGuid, out var ghost)) continue;

                if (!ghost.MatchesLevel(level))
                {
                    Remove(evt.CasterGuid);
                    continue;
                }

                ghost.SetTransform(
                    evt.CX, evt.CY, evt.XR, evt.YR, evt.Dir, evt.DX, evt.DY);
                ghost.SetTiers(
                    evt.BrutalityTier, evt.SurvivalTier, evt.TacticTier);

                switch (evt.Kind)
                {
                    case HeroActionKind.ActiveSkill:
                        if (!ghost.Actions.PrepareSkill(evt.Slot, evt.Item)) continue;
                        if (!ghost.Actions.ReplaySkill(evt.Slot))
                        {
                            Logger.Warning(
                                "Skill replay failed: caster={Caster} slot={Slot} item={Item}",
                                evt.CasterGuid, evt.Slot, evt.Item?.ItemId);
                        }
                        break;

                    case HeroActionKind.Weapon:
                        if (!ghost.Actions.PrepareWeapon(evt.Slot, evt.Item)) continue;
                        if (!ghost.Actions.UseWeapon(evt.Slot, evt.Cycle))
                        {
                            Logger.Warning(
                                "Weapon replay failed: caster={Caster} slot={Slot} item={Item}",
                                evt.CasterGuid, evt.Slot, evt.Item?.ItemId);
                        }
                        break;

                    case HeroActionKind.Talisman:
                        if (!ghost.Actions.ApplyTalisman(evt.Item))
                        {
                            Logger.Warning(
                                "Talisman replay failed: caster={Caster} item={Item}",
                                evt.CasterGuid, evt.Item?.ItemId);
                        }
                        break;
                }
            }
        }

        public void Dispose()
        {
            Clear();
            appliedActions.Clear();
        }
    }
}
