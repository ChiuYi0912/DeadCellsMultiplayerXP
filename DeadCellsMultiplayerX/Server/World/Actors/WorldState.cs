namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 服务端 Ghost 世界
/// </summary>
internal sealed class WorldState
{
    private readonly Dictionary<string, ServerHero> actors = [with(StringComparer.Ordinal)];

    private readonly EventJournal events = new();

    private long nextOutcomeId;

    /// <summary>
    /// 当前动作 epoch；未开始时为 -1
    /// </summary>
    public int Epoch { get; private set; } = -1;

    /// <summary>
    /// 取得本 epoch 内唯一的动作结果 id。
    /// </summary>
    public long NextOutcomeId() => ++this.nextOutcomeId;

    /// <summary>
    /// 实际持有 ServerHeroCombatProxy 的数量
    /// </summary>
    public int Count => this.actors.Values.Count(actor => actor.Ghost != null);

    /// <summary>
    /// 全部客户端 Hero，供 spawn 同步遍历
    /// </summary>
    public IReadOnlyCollection<ServerHero> Actors => this.actors.Values;

    /// <summary>
    /// 按 guid 查找
    /// </summary>
    public ServerHero? Find(string guid)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return null;
        }

        return this.actors.TryGetValue(guid, out var hero) ? hero : null;
    }

    /// <summary>
    /// 按底层可命中实体反查 Hero。
    /// </summary>
    public ServerHero? FindByHero(Entity? entity)
    {
        if (entity == null)
        {
            return null;
        }

        foreach (var hero in this.actors.Values)
        {
            if (ReferenceEquals(hero.Ghost?.Hero, entity))
            {
                return hero;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取或创建客户端 Hero
    /// </summary>
    public ServerHero GetOrCreate(string guid)
    {
        if (this.actors.TryGetValue(guid, out var hero))
        {
            return hero;
        }

        hero = new ServerHero(guid);
        this.actors[guid] = hero;
        return hero;
    }

    /// <summary>
    /// 开始新 epoch
    /// </summary>
    public void BeginEpoch(int epoch)
    {
        this.Epoch = epoch;
        this.Reset();
    }

    /// <summary>
    /// 清空 ServerHeroCombatProxy 与事件
    /// </summary>
    public void Reset()
    {
        foreach (var hero in this.actors.Values)
        {
            hero.Dispose();
        }

        this.actors.Clear();
        this.events.Clear();
        this.nextOutcomeId = 0;
    }

    /// <summary>
    /// 断线清理
    /// </summary>
    public void RemoveGuest(string guid)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        if (this.actors.Remove(guid, out var hero))
        {
            hero.Dispose();
        }
    }

    /// <summary>
    /// 记录一次动作结果
    /// </summary>
    public void RecordEvent(HeroOutcome outcome)
        => this.events.Record(outcome);

    /// <summary>
    /// 读取待广播的动作结果
    /// </summary>
    public List<HeroOutcome> PeekEvents() => this.events.Peek();

    public void CommitEvents(int count) => this.events.Commit(count);
}
