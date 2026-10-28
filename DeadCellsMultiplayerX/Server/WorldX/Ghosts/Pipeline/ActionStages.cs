using System;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.Execution;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.World;
using DeadCellsMultiplayerX.Server.WorldX.Hubs;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.Pipeline
{
    /// <summary>
    /// 一系列安全判断,防止报错
    /// </summary>
    internal sealed class AdmissionStage : IActionStage
    {
        private readonly WorldState world;
        private readonly ClientWorldFrameCollector frames;
        private readonly Func<Game?> game;

        public AdmissionStage(
            WorldState world,
            ClientWorldFrameCollector frames,
            Func<Game?> game)
        {
            this.world = world;
            this.frames = frames;
            this.game = game;
        }

        public StageResult Process(ActionContext context)
        {
            //判断请求是否为空
            var request = context.Request;
            if (string.IsNullOrWhiteSpace(context.SenderGuid) || request == null)
                return StageResult.Reject(Rejection.InvalidRequest);

            //判断帧是否错配
            if (world.Epoch < 0 || request.Epoch != world.Epoch)
                return StageResult.Reject(Rejection.EpochMismatch);

            //判断对应槽位大小
            if (request.Slot is < 0 or > 1)
                return StageResult.Reject(Rejection.InvalidSlot);

            //判断项链槽
            if (request.Kind == HeroActionKind.Talisman && request.Slot != 0)
                return StageResult.Reject(Rejection.InvalidSlot);

            //判断物品是否为空
            if (request.Item == null || string.IsNullOrEmpty(request.Item.ItemId))
                return StageResult.Reject(Rejection.InvalidItem);

            //判断释放进度(为同步武器蓄力,默认为1)
            if (!double.IsFinite(request.Ratio))
                return StageResult.Reject(Rejection.InvalidRatio);

            //判断动作id
            if (request.ActionId <= 0)
                return StageResult.Reject(Rejection.InvalidActionId);

            //检查客户对应实例是否存在
            var actor = world.Find(context.SenderGuid);
            if (actor != null && !actor.Admission.IsNewAction(request.ActionId))
                return StageResult.Reject(Rejection.StaleAction);

            //检查游戏
            var currentGame = game();
            if (currentGame == null)
                return StageResult.Reject(Rejection.GameUnavailable);

            //判断最新状态不为空
            if (!frames.TryGetHeroState(context.SenderGuid, out var state))
                return StageResult.Reject(Rejection.NoClientState);

            //判断客户是否死亡
            if (state.IsDead || state.Life <= 0)
                return StageResult.Reject(Rejection.HeroDead);

            // 客户端进入子关卡后 服务端 curLevel 仍可能停在主关卡
            string levelId = state.LevelId;
            int subLevelIndex = state.SubLevelIndex;

            // 请求必须与发送者当前关卡一致 防止跨关卡执行
            if (request.LevelId != levelId || request.SubLevelIndex != subLevelIndex)
                return StageResult.Reject(Rejection.LevelMismatch);

            //拿到对应关卡实例,来进行跨关卡释放技能或武器
            var level = LevelUtils.ResolveLevel(
                currentGame, levelId, subLevelIndex);
            if (level == null)
                return StageResult.Reject(Rejection.LevelUnavailable);

            //最大的攻击范围
            double dx = state.Pos.CX + state.Pos.XR
                - (request.CX + request.XR);
            double dy = state.Pos.CY + state.Pos.XY
                - (request.CY + request.YR);

            if (dx * dx + dy * dy > ActionPolicy.MaxCastDistanceSq)
                return StageResult.Reject(Rejection.TooFar);

            context.Game = currentGame;
            context.Snapshot = state;
            context.Level = level;
            context.LevelId = levelId;
            context.SubLevelIndex = subLevelIndex;
            context.Actor = actor;
            context.Ratio = request.Kind == HeroActionKind.ActiveSkill
                ? Math.Clamp(request.Ratio, 0.0, 1.0)
                : 1.0;

            return StageResult.Continue;
        }
    }

    /// <summary>
    /// 绑定 GhostHero
    /// </summary>
    internal sealed class BindingStage : IActionStage
    {
        private readonly WorldState world;
        private readonly SpawnSynchronizer spawns;

        public BindingStage(
            WorldState world,
            SpawnSynchronizer spawns)
        {
            this.world = world;
            this.spawns = spawns;
        }

        public StageResult Process(ActionContext context)
        {
            var request = context.Request!;
            var actor = context.Actor ?? world.GetOrCreate(context.SenderGuid);

            bool created = actor.EnsureGhost(
                context.Game!,
                context.Level!,
                context.Snapshot?.Pos?.CX ?? 0,
                context.Snapshot?.Pos?.CY ?? 0);

            if (actor.Ghost == null)
                return StageResult.Reject(Rejection.GhostUnavailable);

            if (created)
                spawns.Sync(actor);

            actor.Admission.Commit(request.ActionId);

            if (!created)
                spawns.Sync(actor);

            context.Actor = actor;
            return StageResult.Continue;
        }
    }

    /// <summary>
    /// 写入并执行动作
    /// </summary>
    internal sealed class ExecutionStage : IActionStage
    {
        private readonly ActionExecutor executor;
        private readonly MobRewind rewind;

        public ExecutionStage(
            ActionExecutor executor,
            MobRewind rewind)
        {
            this.executor = executor;
            this.rewind = rewind;
        }

        public StageResult Process(ActionContext context)
        {
            var request = context.Request!;

            IDisposable? scope = request.Kind == HeroActionKind.Weapon
                ? rewind.Begin(
                    context.Level!,
                    request.ClientTimeMs,
                    context.ServerTimeMs)
                : null;

            try
            {
                return executor.TryExecute(
                        context.Ghost!, request, context.Ratio)
                    ? StageResult.Continue
                    : StageResult.Reject(Rejection.ExecutionRejected);
            }
            finally
            {
                scope?.Dispose();
            }
        }
    }

    /// <summary>
    /// 构建并记录广播事件
    /// </summary>
    internal sealed class PublicationStage : IActionStage
    {
        private readonly WorldState world;

        public PublicationStage(WorldState world)
        {
            this.world = world;
        }

        public StageResult Process(ActionContext context)
        {
            var request = context.Request!;
            var ghost = context.Ghost!;

            world.RecordEvent(new HeroActionEvent
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
                Ratio = context.Ratio,
                Cycle = request.Cycle,
                BrutalityTier = ghost.Hero.brutalityTier,
                SurvivalTier = ghost.Hero.survivalTier,
                TacticTier = ghost.Hero.tacticTier,
                LevelId = context.LevelId,
                SubLevelIndex = context.SubLevelIndex,
                ServerTimeMs = context.ServerTimeMs,
            });

            return StageResult.Continue;
        }
    }
}
