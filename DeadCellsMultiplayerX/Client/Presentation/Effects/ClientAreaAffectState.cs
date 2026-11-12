namespace DeadCellsMultiplayerX.Client.Presentation.Effects;

using dc.hl.types;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// 客户端动态 LevelAreaAffect 快照镜像
/// </summary>
internal sealed class ClientAreaAffectState(GuestClientSession session)
{
    private const int MAX_ENTRIES = 512;
    private const double AREA_FPS = 60.0;

    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ClientAreaAffectState");

    private readonly GuestClientSession session = session;

    /// <summary>
    /// 服务端快照接管的区域
    /// </summary>
    private readonly Dictionary<string, LevelAreaAffect> owned = [];
    private int lastEpoch = int.MinValue;

    /// <summary>
    /// 当前是否正在应用服务端快照
    /// </summary>
    public bool IsApplyingServer { get; private set; }

    /// <summary>
    /// 应用世界帧中的服务端区域快照
    /// </summary>
    public void ApplyFrame(WorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        if (!double.IsFinite(frame.ServerTimeMs))
        {
            return;
        }

        if (frame.Epoch != this.lastEpoch)
        {
            this.lastEpoch = frame.Epoch;
            this.RemoveAllOwned();
        }

        var states = frame.AreaAffects;
        if (states == null || states.Count == 0)
        {
            this.RemoveAllOwned();
            return;
        }

        var game = GuestClientSession.Game;
        if (game == null)
        {
            return;
        }

        var nowSec = this.session.SyncedTimeMs / 1000.0;
        HashSet<string> serverKeys = [];
        var count = Math.Min(states.Count, MAX_ENTRIES);

        for (var i = 0; i < count; i++)
        {
            var state = states[i];
            if (state == null)
            {
                continue;
            }

            var level = LevelUtils.ResolveLevel(
                game, state.LevelId, state.SubLevelIndex);
            if (level == null)
            {
                continue;
            }

            var age = nowSec - state.ServerTimeSec;
            if (age < 0.0)
            {
                age = 0.0;
            }

            var remainingSec = state.RemainingSec - age;
            if (remainingSec <= 0.0)
            {
                continue;
            }

            var key = Key(level, state.Cx, state.Cy, state.A);
            serverKeys.Add(key);

            this.owned.TryGetValue(key, out var affect);
            if (affect != null
                && (affect.level == null
                    || !ReferenceEquals(affect.level, level)
                    || affect.frames <= 0.0))
            {
                this.owned.Remove(key);
                affect = null;
            }

            if (affect == null)
            {
                affect = FindDead(level);

                if (affect == null)
                {
                    affect = new LevelAreaAffect(level);
                    level.areaAffects?.push(affect);
                }
            }

            this.ApplyState(affect, level, state, remainingSec);
            this.owned[key] = affect;
        }

        this.RemoveMissingOwned(serverKeys);
    }

    /// <summary>
    /// 清理全部记录 用于 epoch关卡切换
    /// </summary>
    public void Reset() => this.RemoveAllOwned();

    private void ApplyState(
        LevelAreaAffect affect,
        Level level,
        LevelAreaAffectState state,
        double remainingSec)
    {
        this.IsApplyingServer = true;
        try
        {
            var sameArea = ReferenceEquals(affect.level, level)
                && affect.cx == state.Cx
                && affect.cy == state.Cy
                && affect.a == state.A
                && affect.frames > 0.0;

            if (sameArea)
            {
                affect.frames = remainingSec * AREA_FPS;
                affect.aDurationS = state.ADurationS;
                affect.aValue = state.AValue;
                return;
            }

            affect.init(
                state.Cx, state.Cy, remainingSec, state.A,
                state.ADurationS, state.AValue, BuildAffixes(state.Affixes));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex,
                "Apply LevelAreaAffect failed: level={Level} cx={Cx} cy={Cy} a={A}",
                state.LevelId, state.Cx, state.Cy, state.A);
        }
        finally
        {
            this.IsApplyingServer = false;
        }
    }

    private void RemoveMissingOwned(HashSet<string> serverKeys)
    {
        List<string>? stale = null;

        foreach (var key in this.owned.Keys)
        {
            if (serverKeys.Contains(key))
            {
                continue;
            }

            (stale ??= []).Add(key);
        }

        if (stale == null)
        {
            return;
        }

        foreach (var key in stale)
        {
            if (this.owned.Remove(key, out var affect))
            {
                Clear(affect);
            }
        }
    }

    private void RemoveAllOwned()
    {
        foreach (var affect in this.owned.Values)
        {
            Clear(affect);
        }

        this.owned.Clear();
    }

    private static void Clear(LevelAreaAffect affect)
    {
        try
        {
            if (affect.level != null)
            {
                affect.setDurationS(0.0);
            }
        }
        catch
        {
            // 区域所在 Level 已经卸载时忽略
        }
    }

    private static LevelAreaAffect? FindDead(Level level)
    {
        var list = level.areaAffects;
        if (list == null)
        {
            return null;
        }

        for (var i = 0; i < list.length; i++)
        {
            var affect = AffectCodec.ToObject<LevelAreaAffect>(list.getDyn(i));
            if (affect == null || affect!.frames > 0.0)
            {
                continue;
            }

            if (!ReferenceEquals(affect!.level, level))
            {
                continue;
            }

            return affect;
        }

        return null;
    }

    private static ArrayObj BuildAffixes(List<string>? affixes)
    {
        var result = (ArrayObj)ArrayUtils.CreateDyn().array;
        if (affixes == null)
        {
            return result;
        }

        foreach (var affix in affixes)
        {
            if (!string.IsNullOrEmpty(affix))
            {
                result.push(affix.AsHaxeString());
            }
        }

        return result;
    }

    private static string Key(Level level, int cx, int cy, int a)
        => $"{LevelUtils.KeyOf(level)}|{cx}|{cy}|{a}";
}
