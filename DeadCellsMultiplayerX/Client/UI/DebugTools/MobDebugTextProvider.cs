namespace DeadCellsMultiplayerX.Client.UI.DebugTools;

using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.MobBehavior;

/// <summary>
/// mob 调试文本：netId、生命、elite、最近技能 id、目标玩家、事件阶段
/// </summary>
internal sealed class MobDebugTextProvider(GuestClientSession session)
    : IEntityDebugTextProvider
{
    private sealed class State
    {
        public string SkillId { get; set; } = string.Empty;
        public string TargetGuid { get; set; } = string.Empty;
        public MobModifierEventKind? Event { get; set; }
    }

    private readonly Dictionary<uint, State> states = [];

    public EntityKind Kind => EntityKind.Mob;

    public int LabelColor => 0xFFF36B;

    public void OnEvent(EntityEvent evt)
    {
        if (evt is not MobModifierEvent mobEvt)
        {
            return;
        }

        if (mobEvt.Kind == MobModifierEventKind.MobDied)
        {
            this.states.Remove(mobEvt.NetId);
            return;
        }

        if (!this.states.TryGetValue(mobEvt.NetId, out var state))
        {
            this.states[mobEvt.NetId] = state = new State();
        }

        if (!string.IsNullOrEmpty(mobEvt.SkillId))
        {
            state.SkillId = mobEvt.SkillId;
        }

        if (!string.IsNullOrEmpty(mobEvt.TargetGuid))
        {
            state.TargetGuid = mobEvt.TargetGuid;
        }

        state.Event = mobEvt.Kind;
    }

    public string? GetText(uint netId, object native, long nowMs)
    {
        if (native is not Mob mob || mob.destroyed)
        {
            return null;
        }

        this.states.TryGetValue(netId, out var state);

        var skill = string.IsNullOrEmpty(state?.SkillId)
            ? "-"
            : state.SkillId;

        var target = this.ResolveTarget(state?.TargetGuid);
        var evt = state?.Event?.ToString() ?? "-";
        var elite = mob.elite ? " elite" : string.Empty;

        return $"#{netId} s:{skill} t:{target} e:{evt}";
    }

    public void Reset() => this.states.Clear();

    private string ResolveTarget(string? guid)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return "-";
        }

        if (guid == session.Client?.Guid)
        {
            return "me";
        }

        var guests = session.Client?.LobbyInfo?.Guests;
        if (guests != null
            && guests.TryGetValue(guid, out var info)
            && !string.IsNullOrEmpty(info.Name))
        {
            return info.Name.Length > 12 ? info.Name[..12] : info.Name;
        }

        return guid.Length > 8 ? guid[..8] : guid;
    }
}
