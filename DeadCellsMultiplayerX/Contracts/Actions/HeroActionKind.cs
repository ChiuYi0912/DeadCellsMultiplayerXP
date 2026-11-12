namespace DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 由客户端上报、服务端执行并广播的英雄动作类型
/// </summary>
public enum HeroActionKind
{
    ActiveSkill = 0,
    Weapon = 1,
    Talisman = 2,
    Shield = 3,
}
