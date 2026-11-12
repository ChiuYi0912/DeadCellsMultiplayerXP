namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using dc;
using dc.en;
using dc.hl.types;
using dc.libs.heaps.slib;
using dc.pr;
using DeadCellsMultiplayerX.Client.Presentation.Heroes;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Serialization;
using DeadCellsMultiplayerX.Engine.Utilities;
using Hashlink.Virtuals;
using ModCore.Storage;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// 可见的远端玩家替身。
/// 思路：只根据世界帧插值位置/动画并播放表现（含盾牌视觉）；
/// 不可见的 ClientHeroReplayProxy 才承担技能/武器回放，两者职责分离，避免把表现塞进战斗代理。
/// </summary>
internal sealed class RemoteHero : KingSkin, IHxbitSerializable<RemoteHero.IHxData>
{
    private readonly GuestClientSession session;
    private readonly WorldFrameTimeline frames;

    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "RemoteHero");

    /// <summary>
    /// 当前的动画id
    /// </summary>
    private string lastGroup = "";

    /// <summary>
    /// 最近一次从服务端动画快照应用的动画，用于盾牌表现结束后恢复
    /// </summary>
    private AnimInfo? lastAppliedAnim;

    /// <summary>
    /// 用于判断动画开始与结束时间
    /// </summary>
    private long lastAppliedStartTime = -1;

    /// <summary>
    /// 初始化后的首次更新后打印日志
    /// </summary>
    private bool loggedFirstTick;

    /// <summary>
    /// 用于切换关卡时首次打印日志
    /// </summary>
    private bool loggedLevelWait;

    /// <summary>
    /// 实体唯一id 由服务端提供
    /// </summary>
    public string Guid { get; }

    /// <summary>
    /// 当前的关卡id
    /// </summary>
    public string LevelId { get; set; } = string.Empty;

    /// <summary>
    /// 当前的关卡中进入的子关卡id
    /// </summary>
    public int SubLevelIndex { get; set; } = -1;


    /// <summary>
    /// 实体实际创建所在的关卡id,用于关卡变化时销毁重建
    /// </summary>
    public string CreatedLevelId { get; }

    /// <summary>
    /// 实体实际创建所在的子关卡id,用于关卡变化时销毁重建
    /// </summary>
    public int CreatedSubLevelIndex { get; }


    /// <summary>
    /// 该实体应当显示的目标关卡id(用于避免插值影响)
    /// </summary>
    public string TargetLevelId { get; private set; } = string.Empty;

    /// <summary>
    /// 该实体应当显示的目标子关卡id(用于避免插值影响)
    /// </summary>
    public int TargetSubLevelIndex { get; private set; } = -1;

    /// <summary>
    /// 序列化数据
    /// </summary>
    public sealed class IHxData { }

    public RemoteHero(
        GuestClientSession session,
        Level lvl,
        WorldFrameTimeline frames,
        string guid,
        int x = 0,
        int y = 0) : base(lvl, x, y)
    {
        this.session = session;
        this.frames = frames;
        this.Guid = guid;
        this.CreatedLevelId = lvl?.map?.id?.ToString() ?? string.Empty;
        this.CreatedSubLevelIndex = lvl?.GetSubLevelIndex() ?? -1;

        this.set_easeSpritePos(false);
        this.hasWineGlass = false;
    }

    /// <summary>
    /// 父类初始化,禁用影响渲染字段
    /// </summary>
    public override void init()
    {
        base.init();
        this.hasRepelling = false;
        this.hasGravity = false;
        this.gravity = 0;
        this.collisionMode = new CollisionMode.None();
        this.frict = 0;
    }

    public override void initGfx()
    {
        base.initGfx();

        //移除状态机动画
        AnimManager anim = this.spr.get_anim();
        anim.removeAllStateAnims();

        //创建光
        this.createConfLight("Hero".AsHaxeString());
    }


    /// <summary>
    /// 用于首次初始化和皮肤变化多次初始化
    /// </summary>
    /// <param name="spawn">角色出生数据。</param>
    /// <remarks>
    /// 数据类型为 <see cref="HeroSpawn"/>。
    /// 参数 <paramref name="spawn"/> 不应为空
    /// </remarks>
    public void ApplySpawn(HeroSpawn spawn)
    {
        if (this.destroyed)
        {
            return;
        }

        // 校验初始化所需的数据是否完整
        if (string.IsNullOrEmpty(spawn.ColorMapModel)
            || string.IsNullOrEmpty(spawn.ColorMapSkin)
            || spawn.MainSprite == null)
        {
            return;
        }

        SpriteLib? sprlib = Assets.Class.lib.get(
            spawn.MainSprite.AtlasName.AsHaxeString());
        if (sprlib == null)
        {
            return;
        }

        String group = spawn.MainSprite.GroupName.AsHaxeString();
        dc.h3d.mat.Texture normalMap = sprlib.getNormalMapFromGroup(group);
        this.initSprite(sprlib, group, null, null, null, true, null, normalMap);

        if (this.spr == null)
        {
            return;
        }

        //设置枢轴
        this.spr.pivot.copyFrom(
            DCMXSerializers.MessagePack.Deserialize<SpritePivot>(spawn.MainSprite.PivotData));

        //上色
        this.setColorMap(
            spawn.ColorMapModel.AsHaxeString(),
            spawn.ColorMapSkin.AsHaxeString(),
            null);

        //设置skin发光色
        foreach (var (idx, gdd) in spawn.GlowData)
        {
            if (gdd == null)
            {
                continue;
            }

            this.setGlowData(idx,
                DCMXSerializers.MessagePack.Deserialize<virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_>(gdd),
                this.spr);
        }
    }

    public override void fixedUpdate()
    {
        var nowSec = this.session.SyncedTimeMs / 1000.0;

        if (this.frames.TryGetHeroBracket(
                this.Guid, nowSec,
                out HeroDynamic prev, out HeroDynamic next,
                out var alpha, out var renderTimeSec))
        {
            // 时间线还没追上目标关卡时,保持创建时的摆放,不使用旧关卡样本
            if (prev.LevelId != this.TargetLevelId || prev.SubLevelIndex != this.TargetSubLevelIndex)
            {
                if (!this.loggedLevelWait)
                {
                    this.loggedLevelWait = true;
                    Log.Information(
                        "RemoteHero {Guid} : waiting for level Target={TargetLevelId}#{TargetSub}:  sample={SampleLevel}#{SampleSub}",
                        this.Guid, this.TargetLevelId, this.TargetSubLevelIndex,
                        prev.LevelId, prev.SubLevelIndex);
                }

                base.fixedUpdate();
                return;
            }

            if (!this.loggedFirstTick)
            {
                this.loggedFirstTick = true;
                Log.Information("RemoteHero {Guid} first tick: {Level}#{SubLevelid}", this.Guid, next.LevelId, next.SubLevelIndex);
            }

            //此方法控制实体平滑移动
            RemoteHeroMotion.Apply(this, prev.Pos, next.Pos, alpha);

            //同步状态机数组
            AffectCodec.Apply(this, prev.Affects, nowSec, this.cd?.baseFps ?? 60.0);

            this.Animate(prev.Anim, next.Anim, renderTimeSec);
        }

        base.fixedUpdate();
    }

    /// <summary>
    /// 创建后直接摆放到触发创建的状态位置,避免落到旧关卡坐标
    /// </summary>
    /// <param name="levelId">当前的关卡id</param>
    /// <param name="subLevelIndex">当前的子关卡id</param>
    /// <param name="pos">位置信息</param>
    public void SnapToState(string levelId, int subLevelIndex, PosVector pos)
    {
        this.TargetLevelId = levelId;
        this.TargetSubLevelIndex = subLevelIndex;

        if (pos != null)
        {
            RemoteHeroMotion.SnapTo(this, pos);
        }
    }

    /// <summary>
    /// 更新实体动画
    /// </summary>
    /// <param name="prevAnim">上一个动画快照</param>
    /// <param name="nextAnim">下一个动画快照</param>
    /// <param name="renderTimeSec">客户端时间,用于判断动画开始与结束时间</param>
    public void Animate(AnimInfo prevAnim, AnimInfo nextAnim, double renderTimeSec)
    {
        AnimManager? anim = this.spr?.get_anim();
        if (anim == null)
        {
            return;
        }

        var chosen = AnimStateApplier.Select(prevAnim, nextAnim, renderTimeSec);
        if (chosen == null)
        {
            return;
        }

        this.lastAppliedAnim = chosen;
        var groupName = chosen.GroupName;
        if (string.IsNullOrEmpty(groupName))
        {
            return;
        }

        // 打断翻滚
        if (IsRollRelatedGroup(groupName) && this.IsRollAct())
        {
            this.lastGroup = "";
            this.lastAppliedStartTime = -1;
            return;
        }

        if (AnimStateApplier.Play(
                anim, chosen, this.lastGroup, this.lastAppliedStartTime))
        {
            this.lastGroup = groupName;
            this.lastAppliedStartTime = chosen.StartTime;
        }
    }

    public bool ApplyShieldVisual(
        ShieldActionKind kind,
        int slot,
        ItemRef? item,
        bool fullParry,
        double elapsedMs)
    {
        if (this.destroyed || this.spr == null)
        {
            return false;
        }

        AnimManager? anim = this.spr.get_anim();
        if (anim == null || anim.destroyed)
        {
            return false;
        }

        switch (kind)
        {
            case ShieldActionKind.HoldStart:
                anim.play("block".AsHaxeString(), null, null);
                return true;

            case ShieldActionKind.HoldEnd:
                this.RestoreAnim();
                return true;

            case ShieldActionKind.ParryStart:
                anim.play("blockEnd".AsHaxeString(), null, null);
                this.PlayParryFx(withText: false);
                return true;

            case ShieldActionKind.ParryEnd:
                this.RestoreAnim();
                return true;

            case ShieldActionKind.Block:
                return true;

            case ShieldActionKind.Counter:
                anim.play("blockEnd".AsHaxeString(), null, null);
                this.PlayParryFx(withText: true);
                return true;

            default:
                return false;
        }
    }

    private void RestoreAnim()
    {
        if (this.lastAppliedAnim == null)
        {
            return;
        }

        this.lastGroup = "";
        this.lastAppliedStartTime = -1;
        this.Animate(this.lastAppliedAnim, this.lastAppliedAnim,
            this.session.SyncedTimeMs / 1000.0);
    }

    private void PlayParryFx(bool withText)
    {
        var level = this._level;
        if (level?.fx == null)
        {
            return;
        }

        if (withText)
        {
            this.popText("Parade !".AsHaxeString(), 65535);
        }

        level.fx.shieldParryStart(this, 4480637, null);
        level.fx.frontShieldHit(this, 6921449);
    }

    /// <summary>
    /// 用于判断服务端发送的实体状态中,是否存在某个id
    /// </summary>
    private bool IsAffectActive(int affectId)
    {
        var affects = this.affects;
        if (affects == null)
        {
            return false;
        }

        if (affectId < 0 || affectId >= affects.length)
        {
            return false;
        }

        var list = AffectCodec.ToObject<ArrayObj>(affects.array[affectId]);
        return list != null && list.length > 0;
    }

    /// <summary>
    /// 判断当前实体是否在做翻滚动画
    /// </summary>
    /// <param name="name"></param>
    /// <remarks>由于原始翻滚技能使用状态打断,这里必须判断,否则会出现动画快速切换(闪烁)</remarks>
    /// <returns>bool</returns>
    private static bool IsRollRelatedGroup(string name)
    {
        return name switch
        {
            "rolling" or "rollEnd" or "rollIdle" or "rollRun" => true,
            _ => false,
        };
    }

    /// <summary>
    /// 强制开启渲染
    /// </summary>
    /// <returns></returns>
    public override bool _isOnScreen() => true;

    /// <summary>
    /// 判断此玩家控制的hero是否处于翻滚状态
    /// </summary>
    /// <returns></returns>
    private bool IsRollAct() => this.IsAffectActive(3);

    IHxData IHxbitSerializable<IHxData>.GetData() => new();

    void IHxbitSerializable<IHxData>.SetData(IHxData data)
    {
    }
}
