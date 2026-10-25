using System;
using System.Collections.Generic;
using dc;
using dc.hl.types;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX.Remote
{
    /// <summary>
    /// 客户端动态 LevelAreaAffect 快照镜像
    /// </summary>
    internal sealed class LevelAreaAffectStateManager
    {
        private const int MaxEntries = 512;
        private const double AreaFps = 60.0;

        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "LevelAreaAffectStateManager");

        private readonly GuestClientSession session;

        /// <summary>
        /// 服务端快照接管的区域
        /// </summary>
        private readonly Dictionary<string, LevelAreaAffect> owned = [];

        private bool applyingServer;
        private int lastEpoch = int.MinValue;

        public LevelAreaAffectStateManager(GuestClientSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// 当前是否正在应用服务端快照
        /// </summary>
        public bool IsApplyingServer => applyingServer;

        /// <summary>
        /// 应用世界帧中的服务端区域快照
        /// </summary>
        public void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;
            if (!double.IsFinite(frame.ServerTimeMs)) return;

            if (frame.Epoch != lastEpoch)
            {
                lastEpoch = frame.Epoch;
                owned.Clear();
            }

            var states = frame.AreaAffects;
            if (states == null || states.Count == 0)
            {
                RemoveAllOwned();
                return;
            }

            var game = session.Game;
            if (game == null) return;

            double nowSec = session.SyncedTimeMs / 1000.0;
            HashSet<string> serverKeys = [];
            int count = System.Math.Min(states.Count, MaxEntries);

            for (int i = 0; i < count; i++)
            {
                var state = states[i];
                if (state == null) continue;

                var level = LevelUtils.ResolveLevel(
                    game, state.LevelId, state.SubLevelIndex);
                if (level == null) continue;

                double age = nowSec - state.ServerTimeSec;
                if (age < 0.0) age = 0.0;
                double remainingSec = state.RemainingSec - age;
                if (remainingSec <= 0.0) continue;

                string key = Key(level, state.Cx, state.Cy, state.A);
                serverKeys.Add(key);

                owned.TryGetValue(key, out var affect);
                if (affect != null
                    && (affect.level == null
                        || !ReferenceEquals(affect.level, level)
                        || affect.frames <= 0.0))
                {
                    owned.Remove(key);
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

                ApplyState(affect, level, state, remainingSec);
                owned[key] = affect;
            }

            RemoveMissingOwned(serverKeys);
        }

        /// <summary>
        /// 清理全部记录 用于 epoch关卡切换
        /// </summary>
        public void Reset()
        {
            owned.Clear();
        }

        private void ApplyState(
            LevelAreaAffect affect,
            Level level,
            LevelAreaAffectState state,
            double remainingSec)
        {
            applyingServer = true;
            try
            {
                bool sameArea = ReferenceEquals(affect.level, level)
                    && affect.cx == state.Cx
                    && affect.cy == state.Cy
                    && affect.a == state.A
                    && affect.frames > 0.0;

                if (sameArea)
                {
                    affect.frames = remainingSec * AreaFps;
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
                applyingServer = false;
            }
        }

        private void RemoveMissingOwned(HashSet<string> serverKeys)
        {
            List<string>? stale = null;

            foreach (var key in owned.Keys)
            {
                if (serverKeys.Contains(key)) continue;
                (stale ??= []).Add(key);
            }

            if (stale == null) return;

            foreach (var key in stale)
            {
                if (owned.Remove(key, out var affect))
                    Clear(affect);
            }
        }

        private void RemoveAllOwned()
        {
            foreach (var affect in owned.Values)
                Clear(affect);

            owned.Clear();
        }

        private static void Clear(LevelAreaAffect affect)
        {
            try
            {
                if (affect.level != null)
                    affect.setDurationS(0.0);
            }
            catch
            {
                // 区域所在 Level 已经卸载时忽略
            }
        }

        private static LevelAreaAffect? FindDead(Level level)
        {
            var list = level.areaAffects;
            if (list == null) return null;

            for (int i = 0; i < list.length; i++)
            {
                var affect = AffectCodec.ToObject<LevelAreaAffect>(list.getDyn(i));
                if (affect == null || affect!.frames > 0.0) continue;
                if (!ReferenceEquals(affect!.level, level)) continue;

                return affect;
            }

            return null;
        }

        private static ArrayObj BuildAffixes(List<string>? affixes)
        {
            var result = (ArrayObj)ArrayUtils.CreateDyn().array;
            if (affixes == null) return result;

            foreach (var affix in affixes)
            {
                if (!string.IsNullOrEmpty(affix))
                    result.push(affix.AsHaxeString());
            }

            return result;
        }

        private static string Key(Level level, int cx, int cy, int a)
            => $"{level.map?.id}#{level.GetSubLevelIndex()}|{cx}|{cy}|{a}";
    }
}
