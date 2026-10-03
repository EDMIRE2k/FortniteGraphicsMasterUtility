using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FortniteCinematicSettings;
using FortniteCinematicSettings.Models;
using FortniteCinematicSettings.Services;

internal static class Program
{
    private static int _assertions;
    [STAThread]
    private static void Main(string[] args)
    {
        string temp = Path.Combine(Path.GetTempPath(), "FGMU-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            TestSettings(temp);
            if (args.Contains("--ui")) TestUi(temp, args.Last());
            Console.WriteLine($"PASS: {_assertions} assertions. All tests used temporary fixtures.");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(temp, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(temp, true);
        }
    }

    private static string Fixture()
    {
        var text = new StringBuilder("; Keep this comment\r\n");
        text.AppendLine(SettingsCatalog.MainSection);
        text.AppendLine("bUseNanite=True\r\nbRayTracing=True\r\nUnrelatedOption=keep");
        foreach (var s in SettingsCatalog.CreateQualitySettings().Where(s => s.Section == SettingsCatalog.MainSection))
            text.AppendLine($"{s.Key}=3");
        text.AppendLine(SettingsCatalog.ScalabilitySection);
        foreach (var s in SettingsCatalog.CreateQualitySettings().Where(s => s.Section == SettingsCatalog.ScalabilitySection))
            text.AppendLine($"{s.Key}=3");
        text.AppendLine("[D3DRHIPreference]\r\nPreferredRHI=dx12\r\nPreferredFeatureLevel=sm6");
        text.AppendLine("[Unrelated]\r\nPreferredRHI=untouched\r\nsg.TextureQuality=99");
        return text.ToString();
    }

    private static void TestSettings(string temp)
    {
        var ini = new IniSettingsService();
        string path = Path.Combine(temp, "GameUserSettings.ini");
        File.WriteAllText(path, Fixture(), new UTF8Encoding(true));
        byte[] original = File.ReadAllBytes(path);
        Check(ini.ReadRenderingMode(path) == RenderingMode.DirectX12, "DX12 detection");
        Check(ini.ReadKnownValues(path, SettingsCatalog.CreateQualitySettings())["sg.TextureQuality"] == "3", "Section-aware reads");
        var performance = ini.CreatePlan(ini.ReadValues(path), [], true, true, RenderingMode.Performance, false);
        Check(performance.Count == 3, "Renderer-only change has exactly feature level and two flags");
        string backup = ini.ApplyChanges(path, performance, true, ini.Fingerprint(path));
        Check(ini.ReadRenderingMode(path) == RenderingMode.Performance, "Performance uses dx12/es31");
        Check(File.ReadAllBytes(backup).SequenceEqual(original), "Backup is byte-exact");
        Check(ini.IsReadOnly(path), "Read-only set");
        Check(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "UTF8 BOM preserved");
        Check(File.ReadAllText(path).Contains("[Unrelated]\r\nPreferredRHI=untouched"), "Unrelated section preserved");
        Check(!File.ReadAllText(path).Replace("\r\n", "").Contains('\n'), "CRLF preserved");
        bool rejected = false;
        try { ini.ApplyLumenFix(path, false); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Lumen rejected in Performance");

        var high = SettingsCatalog.CreateQualitySettings();
        var allPerf = ini.CreatePlan(ini.ReadValues(path), high, true, true, RenderingMode.Performance, false);
        Check(!allPerf.Any(c => c.Key.Contains("GlobalIllumination") || c.Key.Contains("Reflection")), "Performance excludes Lumen quality");
        var dx12 = ini.CreatePlan(ini.ReadValues(path), [], true, true, RenderingMode.DirectX12, false);
        ini.ApplyChanges(path, dx12, false);
        Check(ini.ReadRenderingMode(path) == RenderingMode.DirectX12, "Return to full DX12");
        Check(!ini.IsReadOnly(path), "Read-only removed");

        foreach (var setting in high) setting.Value = 5;
        var lumen = ini.CreatePlan(ini.ReadValues(path), high, false, true, RenderingMode.Performance, true);
        Check(lumen.All(c => c.Key is "DesiredGlobalIlluminationQuality" or "PreNaniteGlobalIlluminationQuality" or "bRayTracing" or "sg.GlobalIlluminationQuality"), "Lumen excludes renderer and presets");
        Check(lumen.All(c => c.Key == "bRayTracing" || c.After == "3"), "Lumen tier 3");

        string fingerprint = ini.Fingerprint(path);
        File.AppendAllText(path, "; external change\r\n");
        rejected = false;
        try { ini.ApplyChanges(path, dx12, true, fingerprint); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "External modifications rejected");
        Check(File.ReadAllText(path).EndsWith("; external change\r\n"), "External edit not overwritten");

        File.WriteAllText(path, Fixture(), new UnicodeEncoding(false, true));
        byte[] unicodeOriginal = File.ReadAllBytes(path);
        ini.ApplyChanges(path, performance, true);
        Check(File.ReadAllBytes(path).Take(2).SequenceEqual(new byte[] { 255, 254 }), "UTF16 preserved");
        ini.RestoreLatestBackup(path);
        Check(File.ReadAllBytes(path).SequenceEqual(unicodeOriginal), "Restore is byte-exact");
        Check(!ini.IsReadOnly(path), "Restore unlocks");

        File.WriteAllText(path, "[D3DRHIPreference]\nPreferredRHI=dx11\nPreferredFeatureLevel=sm5\nPreferredFeatureLevel=sm5\n[Other]\nValue=keep", new UTF8Encoding(false));
        ini.ApplyChanges(path, ini.CreatePlan(ini.ReadValues(path), [], false, false, RenderingMode.Performance, false), false);
        Check(!File.ReadAllText(path).Contains("sm5"), "Duplicate target keys updated");
        Check(!File.ReadAllText(path).EndsWith('\n'), "Missing terminal newline preserved");
        Check(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "No BOM added");

        File.WriteAllText(path, "[Other]\nValue=keep\n");
        ini.ApplyChanges(path, ini.CreatePlan(ini.ReadValues(path), [], false, false, RenderingMode.DirectX12, false), false);
        Check(ini.ReadRenderingMode(path) == RenderingMode.DirectX12, "Missing renderer section inserted");
        Check(File.ReadAllText(path).Contains("Value=keep"), "Insertion preserves other sections");
        var presets = SettingsCatalog.CreatePresets();
        Check(presets.Single(p => p.Name == "Photography").Values.Values.All(v => v == 5), "Photography remains maximum");
        Check(presets.Single(p => p.Name == "Default cinematic").Values["sg.TextureQuality"] == 3, "Default cinematic preserved");
        var zero = new QualitySetting("x", "x", "", "", 0);
        Check(zero.Options.Any(o => o.Value == 0), "Current zero values are representable");
        Check(ini.FindBackups(path).Count >= 5, "Backups accumulate");
        Check(!ini.IsFortniteRunning() || System.Diagnostics.Process.GetProcesses().Any(p => p.ProcessName.StartsWith("FortniteClient") || p.ProcessName == "FortniteGame"), "App is not detected as game");
    }

    private static void TestUi(string temp, string output)
    {
        Directory.CreateDirectory(output);
        string path = Path.Combine(temp, "UI.ini");
        File.WriteAllText(path, Fixture().Replace("sg.TextureQuality=3", "sg.TextureQuality=0"));
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow(path, false)
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = 0
        };
        app.MainWindow = window;
        window.Show();
        Pump();
        T Get<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        Check(!Get<Button>("ApplyButton").IsEnabled, "Opening app does not stage changes or coerce zero");
        var theme = Get<ComboBox>("ThemeBox");

        foreach (int mode in new[] { 2, 1 })
        {
            theme.SelectedIndex = mode;
            foreach (var size in new[] { (1180d, 860d), (880d, 580d), (760d, 500d) })
            {
                window.MinWidth = Math.Min(window.MinWidth, size.Item1);
                window.MinHeight = Math.Min(window.MinHeight, size.Item2);
                window.Width = size.Item1; window.Height = size.Item2; Pump();
                Get<RadioButton>("GraphicsNav").IsChecked = true;
                Get<ScrollViewer>("GraphicsPage").ScrollToTop(); Pump();
                CheckFooter(window);
                Capture(window, Path.Combine(output, $"{(mode == 2 ? "dark" : "light")}-{size.Item1}.png"), mode == 2);
                Get<ComboBox>("PresetBox").IsDropDownOpen = true; Pump();
                var popup = (Popup)Get<ComboBox>("PresetBox").Template.FindName("PART_Popup", Get<ComboBox>("PresetBox"));
                Check(popup?.IsOpen == true && popup.Child.RenderSize.Height > 100, "Fluent preset dropdown opens");
                if (size.Item1 == 1180 && popup?.Child is FrameworkElement menu)
                {
                    var bitmap = new RenderTargetBitmap((int)menu.ActualWidth, (int)menu.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(menu);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, mode == 2 ? "dropdown-dark.png" : "dropdown-light.png"));
                    encoder.Save(file);
                }
                Get<ComboBox>("PresetBox").IsDropDownOpen = false;
                Get<ScrollViewer>("GraphicsPage").ScrollToEnd(); Pump();
                CheckFooter(window);
                foreach (string nav in new[] { "ChangesNav", "RecoveryNav" })
                {
                    Get<RadioButton>(nav).IsChecked = true; Pump(); CheckFooter(window);
                }
            }
        }

        Get<RadioButton>("GraphicsNav").IsChecked = true;
        Get<RadioButton>("PerformanceChoice").IsChecked = true; Pump();
        Check(!Get<CheckBox>("NaniteCheck").IsEnabled && !Get<CheckBox>("RayTracingCheck").IsEnabled, "DX12-only controls disabled in Performance");
        Check(Get<CheckBox>("NaniteCheck").IsChecked == false && Get<CheckBox>("RayTracingCheck").IsChecked == false, "Performance flags shown as off");
        Check(Get<TextBox>("PreviewBox").Text.Contains("PreferredFeatureLevel=es31"), "Preview includes Performance renderer");
        Get<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(new IniSettingsService().ReadRenderingMode(path) == RenderingMode.Performance, "UI applies Performance");
        Check(new IniSettingsService().ReadKnownValues(path, SettingsCatalog.CreateQualitySettings())["sg.TextureQuality"] == "0", "Mode-only UI apply preserves zero quality");
        Get<CheckBox>("LumenFixCheck").IsChecked = true; Pump();
        Check(Get<CheckBox>("LumenFixCheck").IsChecked == false, "UI rejects Lumen in Performance");
        Get<RadioButton>("Dx12Choice").IsChecked = true;
        Get<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(new IniSettingsService().ReadRenderingMode(path) == RenderingMode.DirectX12, "UI applies DX12");
        Get<CheckBox>("LumenFixCheck").IsChecked = true; Pump();
        Check(!Get<ComboBox>("PresetBox").IsEnabled && !Get<RadioButton>("PerformanceChoice").IsEnabled, "Lumen is exclusive");
        Check(!Get<TextBox>("PreviewBox").Text.Contains("D3DRHIPreference"), "Lumen preview excludes renderer");
        Get<CheckBox>("LumenFixCheck").IsChecked = false;
        Get<Button>("DiscardButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();

        Get<TextBox>("SearchBox").Text = "no-such-setting";
        Check(Get<TextBlock>("NoResultsText").Visibility == Visibility.Visible, "Search empty state");
        Get<TextBox>("SearchBox").Text = "";
        window.Width = 1180; window.Height = 860; theme.SelectedIndex = 2;
        Get<ComboBox>("PresetBox").SelectedIndex = 5; // Default cinematic.
        Get<RadioButton>("ChangesNav").IsChecked = true; Pump();
        Capture(window, Path.Combine(output, "changes-dark.png"), true);
        Get<Button>("DiscardButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Get<RadioButton>("RecoveryNav").IsChecked = true; Pump();
        Capture(window, Path.Combine(output, "recovery-dark.png"), true);
        window.Width = 1320; window.Height = 900;
        Get<Grid>("Shell").LayoutTransform = new ScaleTransform(1.5, 1.5);
        Get<RadioButton>("GraphicsNav").IsChecked = true; Pump();
        CheckFooter(window);
        Capture(window, Path.Combine(output, "scaled-150-percent.png"), true);
        Get<Grid>("Shell").LayoutTransform = Transform.Identity;
        window.Close();
        var missing = new MainWindow(Path.Combine(temp, "missing.ini"), false)
        { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000 };
        missing.Show(); Pump();
        Check(!((Button)missing.FindName("ApplyButton")).IsEnabled, "Missing file cannot apply");
        Check(!((Button)missing.FindName("UndoButton")).IsEnabled, "Missing backup cannot restore");
        missing.Close();
        app.Shutdown();
    }

    private static void CheckFooter(MainWindow window)
    {
        var shell = (FrameworkElement)window.FindName("Shell");
        var footer = (FrameworkElement)window.FindName("Footer");
        var apply = (FrameworkElement)window.FindName("ApplyButton");
        var warning = (FrameworkElement)window.FindName("WarningText");
        Rect Bounds(FrameworkElement item) => item.TransformToAncestor(shell).TransformBounds(new Rect(item.RenderSize));
        Rect footerBounds = Bounds(footer), applyBounds = Bounds(apply), warningBounds = Bounds(warning);
        Check(footerBounds.Bottom <= shell.ActualHeight + 1, "Footer within window");
        Check(footerBounds.Contains(applyBounds), "Apply wholly visible");
        Check(warningBounds.Bottom + 8 <= applyBounds.Top, "Warning separated from actions");
        var discard = (FrameworkElement)window.FindName("DiscardButton");
        Check(Bounds(discard).Right + 8 <= applyBounds.Left, "Footer buttons have spacing");
    }

    private static void Capture(MainWindow window, string path, bool dark)
    {
        var shell = (FrameworkElement)window.FindName("Shell");
        var grid = (Grid)shell;
        var previous = grid.Background;
        grid.Background = new SolidColorBrush(dark ? Color.FromRgb(32, 32, 32) : Color.FromRgb(243, 243, 243));
        Pump();
        var size = shell.LayoutTransform.TransformBounds(new Rect(shell.RenderSize)).Size;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(shell);
        grid.Background = previous;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        _assertions++;
    }
}
