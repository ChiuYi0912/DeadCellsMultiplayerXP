namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;

using dc.pr;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Server.World.Actors;

/// <summary>
/// 一次动作命令从校验到执行所需的不可变上下文。
/// </summary>
internal sealed class HeroCommandContext(
    string senderGuid,
    HeroCommand command,
    long serverTimeMs)
{
    public string SenderGuid { get; } = senderGuid;

    public HeroCommand Command { get; } = command;

    public long ServerTimeMs { get; } = serverTimeMs;

    public Game? Game { get; set; }

    public HeroDynamic? Snapshot { get; set; }

    public Level? Level { get; set; }

    public string LevelId { get; set; } = string.Empty;

    public int SubLevelIndex { get; set; }

    public ServerHero? Hero { get; set; }

    public ServerHeroCombatProxy? Ghost => this.Hero?.Ghost;

    public double Ratio { get; set; } = 1.0;
}
