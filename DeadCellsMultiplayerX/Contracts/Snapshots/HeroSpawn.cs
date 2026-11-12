namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Loadout;
using MessagePack;

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
    /// 当前项链（旧字段，保留用于兼容过渡；新逻辑使用 <see cref="Loadout"/>）
    /// </summary>
    [Key(9)] public ItemRef? Talisman { get; set; }

    /// <summary>
    /// 完整装备快照；晚加入/重连依赖它恢复武器、技能与项链。
    /// </summary>
    [Key(10)] public PlayerLoadout? Loadout { get; set; }


    /// <summary>
    /// 判断静态部分是否变化，用于决定是否重发 Spawned
    /// </summary>
    public bool SameAs(HeroSpawn o)
    {
        if (o == null)
        {
            return false;
        }

        return this.GUID == o.GUID
            && this.TypeName == o.TypeName
            && this.ColorMapModel == o.ColorMapModel
            && this.ColorMapSkin == o.ColorMapSkin
            && this.SubLevelId == o.SubLevelId
            && SpriteSame(this.MainSprite, o.MainSprite)
            && GlowSame(this.GlowData, o.GlowData)
            && LoadoutSame(this.Loadout, o.Loadout);
    }

    private static bool LoadoutSame(PlayerLoadout? a, PlayerLoadout? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.BrutalityTier == b.BrutalityTier
            && a.SurvivalTier == b.SurvivalTier
            && a.TacticTier == b.TacticTier
            && SlotsSame(a.Weapons, b.Weapons)
            && SlotsSame(a.Actives, b.Actives)
            && SlotSame(a.Talisman, b.Talisman);
    }

    private static bool SlotsSame(
        List<EquipmentSlot>? a,
        List<EquipmentSlot>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!SlotSame(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SlotSame(EquipmentSlot? a, EquipmentSlot? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return ItemDefSame(a.Def, b.Def)
            && RuntimeSame(a.Runtime, b.Runtime);
    }

    private static bool ItemDefSame(ItemDef? a, ItemDef? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.Kind == b.Kind
            && a.ItemId == b.ItemId
            && a.Level == b.Level
            && a.PermanentId == b.PermanentId
            && ListSame(a.Affixes, b.Affixes)
            && ListSame(a.InactiveAffixes, b.InactiveAffixes);
    }

    private static bool RuntimeSame(ItemRuntime? a, ItemRuntime? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.Ammo == b.Ammo
            && a.CooldownUntilMs == b.CooldownUntilMs;
    }

    private static bool ListSame(
        List<string>? a, List<string>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.SequenceEqual(b);
    }

    private static bool SpriteSame(SpriteInfo a, SpriteInfo b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        return a.AtlasName == b.AtlasName
            && a.GroupName == b.GroupName
            && ByteEq(a.PivotData, b.PivotData);
    }

    private static bool GlowSame(Dictionary<int, byte[]> a, Dictionary<int, byte[]> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (k, va) in a)
        {
            if (!b.TryGetValue(k, out var vb))
            {
                return false;
            }

            if (!ByteEq(va, vb))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ByteEq(byte[] a, byte[] b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }
}
