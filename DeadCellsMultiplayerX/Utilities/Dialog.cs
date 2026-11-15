
namespace DeadCellsMultiplayerX.Tools;

using dc.hl;
using DeadCellsMultiplayerX.Engine.Native;
using HashlinkNET.Native.Impl;
using HaxeProxy.Runtime;
using ModCore.Utilities;

[Flags]
internal enum DialogFlags
{
    /// <summary>
    /// 普通对话框
    /// </summary>
    Default = 0,

    /// <summary>
    /// 确认对话框
    /// </summary>
    YesNo = 1,

    /// <summary>
    /// 错误样式对话框
    /// </summary>
    Error = 2,

    /// <summary>
    /// 带 Yes / No 选择的错误对话框
    /// </summary>
    YesNoError = YesNo | Error,
}

internal static class Dialog
{
    public static void Show(string title, string text, DialogFlags flags = DialogFlags.Default)
        => UI.Class.dialog.Invoke(title.AsHaxeString(), text.AsHaxeString(), (int)flags);

    public static void ShowRaw(string title, string text, int flags)
        => UI.Class.dialog.Invoke(title.AsHaxeString(), text.AsHaxeString(), flags);

    public static void ReportError(string message, string title = "Uncaught Exception")
    {
        var winLog = new WinLog(title.AsHaxeString(), 500, 400);
        winLog.setTextContent(message.AsHaxeString(), Ref<bool>.In(false));


        var dismissButton = new Button(winLog, "Feedback bug".AsHaxeString())
        {
            onClick = new(() => Lib_std.sys_exit.Invoke(0))
        };

        var continueButton = new Button(winLog, "Exit".AsHaxeString())
        {
            onClick = new(() => Lib_std.sys_exit.Invoke(0))
        };

        WinLogFont.HlUIsetFont(winLog.h);

        while (Lib_ui.ui_loop.Invoke(true) != 2)
        {
            dc.hxd.System.Class.timeoutTick();
        }

        winLog.destroy();
    }
}