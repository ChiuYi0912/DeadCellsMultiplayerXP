namespace DeadCellsMultiplayerX.Contracts.Entities;

/// <summary>
/// 通用复制实体类型
/// 只允许在末尾追加，禁止重排或复用旧值
/// </summary>
public enum EntityKind : byte
{
    Unknown = 0,
    Mob = 1,
    Pet = 2,
    Boss = 3,
    Loot = 4,
    Interactive = 5,
    Door = 6,
}
