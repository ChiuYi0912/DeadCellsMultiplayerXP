namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using MessagePack;

[MessagePackObject]
public class EntityInfo
{
    [Key(0)] public string? TypeName { get; set; } = "";
    [Key(1)] public string GUID { get; set; } = Guid.NewGuid().ToString();
    [Key(2)] public double RemoteTime { get; set; }
    [Key(3)] public double LocalTime { get; set; }


    [Key(4)] public string? ColorMapModel { get; set; }
    [Key(5)] public string? ColorMapSkin { get; set; }


    [Key(6)] public int SubLevelId { get; set; }

    [Key(7)] public PosVector PosVector { get; set; } = new();
    [Key(8)] public Dictionary<int, byte[]> GlowData { get; set; } = [];
    [Key(9)] public SpriteInfo? MainSprite { get; set; }
    [Key(10)] public AnimInfo AnimInfo { get; set; } = new();
    [Key(11)] public byte CollisionMode { get; set; } = 0;
    [Key(12)] public List<AffectEntry> HeroEffectList { get; set; } = [];

}
