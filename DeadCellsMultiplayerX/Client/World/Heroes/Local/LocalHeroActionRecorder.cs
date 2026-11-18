namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using dc.en;
using dc.tool;
using dc.tool.hero;
using dc.tool.hero.weap;
using dc.tool.weap;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;

/// <summary>Owns local action hooks, shield phases and the single command-recording path.</summary>
internal sealed class LocalHeroActionRecorder(GuestGameSession session) : IClientActionHookSink, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<LocalHeroActionRecorder>();
    private readonly GuestGameSession session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly Dictionary<BaseShield, ShieldState> shields = [with(ReferenceEqualityComparer.Instance)];
    private IDisposable? hooks;
    private bool installing;
    private bool disposed;
    private int generation;

    private sealed class ShieldState
    {
        public bool Holding { get; set; }
        public bool Parrying { get; set; }
    }

    internal HeroCommandQueue Commands { get; } = new();

    public void Init()
    {
        ObjectDisposedException.ThrowIf(this.disposed || this.session.IsDisposed, this);
        if (this.hooks != null || this.installing)
        {
            return;
        }
        this.installing = true;
        try
        {
            var binding = ClientActionHookBinding.Install(this);
            if (this.disposed || this.session.IsDisposed)
            {
                binding.Dispose();
                return;
            }
            this.hooks = binding;
        }
        finally
        {
            this.installing = false;
        }
    }

    internal void ObserveEpoch(int epoch)
    {
        if (!this.disposed && this.Commands.ObserveEpoch(epoch))
        {
            this.generation++;
            this.shields.Clear();
        }
    }

    internal void OnHeroInitialized(Hero hero)
    {
        if (!this.disposed && ReferenceEquals(hero, GuestGameSession.Game?.hero))
        {
            this.generation++;
            this.shields.Clear();
        }
    }

    public void OnActiveSkill(Hook_HeroActiveSkillsManager.orig_onActiveSkill orig,
        HeroActiveSkillsManager self, int id, double ratio)
    {
        var hero = self.hero;
        var local = ReferenceEquals(hero, GuestGameSession.Game?.hero);
        orig(self, id, ratio);
        if (local && hero != null)
        {
            this.RecordCommand(hero, HeroActionKind.ActiveSkill, id, new HeroIntent { Ratio = ratio });
        }
    }

    public void OnWeaponExecute(Hook_BeheadedWeaponsManager.orig_onWeaponExecute orig,
        BeheadedWeaponsManager self, Weapon weapon)
    {
        var hero = self?.hero;
        var local = hero != null && ReferenceEquals(hero, GuestGameSession.Game?.hero);
        var item = local ? weapon?.item : null;
        var slot = item?.posID ?? -1;
        if (!local || hero == null || weapon == null || item == null || slot is < 0 or > 1)
        {
            orig(self, weapon);
            return;
        }
        // Native execution may advance the cycle or trigger nested actions/epoch changes.
        var cycle = weapon.get_cycle();
        orig(self, weapon);
        this.RecordCommand(hero, HeroActionKind.Weapon, slot, new HeroIntent { Cycle = cycle });
    }

    public void OnShieldChargeStart(Hook_BaseShield.orig_onShieldChargeStart orig, BaseShield self)
    {
        orig(self);
        this.RecordShield(self, ShieldIntentKind.HoldStart);
    }

    public void OnShieldReleased(Hook_BaseShield.orig_onShieldReleased orig, BaseShield self)
    {
        orig(self);
        this.RecordShield(self, ShieldIntentKind.HoldEnd);
    }

    public void OnStartParry(Hook_BaseShield.orig_startParry orig, BaseShield self)
    {
        orig(self);
        this.RecordShield(self, ShieldIntentKind.ParryStart);
    }

    public void OnShieldEndParry(Hook_BaseShield.orig_onShieldEndParry orig, BaseShield self)
    {
        orig(self);
        this.RecordShield(self, ShieldIntentKind.ParryEnd);
    }

    private void RecordShield(BaseShield shield, ShieldIntentKind phase)
    {
        var hero = GuestGameSession.Game?.hero;
        if (this.disposed || !this.session.CanRecordActions || shield == null || shield.destroyed
            || hero == null || !ReferenceEquals(shield.owner, hero)
            || shield.item == null || shield.item.posID is < 0 or > 1)
        {
            return;
        }
        if (!this.shields.TryGetValue(shield, out var state))
        {
            this.shields.Add(shield, state = new ShieldState());
        }
        if ((phase == ShieldIntentKind.HoldStart && state.Holding)
            || (phase == ShieldIntentKind.ParryStart && state.Parrying)
            || (phase == ShieldIntentKind.ParryEnd && !state.Parrying))
        {
            return;
        }
        if (!this.RecordCommand(hero, HeroActionKind.Shield, shield.item.posID,
            new HeroIntent { Ratio = 1.0, ShieldKind = phase }))
        {
            return;
        }

        // Only accepted commands advance deduplication. HoldEnd intentionally remains repeatable.
        switch (phase)
        {
            case ShieldIntentKind.HoldStart:
                state.Holding = true;
                break;
            case ShieldIntentKind.HoldEnd:
                state.Holding = false;
                break;
            case ShieldIntentKind.ParryStart:
                state.Parrying = true;
                break;
            case ShieldIntentKind.ParryEnd:
                state.Parrying = false;
                break;
            default:
                break;
        }
    }

    private bool RecordCommand(Hero hero, HeroActionKind kind, int slot, HeroIntent intent)
    {
        var world = this.session.ClientWorld;
        if (!this.CanRecord(hero) || world == null || world.Epoch < 0)
        {
            return false;
        }
        var epoch = world.Epoch;
        var current = this.generation;
        var level = hero._level;
        var levelId = level?.map?.id?.ToString() ?? string.Empty;
        var subLevel = level?.GetSubLevelIndex() ?? -1;
        // Native state reads can re-enter game code. Do not publish into a replaced world/hero.
        if (!this.CanRecord(hero) || this.generation != current || !ReferenceEquals(level, hero._level)
            || !ReferenceEquals(world, this.session.ClientWorld) || world.Epoch != epoch)
        {
            return false;
        }
        this.Commands.Enqueue(new HeroCommand
        {
            Epoch = epoch,
            CommandId = this.Commands.NextCommandId(),
            Kind = kind,
            Slot = slot,
            Intent = intent,
            ClientTimeMs = this.session.SyncedTimeMs,
            LevelId = levelId,
            SubLevelIndex = subLevel,
        });
        return true;
    }

    private bool CanRecord(Hero hero) => !this.disposed && this.session.CanRecordActions
        && !hero.destroyed && ReferenceEquals(hero, GuestGameSession.Game?.hero);

    internal void DebugCastSkill(string itemId, int slot)
    {
        var hero = GuestGameSession.Game?.hero;
        if (hero != null && this.RecordCommand(hero, HeroActionKind.ActiveSkill, slot, new HeroIntent { Ratio = 1.0 }))
        {
            Logger.Information("Debug skill cast recorded: item={Item} slot={Slot}", itemId, slot);
        }
    }

    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
    {
        var hero = GuestGameSession.Game?.hero;
        if (hero != null && this.RecordCommand(hero, HeroActionKind.Weapon, slot, new HeroIntent { Cycle = cycle }))
        {
            Logger.Information("Debug weapon use recorded: item={Item} slot={Slot} cycle={Cycle}", itemId, slot, cycle);
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }
        this.disposed = true;
        this.generation++;
        var binding = this.hooks;
        this.hooks = null;
        try
        {
            binding?.Dispose();
        }
        finally
        {
            this.Commands.Clear();
            this.shields.Clear();
        }
    }
}
