using System.Runtime.CompilerServices;
using dc;
using dc.en;
using dc.hl.types;
using DeadCellsMultiplayerX.Common.Data;
using Hashlink.Proxy;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Serializers
{
    public static class AffectCodec
    {
        private const int MaxOuterIndex = 256;

        private static readonly ConditionalWeakTable<Entity, Dictionary<AffectKey, double>> startCache = new();

        public static List<AffectEntry> Collect(Entity hero, double snapSec, double baseFps)
        {
            var result = new List<AffectEntry>();
            var affects = hero.affects;
            if (affects == null) return result;

            double fps = baseFps > 0.0 && double.IsFinite(baseFps) ? baseFps : 60.0;

            if (!startCache.TryGetValue(hero, out var t0map))
            {
                t0map = new Dictionary<AffectKey, double>();
                startCache.Add(hero, t0map);
            }

            var seen = new HashSet<AffectKey>();

            for (int i = 0; i < affects.length; i++)
            {
                // 数组元素是 HashlinkSharp 的原始 HashlinkObject 代理 需要转成强类型代理
                var inner = ToObject<ArrayObj>(affects.array[i]);

                if (inner == null) continue;

                for (int j = 0; j < inner.length; j++)
                {
                    var v = ToVirtual<virtual_a_t_uniqId_val_>(inner.array[j]);

                    if (v == null) continue;

                    var key = new AffectKey(v.a, v.uniqId, v.val);
                    seen.Add(key);

                    if (!t0map.TryGetValue(key, out double t0))
                    {
                        t0 = snapSec;
                        t0map[key] = t0;
                    }

                    double remainingSec = v.t / fps;

                    result.Add(new AffectEntry
                    {
                        OuterIndex = i,
                        InnerIndex = j,
                        A = v.a,
                        T = v.t,
                        UniqId = v.uniqId,
                        Val = v.val,
                        StartSec = t0,
                        EndSec = snapSec + remainingSec,
                        RemainingSec = remainingSec,
                        ServerTimeSec = snapSec,
                    });
                }
            }

            // 清理
            var stale = new List<AffectKey>();
            foreach (var k in t0map.Keys)
                if (!seen.Contains(k)) stale.Add(k);
            foreach (var k in stale) t0map.Remove(k);

            return result;
        }


        /// <summary>
        /// 把数组中的原始 HashlinkObject 转成强类型对象代理
        /// </summary>
        public static T? ToObject<T>(object? value) where T : HaxeObject
        {
            if (value is T typed) return typed;

            if (value is HashlinkObj obj)
            {
                try
                {
                    return obj.AsHaxe<T>();
                }
                catch (Exception ex) when (
                    ex is InvalidCastException or NotSupportedException or InvalidOperationException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// 把数组中的原始 HashlinkObject 转成强类型 Virtual 代理
        /// </summary>
        public static T? ToVirtual<T>(object? value) where T : HaxeVirtual
        {
            if (value is T typed) return typed;

            if (value is HashlinkObj obj)
            {
                try
                {
                    return obj.AsHaxe().ToVirtual<T>();
                }
                catch (Exception ex) when (
                    ex is InvalidCastException or NotSupportedException or InvalidOperationException)
                {
                    return null;
                }
            }

            return null;
        }

        public static void Apply(
            Entity hero,
            List<AffectEntry> entries,
            double nowSec,
            double baseFps = 60.0)
        {
            if (entries == null || entries.Count == 0) return;
            if (!double.IsFinite(nowSec)) return;

            var affects = hero.affects;
            if (affects == null) return;

            double fps = baseFps > 0.0 && double.IsFinite(baseFps) ? baseFps : 60.0;

            // 注入条目的 uniqId 也要占用本地序列 否则后续 setAffectS 会撞号
            int nextUniqId = hero.lastUniqId;

            foreach (var e in entries)
            {
                if (e.OuterIndex < 0 || e.OuterIndex >= MaxOuterIndex)
                    continue;

                if (e.UniqId >= nextUniqId)
                    nextUniqId = e.UniqId == int.MaxValue ? int.MaxValue : e.UniqId + 1;

                if (nowSec < e.StartSec)
                    continue;

                // 用服务端采集时的剩余秒数 + 快照年龄计算当前剩余
                // 不能直接写 e.T 否则每帧/每次重放都会把计时器拉回采集时的值
                double remainingSec;
                if (e.ServerTimeSec > 0.0)
                {
                    double age = nowSec - e.ServerTimeSec;
                    if (age < 0.0) age = 0.0;
                    remainingSec = e.RemainingSec - age;
                }
                else
                {
                    remainingSec = e.EndSec - nowSec;
                }

                if (remainingSec <= 0.0) continue;
                double remainingFrames = remainingSec * fps;

                while (affects.length <= e.OuterIndex)
                    affects.push((ArrayObj)ArrayUtils.CreateDyn().array);

                var inner = ToObject<ArrayObj>(affects.array[e.OuterIndex]);
                if (inner == null)
                {
                    inner = (ArrayObj)ArrayUtils.CreateDyn().array;
                    affects.array[e.OuterIndex] = inner;
                }
                else
                {
                    inner = CompactNulls(affects, e.OuterIndex, inner);
                }

                var value = new virtual_a_t_uniqId_val_
                {
                    a = e.A,
                    t = remainingFrames,
                    uniqId = e.UniqId,
                    val = e.Val
                };

                // 不能按 InnerIndex 用 null 占位 游戏 preUpdate 会直接解引用 .t
                int slot = FindSlot(inner, e);
                if (slot >= 0)
                    inner.array[slot] = value;
                else
                    inner.push(value);
            }

            if (nextUniqId > hero.lastUniqId)
                hero.lastUniqId = nextUniqId;
        }

        /// <summary>
        /// 清空指定 outer index 的 affect 列表
        /// </summary>
        public static void ClearIndex(Entity entity, int outerIndex)
        {
            var affects = entity?.affects;
            if (affects == null) return;
            if (outerIndex < 0 || outerIndex >= affects.length) return;

            affects.array[outerIndex] = (ArrayObj)ArrayUtils.CreateDyn().array;
        }

        /// <summary>
        /// 移除指定 effect 类型的本地条目 服务端快照已经包含这些类型
        /// </summary>
        /// <param name="excludeIndexes">服务端快照接管的索引 不参与清理</param>
        public static void RemoveByAffectTypes(
            Entity entity,
            HashSet<int> types,
            HashSet<int>? excludeIndexes = null)
        {
            var affects = entity?.affects;
            if (affects == null || types == null || types.Count == 0) return;

            for (int i = 0; i < affects.length; i++)
            {
                if (excludeIndexes != null && excludeIndexes.Contains(i)) continue;

                var inner = ToObject<ArrayObj>(affects.array[i]);
                if (inner == null || inner.length == 0) continue;

                var rebuilt = (ArrayObj)ArrayUtils.CreateDyn().array;
                bool changed = false;

                for (int j = 0; j < inner.length; j++)
                {
                    var v = ToVirtual<virtual_a_t_uniqId_val_>(inner.array[j]);
                    if (v == null)
                    {
                        changed = true;
                        continue;
                    }

                    if (types.Contains(v.a))
                    {
                        changed = true;
                        continue;
                    }

                    rebuilt.push(v);
                }

                if (changed)
                    affects.array[i] = rebuilt;
            }
        }

        /// <summary>
        /// 读取本地指定 affect 的剩余秒数 不存在返回 0
        /// </summary>
        public static double GetRemainingSec(
            Entity entity, AffectEntry entry, double baseFps)
        {
            var affects = entity?.affects;
            if (affects == null) return 0.0;

            int outerIndex = entry.OuterIndex;
            if (outerIndex < 0 || outerIndex >= affects.length) return 0.0;

            var inner = ToObject<ArrayObj>(affects.array[outerIndex]);
            if (inner == null) return 0.0;

            double fps = baseFps > 0.0 && double.IsFinite(baseFps) ? baseFps : 60.0;

            for (int j = 0; j < inner.length; j++)
            {
                var v = ToVirtual<virtual_a_t_uniqId_val_>(inner.array[j]);
                if (v == null) continue;

                if (v.a == entry.A
                    && v.uniqId == entry.UniqId
                    && v.val == entry.Val)
                    return v.t / fps;
            }

            return 0.0;
        }

        /// <summary>
        /// 清理旧版本用 null 占位留下的空洞
        /// </summary>
        private static ArrayObj CompactNulls(ArrayObj affects, int outerIndex, ArrayObj inner)
        {
            bool hasNull = false;
            for (int i = 0; i < inner.length; i++)
            {
                if (ToVirtual<virtual_a_t_uniqId_val_>(inner.array[i]) == null)
                {
                    hasNull = true;
                    break;
                }
            }

            if (!hasNull) return inner;

            var rebuilt = (ArrayObj)ArrayUtils.CreateDyn().array;
            for (int i = 0; i < inner.length; i++)
            {
                var v = ToVirtual<virtual_a_t_uniqId_val_>(inner.array[i]);
                if (v != null) rebuilt.push(v);
            }

            affects.array[outerIndex] = rebuilt;
            return rebuilt;
        }

        private static int FindSlot(ArrayObj inner, AffectEntry e)
        {
            for (int i = 0; i < inner.length; i++)
            {
                var v = ToVirtual<virtual_a_t_uniqId_val_>(inner.array[i]);
                if (v != null
                    && v.a == e.A
                    && v.uniqId == e.UniqId
                    && v.val == e.Val)
                    return i;
            }

            return -1;
        }
    }
}