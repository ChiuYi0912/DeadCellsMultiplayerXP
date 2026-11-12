namespace DeadCellsMultiplayerX.Server.Replication.Areas;

using dc.hl.types;
using dc.tool;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;

/// <summary>
/// 服务端动态 LevelAreaAffect 登记与快照采集
/// </summary>
internal sealed class ServerLevelAreaAffectRegistry
{
    private const int MAX_ENTRIES = 512;

    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerLevelAreaAffectRegistry");

    private readonly HashSet<LevelAreaAffect> registered = [];

    /// <summary>
    /// 登记一个运行时创建的动态区域
    /// </summary>
    public void Register(LevelAreaAffect? affect)
    {
        if (affect == null)
        {
            return;
        }

        this.registered.Add(affect);

        if (this.registered.Count <= 4096)
        {
            return;
        }

        this.CollectDead();
        Logger.Warning(
            "LevelAreaAffect registry overflow: count={Count}", this.registered.Count);
    }

    /// <summary>
    /// 采集当前仍然存活的动态区域快照
    /// </summary>
    public List<LevelAreaAffectState> Collect(long serverTimeMs)
    {
        var result = new List<LevelAreaAffectState>();
        if (this.registered.Count == 0)
        {
            return result;
        }

        var serverSec = serverTimeMs / 1000.0;
        HashSet<string> seen = [];
        List<LevelAreaAffect>? stale = null;

        foreach (var affect in this.registered)
        {
            if (affect == null || affect.level == null || affect.frames <= 0.0)
            {
                (stale ??= []).Add(affect!);
                continue;
            }

            if (result.Count >= MAX_ENTRIES)
            {
                break;
            }

            var levelId = affect.level.map?.id?.ToString() ?? string.Empty;
            var subLevelIndex = affect.level.GetSubLevelIndex();
            var key = $"{LevelUtils.KeyOf(affect.level)}|{affect.cx}|{affect.cy}|{affect.a}";

            if (!seen.Add(key))
            {
                continue;
            }

            result.Add(new LevelAreaAffectState
            {
                LevelId = levelId,
                SubLevelIndex = subLevelIndex,
                Cx = affect.cx,
                Cy = affect.cy,
                A = affect.a,
                ADurationS = affect.aDurationS,
                AValue = affect.aValue,
                RemainingSec = affect.frames / 60.0,
                ServerTimeSec = serverSec,
                Affixes = CollectAffixes(affect.affixes),
            });
        }

        if (stale != null)
        {
            foreach (var affect in stale)
            {
                this.registered.Remove(affect);
            }
        }

        return result;
    }

    /// <summary>
    /// 清除全部登记 用于 epoch关卡切换
    /// </summary>
    public void Clear() => this.registered.Clear();

    private void CollectDead()
    {
        List<LevelAreaAffect>? stale = null;

        foreach (var affect in this.registered)
        {
            if (affect != null && affect.level != null && affect.frames > 0.0)
            {
                continue;
            }

            (stale ??= []).Add(affect!);
        }

        if (stale == null)
        {
            return;
        }

        foreach (var affect in stale)
        {
            this.registered.Remove(affect);
        }
    }

    private static List<string> CollectAffixes(ArrayObj? affixes)
    {
        var result = new List<string>();
        if (affixes == null)
        {
            return result;
        }

        for (var i = 0; i < affixes.length; i++)
        {
            string? value = affixes.getDyn(i)?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }

        return result;
    }
}
