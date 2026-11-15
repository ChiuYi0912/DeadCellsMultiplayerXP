namespace DeadCellsMultiplayerX.Engine.Native;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
[SuppressMessage(
    "Interoperability",
    "SYSLIB1054",
    Justification = "Win32 interop is small and explicit.")]
internal static class WinLogFont
{
    private const string TextProperty = "_text";
    private const uint WM_SETFONT = 0x0030;

    private static IntPtr font;

    public static int Height { get; private set; } = -15;


    public static void HlUIsetFont(IntPtr windowRef)
    {
        if (windowRef == IntPtr.Zero || IntPtr.Size != 8)
        {
            return;
        }

        var hwnd = Marshal.ReadIntPtr(windowRef, IntPtr.Size);
        var richEdit = GetPropW(hwnd, TextProperty);
        var currentFont = GetFont();
        if (richEdit == IntPtr.Zero || currentFont == IntPtr.Zero)
        {
            return;
        }

        _ = SendMessageW(richEdit, WM_SETFONT, currentFont, 1);
    }

    private static IntPtr GetFont()
    {
        if (font == IntPtr.Zero)
        {
            font = CreateFontW(
                Height,
                0,
                0,
                0,
                400,  // FW_NORMAL
                0,
                0,
                0,
                1,    // DEFAULT_CHARSET
                0,
                0,
                5,    // CLEARTYPE_QUALITY
                0,
                "Segoe UI");
        }

        return font;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetPropW(IntPtr hWnd, string lpString);


    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint charSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);
}
