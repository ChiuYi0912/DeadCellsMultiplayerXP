namespace DeadCellsMultiplayerX.Server.World.Heroes.Shield;

using dc.en;
using dc.tool.atk;
using dc.tool.weap;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Engine.Adapters.Items;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;
using DeadCellsMultiplayerX.Server.World.Actors;
using Serilog;

/// <summary>
/// 观测服务端隐形盾的原生格挡结果，并把格挡/眩晕/技能打断写入动作结果流。
/// 不重新判定 Block/Partial；判定来自 AttackTargetImpl.updateHitResult。
/// </summary>
internal sealed class ShieldAttackArbiter(
    ServerActorState actorState,
    ServerEntityRegistry entities,
    Func<long> clock)
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ShieldAttackArbiter");

    private readonly Dictionary<BaseShield, HeroOutcome> pendingBlocks =
        [with(ReferenceEqualityComparer.Instance)];

    public void OnBlock(
        ServerHero hero,
        BaseShield shield,
        AttackData? sourceAtk,
        bool fullParry)
    {
        if (hero?.Ghost == null || shield == null || shield.destroyed)
        {
            return;
        }

        var now = clock();
        var blockKind = fullParry
            ? ShieldBlockKind.Full
            : ShieldBlockKind.Partial;

        var sourceMob = sourceAtk?.source as Mob;
        var sourceNetId = sourceMob != null
            && entities.TryGetNetId(sourceMob, out var netId)
                ? netId
                : 0u;

        var skillInterrupted = false;

        // 引擎的 applyStunAndBumpFromParry 只对直接攻击者生效；
        // 完美格挡在此基础上显式打断 mob 当前技能。
        if (fullParry
            && sourceMob != null
            && !sourceMob.destroyed
            && sourceAtk != null
            && sourceAtk.carrier == null
            && !sourceAtk.hasTag(14)
            && !sourceAtk.hasTag(18)
            && !sourceAtk.hasTag(22)
            && !sourceAtk.hasTag(29))
        {
            skillInterrupted = sourceMob.hasSkillCharging();
            sourceMob.interruptSkills();
        }

        var stunMs = 0L;
        var skillInfo = shield.get_curSkillInf();
        var stunSeconds = skillInfo?.props?.duration ?? 0.0;
        stunMs = (long)Math.Max(0, stunSeconds * 1000.0);

        var engineHero = hero.Ghost.Hero;
        var outcome = new HeroOutcome
        {
            Epoch = actorState.Epoch,
            OutcomeId = actorState.NextOutcomeId(),
            CommandId = 0,
            Cause = HeroOutcomeCause.WorldEvent,
            CasterGuid = hero.OwnerGuid,
            Kind = HeroActionKind.Shield,
            Slot = shield.item?.posID ?? 0,
            Item = shield.item != null
                ? ItemCodec.Collect(shield.item)
                : new ItemRef(),
            CX = engineHero.cx,
            CY = engineHero.cy,
            XR = engineHero.xr,
            YR = engineHero.yr,
            Dir = engineHero.dir,
            DX = engineHero.dx,
            DY = engineHero.dy,
            Ratio = 1.0,
            Cycle = 0,
            BrutalityTier = engineHero.brutalityTier,
            SurvivalTier = engineHero.survivalTier,
            TacticTier = engineHero.tacticTier,
            LevelId = hero.Level?.map?.id?.ToString() ?? string.Empty,
            SubLevelIndex = hero.Level is { } level
                ? level.GetSubLevelIndex()
                : -1,
            ServerTimeMs = now,
            Status = HeroOutcomeStatus.Executed,
            Rejection = HeroRejection.None,
            Shield = new ShieldOutcome
            {
                BlockKind = blockKind,
                Held = blockKind == ShieldBlockKind.Partial,
                Countered = false,
                SourceNetId = sourceNetId,
                StunDurationMs = stunMs,
                SkillInterrupted = skillInterrupted,
                ParryWindowEndMs = now + (long)Math.Max(
                    0, shield.totalParryDurationS * 1000.0),
            },
        };

        // onShieldBlock 与 shieldCounterAttack 在同一次原生调用栈内先后触发；
        // 先记录引用，counter 到达时把 Countered 改成 true，帧发布前即可看到最终值。
        this.pendingBlocks[shield] = outcome;
        actorState.RecordEvent(outcome);

        Logger.Information(
            "Shield block: guid={Guid} kind={Kind} source={Source} " +
            "stunMs={StunMs} interrupted={Interrupted}",
            hero.OwnerGuid, blockKind, sourceNetId, stunMs, skillInterrupted);
    }

    public void OnCounter(
        BaseShield shield,
        AttackData? sourceAtk,
        bool fullParry)
    {
        if (shield == null)
        {
            return;
        }

        if (this.pendingBlocks.Remove(shield, out var outcome)
            && outcome.Shield != null)
        {
            outcome.Shield.Countered = true;
            Logger.Information(
                "Shield counter: guid={Guid} fullParry={FullParry} source={Source}",
                outcome.CasterGuid, fullParry, sourceAtk?.source);
        }
    }

    public void Clear() => this.pendingBlocks.Clear();
}
