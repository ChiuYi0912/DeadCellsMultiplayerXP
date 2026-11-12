namespace DeadCellsMultiplayerX;

using System.Runtime.ExceptionServices;
using dc;
using DeadCellsMultiplayerX.Composition;
using DeadCellsMultiplayerX.Tools;
using ModCore.Events.Interfaces;
using ModCore.Mods;
using ModCore.Modules;
using ModCore.Utilities;

internal sealed class ModEntry(ModInfo info) : ModBase(info), IOnAfterLoadingAssets
{
    public static ModEntry Instance { get; private set; } = null!;
    /// <summary>
    /// 初始化
    /// </summary>
    public override void Initialize()
    {
        Instance = this;

#if DEBUG
        DebugLauncher.Init();
#endif

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DCCM_MULTIPLAYER_NO_CLIENT")))
        {
            return;
        }

        Environment.SetEnvironmentVariable("DCCM_MULTIPLAYER_NO_CLIENT", "1");

        ClientCompositionRoot.Start();

        GetText.Instance.RegisterMod("DeadCellsMultiplayerX");

        //可以捕获到奇怪的报错
#if DEBUG
        Hook_Boot.mainLoop += this.Hook_Boot_mainLoop;
        TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;
#endif
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) => this.Logger.Fatal(e.Exception, "TaskScheduler_UnobservedTaskException.");

    private void Hook_Boot_mainLoop(Hook_Boot.orig_mainLoop orig, Boot self)
    {
        try
        {
            orig(self);
        }
        catch (Exception ex)
        {
            this.Logger.Error("{ex}", ex);
            //System.Diagnostics.Debugger.Break();
            ExceptionDispatchInfo.Capture(ex).Throw();
            throw;
        }
    }

    void IOnAfterLoadingAssets.OnAfterLoadingAssets()
    {
        var res = this.Info.ModRoot.GetFilePath("res.pak");
        FsPak.Instance.FileSystem.loadPak(res.AsHaxeString());

#if DEBUG
        // Phase 0: 游戏内完整协议基线。设置 DCMX_PROTOCOL_BASELINE_OUTPUT 后生成并退出。
        if (ProtocolBaseline.TryRunFromEnvironment())
        {
            Environment.Exit(0);
        }
#endif
    }
}
