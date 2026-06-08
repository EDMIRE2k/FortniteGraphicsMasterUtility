using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace FortniteCinematicSettings.Services;

public static class MicaService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaMicaEffect = 1029;

    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private const int WmDwmCompositionChanged = 0x031E;
    private const int WmDwmColorizationColorChanged = 0x0320;

    public static bool IsTransparencyEnabled()
    {
        try
        {
            object? value = Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                ?.GetValue("EnableTransparency");
            return value is not int intValue || intValue != 0;
        }
        catch
        {
            return true;
        }
    }

    public static void Attach(Window window, Action systemVisualsChanged)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source)
        {
            return;
        }

        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
        {
            if (message is WmSettingChange or WmThemeChanged or WmDwmCompositionChanged or WmDwmColorizationColorChanged)
            {
                window.Dispatcher.BeginInvoke(systemVisualsChanged);
            }

            return nint.Zero;
        });
    }

    public static bool Apply(Window window, bool darkMode)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero)
        {
            return false;
        }

        int dark = darkMode ? 1 : 0;
        int rounded = 2;
        int mica = 2;
        int enabled = 1;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };

        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref rounded, sizeof(int));
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        int result = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref mica, sizeof(int));
        if (result != 0)
        {
            result = DwmSetWindowAttribute(hwnd, DwmwaMicaEffect, ref enabled, sizeof(int));
        }

        window.Background = Brushes.Transparent;
        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        return result == 0 && IsTransparencyEnabled();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
}
