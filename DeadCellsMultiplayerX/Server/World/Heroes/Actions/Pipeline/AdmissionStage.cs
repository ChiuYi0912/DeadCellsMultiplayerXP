namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline;

using dc.pr;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using DeadCellsMultiplayerX.Server.World.Actors;

internal sealed class AdmissionStage(
    WorldState world,
    ClientFrameCollector frames,
    Func<Game?> game) : IActionStage
{
    private readonly WorldState world = world;
    private readonly ClientFrameCollector frames = frames;
    private readonly Func<Game?> game = game;

    public StageResult Process(ActionContext context)
    {
        //判断请求是否为空
        var request = context.Request;
        if (string.IsNullOrWhiteSpace(context.SenderGuid) || request == null)
        {
            return StageResult.Reject(Rejection.InvalidRequest);
        }

        //判断帧是否错配
        if (this.world.Epoch < 0 || request.Epoch != this.world.Epoch)
        {
            return StageResult.Reject(Rejection.EpochMismatch);
        }

        //判断对应槽位大小
        if (request.Slot is < 0 or > 1)
        {
            return StageResult.Reject(Rejection.InvalidSlot);
        }

        //判断项链槽
        if (request.Kind == HeroActionKind.Talisman && request.Slot != 0)
        {
            return StageResult.Reject(Rejection.InvalidSlot);
        }

        //判断盾牌动作
        if (request.Kind == HeroActionKind.Shield)
        {
            if (request.ShieldKind > ShieldActionKind.Counter)
            {
                return StageResult.Reject(Rejection.InvalidRequest);
            }

            if (request.ShieldKind == ShieldActionKind.Counter
                && request.SourceNetId == 0)
            {
                return StageResult.Reject(Rejection.InvalidRequest);
            }
        }

        //判断物品是否为空
        if (request.Item == null || string.IsNullOrEmpty(request.Item.ItemId))
        {
            return StageResult.Reject(Rejection.InvalidItem);
        }

        //判断释放进度(为同步武器蓄力,默认为1)
        if (!double.IsFinite(request.Ratio))
        {
            return StageResult.Reject(Rejection.InvalidRatio);
        }

        //判断动作id
        if (request.ActionId <= 0)
        {
            return StageResult.Reject(Rejection.InvalidActionId);
        }

        //检查客户对应实例是否存在
        var actor = this.world.Find(context.SenderGuid);
        if (actor != null && !actor.Admission.IsNewAction(request.ActionId))
        {
            return StageResult.Reject(Rejection.StaleAction);
        }

        //检查游戏
        var currentGame = this.game();
        if (currentGame == null)
        {
            return StageResult.Reject(Rejection.GameUnavailable);
        }

        //判断最新状态不为空
        if (!this.frames.TryGetHeroState(context.SenderGuid, out var state))
        {
            return StageResult.Reject(Rejection.NoClientState);
        }

        //判断客户是否死亡
        if (state.IsDead || state.Life <= 0)
        {
            return StageResult.Reject(Rejection.HeroDead);
        }

        // 客户端进入子关卡后 服务端 curLevel 仍可能停在主关卡
        var levelId = state.LevelId;
        var subLevelIndex = state.SubLevelIndex;

        // 请求必须与发送者当前关卡一致 防止跨关卡执行
        if (request.LevelId != levelId || request.SubLevelIndex != subLevelIndex)
        {
            return StageResult.Reject(Rejection.LevelMismatch);
        }

        //拿到对应关卡实例,来进行跨关卡释放技能或武器
        var level = LevelUtils.ResolveLevel(
            currentGame, levelId, subLevelIndex);
        if (level == null)
        {
            return StageResult.Reject(Rejection.LevelUnavailable);
        }

        //最大的攻击范围
        var dx = state.Pos.CX + state.Pos.XR
            - (request.CX + request.XR);
        var dy = state.Pos.CY + state.Pos.XY
            - (request.CY + request.YR);

        if ((dx * dx) + (dy * dy) > ActionPolicy.MAX_CAST_DISTANCE_SQ)
        {
            return StageResult.Reject(Rejection.TooFar);
        }

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
/// 绑定 ServerHeroCombatProxy
/// </summary>

