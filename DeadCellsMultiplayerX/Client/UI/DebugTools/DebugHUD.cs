namespace DeadCellsMultiplayerX.Client.UI.DebugTools;

using dc;
using dc.h2d;
using dc.haxe.ds;
using dc.hl.types;
using dc.level;
using dc.libs.heaps;
using dc.light;
using dc.pr;
using dc.tool;
using dc.ui;
using dc.ui.debug;
using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Client.UI.DebugTools.Stats;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore.Events;
using ModCore.Utilities;

/// <summary>
/// 左上角联机状态面板
/// </summary>
public partial class DebugHUD : IEventReceiver, IClientDebugHookSink, IDisposable
{
    private const long STATS_REFRESH_INTERVAL_MS = 200;

    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext("SourceContext", "DebugHUD");

    private static DebugHUD? instance;

    private HookGroup? hookGroup;
    private bool? lastNetstatVisible;

    private readonly List<MetricBinding> metrics = [];

    private dc.h2d.Text headerText = null!;
    private dc.h2d.Text identityText = null!;
    private Flow metricsFlow = null!;
    private string headerCached = string.Empty;
    private string identityCached = string.Empty;

    private long lastStatsRefreshMs;

    private const int METRIC_UPPER = 1_000_000_000;

    private int metricGreen;
    private int metricRed;
    private int expectedEntityLimit = -1;

    /// <summary>
    /// 每帧会波动的数值指标
    /// </summary>
    private enum MetricKind
    {
        Epoch,
        Sequence,
        Hz,
        Age,
        FrameBytes,
        Pending,
        Gap,
        Nack,
        Resync,
        Skip,
        Drop,
        Overflow,
        Rtt,
        Offset,
        Mobs,
        Ghosts,
        Heroes,
        Entities,
        Pred,
        Spawn,
    }

    private sealed record MetricBinding(
        MetricKind Kind, DebugThresholdValueText Widget, IntMap Map);

    public DebugHud Debug { get; set; } = null!;

    public int TextColor { get; set; } = 0xFFFFFF;

    public static void EnsureCreated()
    {
        if (instance != null)
        {
            return;
        }

        Logger.Information("DebugHUD hooks created");
        instance = new DebugHUD();
    }

    public static void EnsureNative() => instance?.EnsureNativeInternal();

    private void EnsureNativeInternal()
    {
        if (this.Debug != null && !this.Debug.destroyed)
        {
            return;
        }

        this.Debug = new DebugHud();
        Logger.Information("DebugHud instance created");
    }

    public DebugHUD()
    {
        this.hookGroup = new HookGroup();
        this.hookGroup.Add(ClientDebugHookBinding.Install(this));
    }

    /// <summary>
    /// 释放当前调试 HUD；会话结束时调用。
    /// </summary>
    public static void DisposeInstance() => instance?.Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        this.hookGroup?.Dispose();
        this.hookGroup = null;

        if (ReferenceEquals(instance, this))
        {
            instance = null;
        }

