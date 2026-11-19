namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;

using System.Diagnostics;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Loadout;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;
using DeadCellsMultiplayerX.Server.World.Replication.Frames;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Simulation.Actions;
using Serilog;

/// <summary>
/// 动作命令的唯一入口：信封校验 -> 结果去重 -> 模块校验/执行 -> 生成 outcome。
/// 不信任客户端上传的物品、tiers 与 pose。
/// </summary>
internal sealed class HeroCommandDispatcher
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "HeroCommandDispatcher");

    private readonly ServerActorState actorState;
    private readonly ServerHeroSpawner spawner;
    private readonly SpawnSynchronizer spawns;
    private readonly ClientFrameCollector frames;
    private readonly Dictionary<HeroActionKind, IHeroActionModule> modules;
    private readonly MobRewind rewind;
    private readonly Func<long> clock;
    private readonly Func<Game?> game;
    private readonly HeroCommandJournal journal = new();

    public HeroCommandDispatcher(
        ServerActorState actorState,
        ServerHeroSpawner spawner,
        SpawnSynchronizer spawns,
        ClientFrameCollector frames,
        ServerEntityRegistry entities,
        ServerEntityHistory history,
        Func<long> clock,
        Func<Game?> game)
    {
        this.actorState = actorState;
        this.spawner = spawner;
        this.spawns = spawns;
        this.frames = frames;
        this.clock = clock;
        this.game = game;
        this.rewind = new MobRewind(entities, history);
        this.modules = ActionModuleCatalog
            .CreateDefault(clock)
            .ToDictionary(static module => module.Rules.Kind);
    }

    public HeroCommandJournal Journal => this.journal;

    public void BeginEpoch() => this.journal.Clear();

    public void Handle(string senderGuid, HeroCommand? command)
    {
        var started = Stopwatch.GetTimestamp();
        var context = new HeroCommandContext(
            senderGuid, command ?? new HeroCommand(), this.clock());

        try
        {
            this.HandleCore(senderGuid, command, context, started);
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "Command dispatch failed: guid={Guid} kind={Kind} cmd={CommandId}",
                senderGuid, command?.Kind, command?.CommandId);

            var outcome = this.CreateOutcome(
                context, null, HeroRejection.ExecutionException,
                HeroOutcomeStatus.Rejected);

            if (command != null)
            {
                this.actorState.Find(senderGuid)?.Commands.Record(
                    command.CommandId, outcome);
            }

            this.actorState.RecordEvent(outcome);
            this.RecordJournal(
                context, HeroRejection.ExecutionException, started, "Exception");
        }
    }

    private void HandleCore(
        string senderGuid,
        HeroCommand? command,
        HeroCommandContext context,
        long started)
    {
        if (command == null || string.IsNullOrWhiteSpace(senderGuid))
        {
            this.RecordJournal(context, HeroRejection.InvalidRequest, started, "InvalidRequest");
            return;
        }

        if (this.actorState.Epoch < 0 || command.Epoch != this.actorState.Epoch)
        {
            this.RecordJournal(context, HeroRejection.EpochMismatch, started, "EpochMismatch");
            return;
        }

        if (command.CommandId <= 0)
        {
            this.RecordJournal(
                context, HeroRejection.InvalidCommandId, started, "InvalidCommandId");
            return;
        }

        var hero = this.actorState.GetOrCreate(senderGuid);

        if (hero.Commands.TryGetCached(command.CommandId, out var cached)
            && cached != null)
        {
            // 重发同一命令时重播原结果；客户端按 OutcomeId 去重。
            this.actorState.RecordEvent(cached);
            this.RecordJournal(context, HeroRejection.None, started, "Duplicate");
            return;
        }

        if (command.Intent == null)
        {
            this.Reject(context, hero, HeroRejection.InvalidRequest, started);
            return;
        }

        if (command.Slot is < 0 or > 1)
        {
            this.Reject(context, hero, HeroRejection.InvalidSlot, started);
            return;
        }

        if (!double.IsFinite(command.Intent.Ratio))
        {
            this.Reject(context, hero, HeroRejection.InvalidRatio, started);
            return;
        }

        var currentGame = this.game();
        if (currentGame == null)
        {
            this.Reject(context, hero, HeroRejection.GameUnavailable, started);
            return;
        }

        if (!this.frames.TryGetHeroState(senderGuid, out var state)
            || state == null)
        {
            this.Reject(context, hero, HeroRejection.NoClientState, started);
            return;
        }

        if (state.IsDead || state.Life <= 0)
        {
            this.Reject(context, hero, HeroRejection.HeroDead, started);
            return;
        }

        if (command.LevelId != state.LevelId
            || command.SubLevelIndex != state.SubLevelIndex)
        {
            this.Reject(context, hero, HeroRejection.LevelMismatch, started);
            return;
        }

        var level = LevelUtils.ResolveLevel(
            currentGame, state.LevelId, state.SubLevelIndex);
        if (level == null)
        {
            this.Reject(context, hero, HeroRejection.LevelUnavailable, started);
            return;
        }

        context.Game = currentGame;
        context.Snapshot = state;
        context.Level = level;
        context.LevelId = state.LevelId;
        context.SubLevelIndex = state.SubLevelIndex;
        context.Hero = hero;
        context.Ratio = command.Kind == HeroActionKind.ActiveSkill
            ? Math.Clamp(command.Intent.Ratio, 0.0, 1.0)
            : 1.0;

        var ensured = this.spawner.Ensure(
            currentGame, senderGuid, state, out _);
        if (ensured?.Ghost == null)
        {
            this.Reject(context, hero, HeroRejection.GhostUnavailable, started);
            return;
        }

        hero = ensured;
        context.Hero = hero;
        this.spawns.Sync(hero);

        if (!this.modules.TryGetValue(command.Kind, out var module))
        {
            this.Reject(context, hero, HeroRejection.UnsupportedKind, started);
            return;
        }

        if (!TryResolveItem(module.Rules.SlotPolicy, command.Slot, hero, out var item))
        {
            this.Reject(context, hero, HeroRejection.LoadoutUnavailable, started);
            return;
        }

        var rejection = module.Validate(context, item!);
        if (rejection != HeroRejection.None)
        {
            this.Reject(context, hero, rejection, started);
            return;
        }

        HeroRejection execution = HeroRejection.ExecutionFailed;

        try
        {
            var scope = command.Kind == HeroActionKind.Weapon
                ? this.rewind.Begin(
                    context.Level!, command.ClientTimeMs, context.ServerTimeMs)
                : null;

            try
            {
                execution = module.Execute(context, item!);
            }
            finally
            {
                scope?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "Command execution failed: guid={Guid} kind={Kind} slot={Slot} " +
                "cmd={CommandId}",
                context.SenderGuid, command.Kind, command.Slot, command.CommandId);

            execution = HeroRejection.ExecutionException;
        }

        var outcome = this.CreateOutcome(
            context,
            item,
            execution,
            execution == HeroRejection.None
                ? HeroOutcomeStatus.Executed
                : HeroOutcomeStatus.Rejected);

        hero.Commands.Record(command.CommandId, outcome);
        this.actorState.RecordEvent(outcome);
        this.RecordJournal(
            context, execution, started,
            outcome.Status == HeroOutcomeStatus.Executed ? "Executed" : "Rejected");
    }

    private static bool TryResolveItem(
        SlotPolicy policy,
        int slot,
        ServerHero hero,
        out ItemRef? item)
    {
        item = null;

        var category = policy switch
        {
            SlotPolicy.Weapon => EquipmentCategory.Weapon,
            SlotPolicy.Active => EquipmentCategory.Active,
            SlotPolicy.TalismanOnly => EquipmentCategory.Talisman,
            _ => (EquipmentCategory?)null,
        };

        if (category == null)
        {
            return false;
        }

        if (policy == SlotPolicy.TalismanOnly && slot != 0)
        {
            return false;
        }

        return hero.Loadout.TryResolve(category.Value, slot, out item);
    }

    private void Reject(
        HeroCommandContext context,
        ServerHero hero,
        HeroRejection rejection,
        long started)
    {
        var outcome = this.CreateOutcome(
            context, null, rejection, HeroOutcomeStatus.Rejected);
        hero.Commands.Record(context.Command.CommandId, outcome);
        this.actorState.RecordEvent(outcome);

        if (rejection != HeroRejection.ExecutionException)
        {
            Logger.Debug(
                "Command rejected: guid={Guid} kind={Kind} slot={Slot} " +
                "cmd={CommandId} reason={Reason}",
                context.SenderGuid, context.Command.Kind, context.Command.Slot,
                context.Command.CommandId, rejection);
        }

        this.RecordJournal(context, rejection, started, "Rejected");
    }

    private HeroOutcome CreateOutcome(
        HeroCommandContext context,
        ItemRef? item,
        HeroRejection rejection,
        HeroOutcomeStatus status)
    {
        var command = context.Command;
        var intent = command.Intent;
        var outcome = new HeroOutcome
        {
            Epoch = command.Epoch,
            OutcomeId = this.actorState.NextOutcomeId(),
            CommandId = command.CommandId,
            Cause = HeroOutcomeCause.Command,
            CasterGuid = context.SenderGuid,
            Kind = command.Kind,
            Slot = command.Slot,
            Item = item ?? new ItemRef(),
            Ratio = context.Ratio,
            Cycle = intent?.Cycle ?? 0,
            LevelId = context.LevelId,
            SubLevelIndex = context.SubLevelIndex,
            ServerTimeMs = context.ServerTimeMs,
            Status = status,
            Rejection = rejection,
            Shield = command.Kind == HeroActionKind.Shield && intent != null
                ? new ShieldOutcome { Phase = intent.ShieldKind }
                : null,
        };

        var ghost = context.Ghost;
        if (ghost != null)
        {
            var hero = ghost.Hero;
            outcome.CX = hero.cx;
            outcome.CY = hero.cy;
            outcome.XR = hero.xr;
            outcome.YR = hero.yr;
            outcome.Dir = hero.dir;
            outcome.DX = hero.dx;
            outcome.DY = hero.dy;
            outcome.BrutalityTier = hero.brutalityTier;
            outcome.SurvivalTier = hero.survivalTier;
            outcome.TacticTier = hero.tacticTier;
        }

        return outcome;
    }

    private void RecordJournal(
        HeroCommandContext context,
        HeroRejection rejection,
        long started,
        string result)
    {
        var command = context.Command;

        this.journal.Record(new HeroCommandJournal.Entry(
            context.ServerTimeMs,
            context.SenderGuid,
            command.Epoch,
            command.CommandId,
            command.Kind.ToString(),
            command.Slot,
            rejection == HeroRejection.None ? result : rejection.ToString(),
            Stopwatch.GetElapsedTime(started).TotalMilliseconds));
    }
}
