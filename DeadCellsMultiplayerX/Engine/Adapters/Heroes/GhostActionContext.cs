namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

using System.Diagnostics;
using dc.en;
using Serilog;

/// <summary>
/// 技能/武器动作模块共享的只读上下文
/// 只提供 Hero、owner 和诊断辅助
/// </summary>
internal sealed class GhostActionContext(Hero hero, string ownerGuid)
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostActions");

    /// <summary>
    /// 客户端在服务端的隐形 Hero
    /// </summary>
    public Hero Hero { get; } = hero;

    /// <summary>
    /// 客户端id
    /// </summary>
    public string OwnerGuid { get; } = ownerGuid;

    /// <summary>
    /// 当前 Level 的实体与 Power 数量
    /// </summary>
    public (int Entities, int Powers) Count()
    {
        var level = this.Hero._level;
        return (level?.entities?.length ?? 0, level?.powers?.length ?? 0);
    }

    /// <summary>
    /// DEBUG 下记录一次动作造成的实体/Power 增量
    /// </summary>
    [Conditional("DEBUG")]
    public void LogActionDelta(
        string action,
        int slot,
        (int Entities, int Powers) before)
    {
        var level = this.Hero._level;
        var entities = level?.entities?.length ?? 0;
        var powers = level?.powers?.length ?? 0;
        var levelId = level?.map?.id?.ToString() ?? string.Empty;

        Logger.Verbose(
            "GhostHero {Action}: owner={Owner} slot={Slot} level={Level} " +
            "entities {OldEntities}->{NewEntities} powers {OldPowers}->{NewPowers}",
            action, this.OwnerGuid, slot, levelId,
            before.Entities, entities, before.Powers, powers);
    }
}
