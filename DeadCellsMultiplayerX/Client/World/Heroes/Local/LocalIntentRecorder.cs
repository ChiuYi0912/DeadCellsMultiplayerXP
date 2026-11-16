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

/// <summary>
/// 本地动作意图记录：从 Hook 捕获技能/武器/盾牌，
/// 只上报“想做什么”，物品/等级/pose 由服务端解析。
/// </summary>
internal sealed class LocalIntentRecorder(
    GuestClientSession session,
    HeroCommandSender sender,
    LocalActionDeduper dedupe) : IClientActionHookSink
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "LocalIntentRecorder");

    private readonly GuestClientSession session = session
            ?? throw new ArgumentNullException(nameof(session));
    private readonly HeroCommandSender sender = sender
            ?? throw new ArgumentNullException(nameof(sender));
    private readonly LocalActionDeduper dedupe = dedupe
            ?? throw new ArgumentNullException(nameof(dedupe));

    public void Clear() => this.dedupe.Clear();

    /// <summary>
    /// 上报本地释放的主动技能
    /// </summary>
    public void OnActiveSkill(
        Hook_HeroActiveSkillsManager.orig_onActiveSkill orig,
        HeroActiveSkillsManager self,
        int id,
        double ratio)
    {
        var game = GuestClientSession.Game;
        var hero = self.hero;
        var isLocal = hero != null && ReferenceEquals(hero, game?.hero);

        orig(self!, id, ratio);

        if (!isLocal)
        {
            return;
        }

        this.SendCommand(
            hero!, HeroActionKind.ActiveSkill, id,
            new HeroIntent { Ratio = ratio });
    }

    /// <summary>
    /// 武器命中帧
    /// </summary>
    public void OnWeaponExecute(
        Hook_BeheadedWeaponsManager.orig_onWeaponExecute orig,
        BeheadedWeaponsManager self,
        Weapon weapon)
    {
        var game = GuestClientSession.Game;
        var hero = self?.hero;
        var isLocal = hero != null && ReferenceEquals(hero, game?.hero);
        var item = isLocal ? weapon?.item : null;
        var slot = item?.posID ?? -1;

        // 背包武器等非主武器槽位的执行不上报
        if (!isLocal || item == null || slot is < 0 or > 1)
        {
            orig(self!, weapon);
            return;
        }

        var cycle = weapon!.get_cycle();

        orig(self!, weapon);

        // CommandId 和 epoch 在 orig 之后才确定
        var sendEpoch = this.session.ClientWorld?.Epoch ?? -1;
        var commandId = sendEpoch >= 0
            ? this.sender.NextCommandId()
            : 0;

        this.SendCommand(
            hero!, HeroActionKind.Weapon, slot,
            new HeroIntent { Cycle = cycle },
            sendEpoch, commandId);
    }

    public void OnShieldChargeStart(
        Hook_BaseShield.orig_onShieldChargeStart orig, BaseShield self)
    {
        orig(self);
        if (!IsLocalShield(self))
        {
            return;
        }

        if (!this.dedupe.TryBeginHold(self))
        {
            return;
        }

        this.SendShieldCommand(self, ShieldIntentKind.HoldStart);
    }

    public void OnShieldReleased(
        Hook_BaseShield.orig_onShieldReleased orig, BaseShield self)
    {
        orig(self);
        if (!IsLocalShield(self))
        {
            return;
        }

        this.dedupe.EndHold(self);
        this.SendShieldCommand(self, ShieldIntentKind.HoldEnd);
    }

    public void OnStartParry(
        Hook_BaseShield.orig_startParry orig, BaseShield self)
    {
        orig(self);
        if (!IsLocalShield(self))
        {
            return;
        }

        if (!this.dedupe.TryBeginParry(self))
        {
            return;
        }

        this.SendShieldCommand(self, ShieldIntentKind.ParryStart);
    }

    public void OnShieldEndParry(
        Hook_BaseShield.orig_onShieldEndParry orig, BaseShield self)
    {
        orig(self);
        if (!IsLocalShield(self))
        {
            return;
        }

        if (!this.dedupe.TryEndParry(self))
        {
            return;
        }

        this.SendShieldCommand(self, ShieldIntentKind.ParryEnd);
    }

    /// <summary>
    /// DEBUG:直接向服务端上报指定技能的释放
    /// </summary>
    internal void DebugCastSkill(string itemId, int slot)
    {
        var hero = GuestClientSession.Game?.hero;
        var world = this.session.ClientWorld;
        if (hero == null || world == null || world.Epoch < 0)
        {
            return;
        }

        this.SendCommand(
            hero, HeroActionKind.ActiveSkill, slot,
            new HeroIntent { Ratio = 1.0 });

        Logger.Information(
            "Debug skill cast sent: item={Item} slot={Slot}", itemId, slot);
    }

    /// <summary>
    /// DEBUG:直接向服务端上报指定武器的使用
    /// </summary>
    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
    {
        var hero = GuestClientSession.Game?.hero;
        var world = this.session.ClientWorld;
        if (hero == null || world == null || world.Epoch < 0)
        {
            return;
        }

        this.SendCommand(
            hero, HeroActionKind.Weapon, slot,
            new HeroIntent { Cycle = cycle });

        Logger.Information(
            "Debug weapon use sent: item={Item} slot={Slot} cycle={Cycle}",
            itemId, slot, cycle);
    }

    private static bool IsLocalShield(BaseShield? shield)
    {
        if (shield == null || shield.destroyed)
        {
            return false;
        }

        var hero = GuestClientSession.Game?.hero;
        if (hero == null || !ReferenceEquals(shield.owner, hero))
        {
            return false;
        }

        return shield.item != null && shield.item.posID is >= 0 and <= 1;
    }

    private void SendShieldCommand(
        BaseShield shield,
        ShieldIntentKind kind)
    {
        var hero = GuestClientSession.Game?.hero;
        var item = shield.item;
        if (hero == null || item == null)
        {
            return;
        }

        var slot = item.posID;
        if (slot is < 0 or > 1)
        {
            return;
        }

        this.SendCommand(
            hero, HeroActionKind.Shield, slot,
            new HeroIntent
            {
                Ratio = 1.0,
                ShieldKind = kind,
            });
    }

    private void SendCommand(
        Hero hero,
        HeroActionKind kind,
        int slot,
        HeroIntent intent,
        int? epoch = null,
        long? commandId = null)
    {
        var world = this.session.ClientWorld;
        if (world == null)
        {
            return;
        }

        var currentEpoch = epoch ?? world.Epoch;
        if (currentEpoch < 0)
        {
            return;
        }

        var command = new HeroCommand
        {
            Epoch = currentEpoch,
            CommandId = commandId ?? this.sender.NextCommandId(),
            Kind = kind,
            Slot = slot,
            Intent = intent,
            ClientTimeMs = this.session.SyncedTimeMs,
            LevelId = hero._level?.map?.id?.ToString() ?? string.Empty,
            SubLevelIndex = hero._level?.GetSubLevelIndex() ?? -1,
        };

        this.sender.Send(command);
    }
}