        GC.SuppressFinalize(this);
    }


    private static void Hook_Console_ctor(HashlinkClosure orig, HashlinkObject self)
    {
        orig.DynamicInvoke(self);
        var s = self.AsHaxe<dc.ui.Console>();
        var ss = dc.ui.Console.Class;
        ss.HIDE_UI = "FDMM_HIDE_UI".AsHaxeString();
        ss.HIDE_DEBUG = "FDMM_HIDE_DEBUG".AsHaxeString();
        ss.HIDE_CONSOLE = "FDMM_HIDE_CONSOLE".AsHaxeString();
        s.activateDebug();
    }

    private static void Hook_LevelDisp_init(Hook__LevelDisp.orig___constructor__ orig, LevelDisp arg1, Level p, LevelMap map, ArrayObj parallaxInfo)
    {
        orig(arg1, p, map, parallaxInfo);
        arg1.debug = new Graphics(null);
        p.scroller.addChildAt(arg1.debug, Const.Class.DP_DEBUG);
    }

    private void HookGameOnDispose(Hook_Game.orig_onDispose orig, Game self)
    {
        orig(self);

        if (this.Debug != null && !this.Debug.destroyed)
        {
            this.Debug.destroy();
        }
    }

    private void Hook_DebugHud_initlogui(
        Hook_DebugHud.orig_initLogsUi orig, DebugHud self)
    {
        orig(self);
        self.logsFlow?.remove();
        self.logsFlow = null;

        this.metrics.Clear();
        this.headerCached = string.Empty;
        this.identityCached = string.Empty;
        this.expectedEntityLimit = -1;

        var flow = new Flow(self.root);
        flow.set_minHeight(50);
        flow.set_paddingLeft(10);
        flow.set_paddingRight(10);
        flow.set_paddingTop(5);
        flow.set_paddingBottom(5);
        flow.set_verticalSpacing(2);
        flow.set_horizontalAlign(new FlowAlign.Left());
        flow.set_verticalAlign(new FlowAlign.Middle());
        flow.isVertical = true;
        flow.set_backgroundTile(Tile.Class.fromColor(0, null, null, 0.4, null));
        flow.set_visible(false);

        self.logsFlow = flow;

        self.logsTexts = new EnumValueMap();

        this.headerText = new dc.h2d.Text(Assets.Class.font18, flow);
        this.headerText.set_textColor(this.TextColor);
        this.headerText.scaleX = 0.7;
        this.headerText.scaleY = 0.7;

        this.identityText = new dc.h2d.Text(Assets.Class.font18, flow);
        this.identityText.set_textColor(this.TextColor);
        this.identityText.scaleX = 0.7;
        this.identityText.scaleY = 0.7;

        this.metricsFlow = new Flow(flow)
        {
            isVertical = true
        };
        this.metricsFlow.set_verticalSpacing(1);
        this.metricsFlow.set_horizontalAlign(new FlowAlign.Left());
        this.metricsFlow.set_visible(false);

        this.BuildMetrics(this.metricsFlow);

        self.onResize();

        Logger.Information(
            "Net stats panel built: metrics={MetricCount}", this.metrics.Count);
    }

    /// <summary>
    /// 创建动态数值行；同一行内每个数字按阈值独立着色
    /// </summary>
    private void BuildMetrics(Flow parent)
    {
        var green = DebugHud.Class.colorGreen;
        var yellow = DebugHud.Class.colorYellow;
        var red = DebugHud.Class.colorRed;
        var neutral = DebugThresholdValueText.Class.DEFAULT_COLOR;
        const int upper = METRIC_UPPER;

        this.metricGreen = green;
        this.metricRed = red;

        // 时钟
        var clockRow = NewMetricRow(parent);
        Add(clockRow, MetricKind.Rtt, "rtt=", "ms", Thresholds((1, red), (50, green), (150, yellow), (300, red)));
        Add(clockRow, MetricKind.Offset, "offset=", "ms", Thresholds((upper, neutral)));

        // 世界帧
        var worldRow = NewMetricRow(parent);
        Add(worldRow, MetricKind.Epoch, "epoch=", "", Thresholds((upper, neutral)));
        Add(worldRow, MetricKind.Sequence, "sequence=", "", Thresholds((upper, neutral)));
        Add(worldRow, MetricKind.Hz, "frameHz=", "", Thresholds((10, red), (15, yellow), (upper, green)));
        Add(worldRow, MetricKind.Age, "frameAge=", "ms", Thresholds((200, green), (500, yellow), (1000, red)));
        Add(worldRow, MetricKind.FrameBytes, "frameBytes=", "B", Thresholds((upper, neutral)));

        // 帧缺口与异常
        var frameRow = NewMetricRow(parent);
        Add(frameRow, MetricKind.Pending, "pending=", "", Thresholds((1, green), (5, yellow), (upper, red)));
        Add(frameRow, MetricKind.Gap, "gap=", "", Thresholds((1, green), (upper, yellow)));
        Add(frameRow, MetricKind.Nack, "nack=", "", Thresholds((1, green), (upper, yellow)));
        Add(frameRow, MetricKind.Resync, "resync=", "", Thresholds((1, green), (upper, yellow)));
        Add(frameRow, MetricKind.Skip, "skipped=", "", Thresholds((1, green), (upper, yellow)));
        Add(frameRow, MetricKind.Drop, "dropped=", "", Thresholds((1, green), (upper, yellow)));
        Add(frameRow, MetricKind.Overflow, "overflow=", "", Thresholds((1, green), (upper, red)));

        // 实体
        var entityRow = NewMetricRow(parent);
        Add(entityRow, MetricKind.Mobs, "mobs=", "", Thresholds((upper, neutral)));
        Add(entityRow, MetricKind.Ghosts, "ghosts=", "", Thresholds((upper, neutral)));
        Add(entityRow, MetricKind.Heroes, "heroes=", "", Thresholds((upper, neutral)));
        Add(entityRow, MetricKind.Entities, "entities=", "", Thresholds((upper, neutral)));
        Add(entityRow, MetricKind.Spawn, "pendingSpawn=", "", Thresholds((1, green), (upper, yellow)));
        Add(entityRow, MetricKind.Pred, "predicted=", "", Thresholds((1, green), (upper, yellow)));

        void Add(Flow row, MetricKind kind, string prefix, string suffix, IntMap map)
        {
            var widget = new DebugThresholdValueText(
                Assets.Class.font12,
                map,
                prefix.AsHaxeString(),
                suffix.AsHaxeString(),
                row);
            widget.prefixText.set_textColor(12306395);

            this.metrics.Add(new MetricBinding(kind, widget, map));
        }
    }

    private static Flow NewMetricRow(Flow parent)
    {
        var row = new Flow(parent)
        {
            isVertical = false
        };
        row.set_horizontalSpacing(12);
        row.set_verticalAlign(new FlowAlign.Middle());
        return row;
    }

    private static IntMap Thresholds(params (int upper, int Color)[] entries)
    {
        var map = new IntMap();
        foreach (var (upper, color) in entries)
        {
            map.set(upper, color);
        }

        return map;
    }

    private void Hook_DebugHud_initialize(
        Hook__DebugHud.orig___constructor__ orig, DebugHud arg1)
    {
        orig(arg1);
        arg1.root.set_visible(true);
    }

    private void Hook_DebugHud_postUpdate(
        Hook_DebugHud.orig_postUpdate orig, DebugHud self)
    {
        this.UpdateNetStats(self);

        if (!self.root.visible || Game.Class.ME == null || Game.Class.ME.curLevel == null)
        {
            return;
        }

        Level currentLevel = Game.Class.ME.curLevel;

        virtual_cx_cy_gx_gy_sx_sy_ mouseData = currentLevel.getMouse();
        if (mouseData != null)
        {
            LevelMap levelMap = currentLevel.map;
            Room currentRoom = levelMap.getRoomAt(mouseData.cx, mouseData.cy);

            if (currentRoom != null)
            {
                var roomX = mouseData.cx - currentRoom.x;
                var roomY = mouseData.cy - currentRoom.y;
                self.mouse.set_text($"pixel={mouseData.sx}, {mouseData.sy} / case={mouseData.cx}, {mouseData.cy} / room={roomX}, {roomY}".AsHaxeString());
            }
        }

        var fxAlloc = currentLevel.fx.pool.nalloc;

        var activeCritters = currentLevel.countActiveCritters();
        var totalCritters = currentLevel.critters.length;

        var splattersCount = currentLevel.splatters.length;

        self.statsText.set_text($"fx={fxAlloc} critters={activeCritters}/{totalCritters} splatters={splattersCount}".AsHaxeString());

        var visibleLights = PointLight.Class.nvisible;
        var updatedLights = PointLight.Class.nupdated;
        self.lights.set_text($"lights: nvisible={visibleLights} nupdated={updatedLights}".AsHaxeString());

        var mobsLeft = currentLevel.get_mobsLeftCount();
        var totalMobs = currentLevel.get_totalMobCount();
        self.mobs.set_text($"Mobs count={mobsLeft}/{totalMobs} (exclude malaise spawned)".AsHaxeString());

        var averageFPS = self.fpsAverage.getAverage();
        self.fpsValueText.updateValue(averageFPS);

        var drawCalls = Boot.Class.ME.engine.drawCalls;
        self.drawCallsValueText.updateValue(drawCalls);

        var visibleObjects = ObjectHelper.Class.getVisibleObjectsCount(self.graphicsObjects);
        var totalObjects = self.graphicsObjects.length;
        var culledObjects = totalObjects - visibleObjects;
        self.objectVisibilityText.set_text($"Object visible: {visibleObjects} Object culled: {culledObjects}".AsHaxeString());

        var tileCount = self.getTileGroupTileCount();
        self.tileGroupCountText.set_text($"TileGroup count: {tileCount}".AsHaxeString());

        var textureCount = MemoryManagerExtender.Class.getTexturesCount(Boot.Class.ME.engine.mem);
        self.textureCountText.updateValue(textureCount);

        var textureMemoryKB = (int)(MemoryManagerExtender.Class.getTextureMemoryUsed(Boot.Class.ME.engine.mem) / 1000.0);
        self.textureMemText.updateValue(textureMemoryKB);
    }

    /// <summary>
    /// 刷新左上角状态面板
    /// </summary>
    private void UpdateNetStats(DebugHud self)
    {
        if (self.logsFlow == null)
        {
            return;
        }

        var show = DebugFlags.Get("netstat");
        self.logsFlow.set_visible(show);

        if (this.lastNetstatVisible != show)
        {
            this.lastNetstatVisible = show;
            Logger.Information("Net stats visible: {Visible}", show);
        }

        if (!show)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - this.lastStatsRefreshMs < STATS_REFRESH_INTERVAL_MS)
        {
            return;
        }

        this.lastStatsRefreshMs = now;

        var session = ClientRuntime.Instance.CurrentGuestClient?.Session;
        var snapshot = ClientNetStats.Collect(session);

        SetText(this.headerText, snapshot.Header, ref this.headerCached);
        SetText(this.identityText, snapshot.Identity, ref this.identityCached);

        this.metricsFlow.set_visible(snapshot.HasWorld);
        if (!snapshot.HasWorld)
        {
            return;
        }

        this.UpdateEntityLimits(snapshot.Players);

        foreach (var binding in this.metrics)
        {
            binding.Widget.updateValue(GetMetricValue(snapshot, binding.Kind));
        }
    }

    /// <summary>
    /// 以房间人数为上限更新 ghosts / heroes 的阈值
    /// </summary>
    private void UpdateEntityLimits(int players)
    {
        var expected = System.Math.Max(players - 1, 0);
        if (expected == this.expectedEntityLimit)
        {
            return;
        }

        this.expectedEntityLimit = expected;

        this.ApplyEntityLimit(MetricKind.Ghosts, expected);
        this.ApplyEntityLimit(MetricKind.Heroes, expected);
    }

    private void ApplyEntityLimit(MetricKind kind, int expected)
    {
        foreach (var binding in this.metrics)
        {
            if (binding.Kind != kind)
            {
                continue;
            }

            binding.Map.clear();
            binding.Map.set(expected + 1, this.metricGreen);
            binding.Map.set(METRIC_UPPER, this.metricRed);

            binding.Widget.updateColor();
        }
    }

    private static void SetText(
        dc.h2d.Text text, string value, ref string cache)
    {
        if (cache == value)
        {
            return;
        }

        cache = value;
        text.set_text(value.AsHaxeString());
    }

    private static int GetMetricValue(
        in ClientNetSnapshot snapshot, MetricKind kind) => kind switch
        {
            MetricKind.Epoch => ClampInt(snapshot.Epoch),
            MetricKind.Sequence => ClampInt(snapshot.Sequence),
            MetricKind.Hz => snapshot.Hz,
            MetricKind.Age => snapshot.AgeMs < 0 ? 0 : ClampInt(snapshot.AgeMs),
            MetricKind.FrameBytes => snapshot.FrameBytes,
            MetricKind.Pending => snapshot.Pending,
            MetricKind.Gap => snapshot.Gap,
            MetricKind.Nack => snapshot.Nack,
            MetricKind.Resync => snapshot.Resync,
            MetricKind.Skip => snapshot.Skip,
            MetricKind.Drop => snapshot.Drop,
            MetricKind.Overflow => snapshot.Overflow,
            MetricKind.Rtt => snapshot.RttMs < 0 ? 0 : ClampInt(snapshot.RttMs),
            MetricKind.Offset => ClampSignedInt(snapshot.OffsetMs),
            MetricKind.Mobs => snapshot.Mobs,
            MetricKind.Ghosts => snapshot.Ghosts,
            MetricKind.Heroes => snapshot.Heroes,
            MetricKind.Entities => snapshot.Entities,
            MetricKind.Pred => snapshot.Pred,
            MetricKind.Spawn => snapshot.PendingSpawns,
            _ => 0,
        };

    private static int ClampInt(long value)
        => value > int.MaxValue ? int.MaxValue : (int)System.Math.Max(value, 0);

    private static int ClampSignedInt(long value)
        => value > int.MaxValue ? int.MaxValue
         : value < int.MinValue ? int.MinValue
         : (int)value;
}
