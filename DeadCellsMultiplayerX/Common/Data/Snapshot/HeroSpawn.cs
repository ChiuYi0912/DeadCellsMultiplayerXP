using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 初始化hero
    /// </summary>
    [MessagePackObject]
    public class HeroSpawn
    {   
        /// <summary>
        /// 客户id
        /// </summary>
        [Key(0)] public string GUID { get; set; } = string.Empty;

        /// <summary>
        /// 记录所操控的实体,用于后续变身模式
        /// </summary>
        [Key(1)] public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 皮肤贴图名
        /// </summary>
        [Key(2)] public string ColorMapModel { get; set; } = string.Empty;

        /// <summary>
        /// 色带图
        /// </summary>
        [Key(3)] public string ColorMapSkin { get; set; } = string.Empty;

        /// <summary>
        /// 当前所在的子关卡id
        /// </summary>
        [Key(4)] public int SubLevelId { get; set; } = -1;

        /// <summary>
        /// 贴图状态
        /// </summary>
        [Key(5)] public SpriteInfo MainSprite { get; set; } = new();

        /// <summary>
        /// 皮肤发光数据
        /// </summary>
        [Key(6)] public Dictionary<int, byte[]> GlowData { get; set; } = [];

        /// <summary>
        /// 当前的关卡id
        /// </summary>
        [Key(7)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 服务端分配,静态数据变化时递增
        /// </summary>
        [Key(8)] public uint Version { get; set; }

        /// <summary>
        /// 当前项链
        /// </summary>
        [Key(9)] public ItemRef? Talisman { get; set; }


        /// <summary>
        /// 判断静态部分是否变化，用于决定是否重发 Spawned
        /// </summary>
        public bool SameAs(HeroSpawn o)
        {
            if (o == null) return false;

            return GUID == o.GUID
                && TypeName == o.TypeName
                && ColorMapModel == o.ColorMapModel
                && ColorMapSkin == o.ColorMapSkin
                && SubLevelId == o.SubLevelId
                && SpriteSame(MainSprite, o.MainSprite)
                && GlowSame(GlowData, o.GlowData)
                && ItemRefSame(Talisman, o.Talisman);
        }

        private static bool ItemRefSame(ItemRef? a, ItemRef? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return a.Kind == b.Kind
                && a.ItemId == b.ItemId
                && a.ItemLevel == b.ItemLevel
                && a.PosID == b.PosID
                && a.PermanentId == b.PermanentId
                && a.Ammo == b.Ammo
                && ListSame(a.Affixes, b.Affixes)
                && ListSame(a.InactiveAffixes, b.InactiveAffixes);
        }

        private static bool ListSame(
            List<string>? a, List<string>? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return a.SequenceEqual(b);
        }

        private static bool SpriteSame(SpriteInfo a, SpriteInfo b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return a.AtlasName == b.AtlasName
                && a.GroupName == b.GroupName
                && ByteEq(a.PivotData, b.PivotData);
        }

        private static bool GlowSame(Dictionary<int, byte[]> a, Dictionary<int, byte[]> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var (k, va) in a)
            {
                if (!b.TryGetValue(k, out var vb)) return false;
                if (!ByteEq(va, vb)) return false;
            }
            return true;
        }

        private static bool ByteEq(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;

            return true;
        }
    }
}