using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FortniteCinematicSettings.Models;
using FortniteCinematicSettings.Services;
using Microsoft.Win32;

namespace FortniteCinematicSettings;

public partial class MainWindow : Window
{
    private readonly IniSettingsService _ini = new();
    private readonly ObservableCollection<QualitySetting> _settings = new(SettingsCatalog.CreateQualitySettings());
    private readonly HashSet<string> _edited = new();
    private readonly IReadOnlyList<PresetDefinition> _presets;
    private readonly ICollectionView _view;
    private readonly bool _persistPreferences;
    private readonly string _themePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGraphicsMasterUtility", "theme.txt");
    private IReadOnlyDictionary<string, string> _baseline = new Dictionary<string, string>();
    private IReadOnlyList<SettingChange> _plan = [];
    private string _configPath;
    private string _fingerprint = "";
    private RenderingMode? _savedMode;
    private bool _baselineReadOnly;
    private bool _featuresEdited;
    private bool _dx12Nanite;
    private bool _dx12RayTracing;
    private bool _ready;
    private bool _updating;

    public MainWindow() : this(null) { }

    public MainWindow(string? configPath, bool persistPreferences = true)
    {
        _configPath = configPath ?? _ini.DefaultConfigPath;
        _persistPreferences = persistPreferences;
        InitializeComponent();
        _presets = new[] { new PresetDefinition("Custom / current", "Individual values from your configuration.", false, false, new Dictionary<string, int>()) }
            .Concat(SettingsCatalog.CreatePresets()).ToArray();
        _view = CollectionViewSource.GetDefaultView(_settings);
        _view.Filter = item => item is QualitySetting setting &&
            (setting.Name.Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
             setting.Key.Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
             setting.Description.Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        SettingsList.ItemsSource = _view;
        PresetBox.ItemsSource = _presets;
        foreach (var setting in _settings)
            setting.PropertyChanged += (_, args) =>
            {
                if (!_ready || _updating || args.PropertyName != nameof(QualitySetting.Value)) return;
                _edited.Add(setting.Key);
                PresetBox.SelectedIndex = 0;
                UpdatePlan();
            };

        if (_persistPreferences)
        {
            try { if (File.Exists(_themePath) && int.TryParse(File.ReadAllText(_themePath), out int theme)) ThemeBox.SelectedIndex = Math.Clamp(theme, 0, 2); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var work = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, work.Width);
        MinHeight = Math.Min(MinHeight, work.Height);
        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
        _ready = true;
        SourceInitialized += (_, _) =>
        {
            MicaService.Attach(this, ApplyTheme);
            ApplyTheme();
        };
        SizeChanged += (_, _) => NavigationColumn.Width = new GridLength(ActualWidth < 1000 ? 196 : 226);
        Closing += OnClosing;
        ApplyTheme();
        LoadFile();
    }

    private RenderingMode? SelectedMode => PerformanceChoice.IsChecked == true ? RenderingMode.Performance :
        Dx12Choice.IsChecked == true ? RenderingMode.DirectX12 : null;
    private bool IsLumen => LumenFixCheck.IsChecked == true;
    private bool ProtectionChangedOnDisk => File.Exists(_configPath) && (ReadOnlyCheck.IsChecked == true) != _baselineReadOnly;
    private bool HasChanges => _plan.Count > 0 || ProtectionChangedOnDisk;

    private void LoadFile()
    {
        try
        {
            _updating = true;
            _baseline = _ini.ReadValues(_configPath);
            _fingerprint = _ini.Fingerprint(_configPath);
            _savedMode = _ini.ReadRenderingMode(_configPath);
            _baselineReadOnly = _ini.IsReadOnly(_configPath);
            foreach (var setting in _settings)
            {
                if (_baseline.TryGetValue(IniSettingsService.Address(setting.Section, setting.Key), out var raw) && int.TryParse(raw, out int value))
                    setting.Value = value;
                else setting.Value = 3;
            }
            bool Flag(string key) => _baseline.TryGetValue(IniSettingsService.Address(SettingsCatalog.MainSection, key), out var value)
                && value.Equals("True", StringComparison.OrdinalIgnoreCase);
            _dx12Nanite = Flag("bUseNanite");
            _dx12RayTracing = Flag("bRayTracing");
            NaniteCheck.IsChecked = _savedMode != RenderingMode.Performance && _dx12Nanite;
            RayTracingCheck.IsChecked = _savedMode != RenderingMode.Performance && _dx12RayTracing;
            ReadOnlyCheck.IsChecked = _baselineReadOnly;
            LumenFixCheck.IsChecked = false;
            Dx12Choice.IsChecked = _savedMode == RenderingMode.DirectX12;
            PerformanceChoice.IsChecked = _savedMode == RenderingMode.Performance;
            PresetBox.SelectedItem = _presets.Skip(1).FirstOrDefault(p =>
                p.Nanite == NaniteCheck.IsChecked && p.RayTracing == RayTracingCheck.IsChecked &&
                _settings.All(s => p.Values[s.Key] == s.Value)) ?? _presets[0];
            PresetDescriptionText.Text = ((PresetDefinition)PresetBox.SelectedItem).Description;
            _edited.Clear();
            _featuresEdited = false;
            ConfigPathBox.Text = _configPath;
            RefreshFileState();
            _updating = false;
            UpdateAvailability();
            UpdatePlan();
        }
        catch (Exception ex)
        {
            _updating = false;
            ApplyButton.IsEnabled = false;
            Notify(ex.Message, true);
        }
    }

    private void RefreshFileState()
    {
        bool exists = File.Exists(_configPath);
        FileStatus.Text = exists ? "Config connected" : "Config not found";
        FileStatus.Foreground = (Brush)FindResource(exists ? "SuccessBrush" : "WarningBrush");
        SavedModeText.Text = _savedMode switch
        {
            RenderingMode.Performance => "Saved: Performance",
            RenderingMode.DirectX12 => "Saved: DirectX 12",
            _ => "Saved: other / not set"
        };
        ProtectionText.Text = !exists ? "No configuration file selected." : _ini.IsReadOnly(_configPath) ? "File status: read-only" : "File status: writable";
        OpenFolderButton.IsEnabled = Directory.Exists(Path.GetDirectoryName(_configPath));
        UnlockButton.IsEnabled = exists && _ini.IsReadOnly(_configPath);
        var backups = _ini.FindBackups(_configPath);
        UndoButton.IsEnabled = backups.Count > 0;
        BackupText.Text = backups.Count == 0 ? "No backups yet." :
            $"{backups.Count} backup(s). Latest: {File.GetLastWriteTime(backups[0]):g}";
    }

    private void UpdateAvailability()
    {
        bool performance = SelectedMode == RenderingMode.Performance;
        PresetBox.IsEnabled = !IsLumen;
        Dx12Choice.IsEnabled = PerformanceChoice.IsEnabled = !IsLumen;
        NaniteCheck.IsEnabled = RayTracingCheck.IsEnabled = !IsLumen && !performance;
        SearchBox.IsEnabled = !IsLumen;
        foreach (var setting in _settings) setting.IsAvailable = !IsLumen && (!performance || !IniSettingsService.RequiresFullRenderer(setting));
        _view.Refresh();
        RendererHint.Text = IsLumen ? "Lumen Fix is exclusive. Rendering mode and quality changes are excluded." :
            performance ? "Nanite, ray tracing and Lumen controls are unavailable in Performance Mode." :
            SelectedMode is null ? "Choose a rendering mode to apply graphics changes." :
            "Full DX12 rendering with Nanite, Lumen and ray tracing options.";
        ApplyLabel.Text = IsLumen ? "Apply Lumen Fix" : "Apply settings";
    }

    private void UpdatePlan()
    {
        if (!_ready || _updating) return;
        var plan = _ini.CreatePlan(_baseline, _settings.Where(s => _edited.Contains(s.Key)),
            NaniteCheck.IsChecked == true, RayTracingCheck.IsChecked == true, SelectedMode, IsLumen);
        bool changedToPerformance = SelectedMode == RenderingMode.Performance && SelectedMode != _savedMode;
        _plan = plan.Where(c => IsLumen || c.Key is not ("bUseNanite" or "bRayTracing") || _featuresEdited || changedToPerformance).ToArray();
        ChangesList.ItemsSource = _plan;
        int count = _plan.Count + (ProtectionChangedOnDisk ? 1 : 0);
        NavChangeCount.Text = count > 0 ? count.ToString() : "";
        PendingText.Text = count == 0 ? "No pending changes" : $"{count} pending change{(count == 1 ? "" : "s")}";
        ChangesSummary.Text = count == 0 ? "Your configuration is up to date." :
            $"{_plan.Count} INI value(s) will change." +
            (ProtectionChangedOnDisk ? $" File protection will be {(ReadOnlyCheck.IsChecked == true ? "enabled" : "disabled")}." : "");
        bool rendererChanged = _plan.Any(c => c.Section == IniSettingsService.RendererSection);
        ApplyHint.Text = rendererChanged ? "Restart Fortnite after applying." : "A backup is created before applying.";
        DiscardButton.IsEnabled = HasChanges;
        ApplyButton.IsEnabled = File.Exists(_configPath) && HasChanges && (SelectedMode.HasValue || IsLumen);
        var text = new StringBuilder();
        foreach (var group in _plan.GroupBy(x => x.Section))
        {
            text.AppendLine(group.Key);
            foreach (var change in group) text.AppendLine($"{change.Key}={change.After}");
            text.AppendLine();
        }
        PreviewBox.Text = text.Length == 0 ? "; No INI value changes." : text.ToString();
        bool photo = _settings.All(s => s.Value == 5) && SelectedMode != RenderingMode.Performance && !IsLumen;
        WarningText.Text = IsLumen ? "Lumen Fix cannot add missing hardware support. Visual quality and performance depend on the GPU and game version." :
            photo ? "Photography: screenshots only. Certain outfits can cause severe visual glitches and performance drops. Upscaling recommended." :
            "GPU warning: Cinematic values are very demanding. Use DLSS or another upscaler, especially below RTX 4090 / 5090 class.";
    }

    private void RendererChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updating) return;
        OperationText.Visibility = Visibility.Collapsed;
        _updating = true;
        NaniteCheck.IsChecked = SelectedMode != RenderingMode.Performance && _dx12Nanite;
        RayTracingCheck.IsChecked = SelectedMode != RenderingMode.Performance && _dx12RayTracing;
        _updating = false;
        UpdateAvailability();
        UpdatePlan();
    }

    private void PresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updating || IsLumen || PresetBox.SelectedItem is not PresetDefinition preset) return;
        PresetDescriptionText.Text = preset.Description;
        if (preset.Values.Count == 0) return;
        _updating = true;
        foreach (var setting in _settings)
        {
            if (preset.Values.TryGetValue(setting.Key, out int value))
            {
                setting.Value = value;
                _edited.Add(setting.Key);
            }
        }
        _dx12Nanite = preset.Nanite;
        _dx12RayTracing = preset.RayTracing;
        NaniteCheck.IsChecked = SelectedMode != RenderingMode.Performance && preset.Nanite;
        RayTracingCheck.IsChecked = SelectedMode != RenderingMode.Performance && preset.RayTracing;
        _featuresEdited = true;
        _updating = false;
        _view.Refresh();
        UpdatePlan();
    }

    private void FeatureChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updating) return;
        _featuresEdited = true;
        _dx12Nanite = NaniteCheck.IsChecked == true;
        _dx12RayTracing = RayTracingCheck.IsChecked == true;
        PresetBox.SelectedIndex = 0;
        UpdatePlan();
    }

    private void ProtectionChanged(object sender, RoutedEventArgs e) => UpdatePlan();

    private void LumenChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updating) return;
        if (IsLumen && (_savedMode != RenderingMode.DirectX12 || SelectedMode != RenderingMode.DirectX12))
        {
            _updating = true;
            LumenFixCheck.IsChecked = false;
            _updating = false;
            Notify("Apply DirectX 12 first, then enable Lumen Fix. The fix cannot be combined with a renderer change.", true);
        }
        UpdateAvailability();
        UpdatePlan();
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton button) return;
        string page = button.Tag?.ToString() ?? "Graphics";
        GraphicsPage.Visibility = page == "Graphics" ? Visibility.Visible : Visibility.Collapsed;
        ChangesPage.Visibility = page == "Changes" ? Visibility.Visible : Visibility.Collapsed;
        RecoveryPage.Visibility = page == "Recovery" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page == "Recovery" ? "File & recovery" : page;
        PageSubtitle.Text = page switch
        {
            "Changes" => "Review before applying",
            "Recovery" => "Configuration, protection and backups",
            _ => "Rendering and visual quality"
        };
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        _view.Refresh();
        NoResultsText.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (!HasChanges) return;
        try
        {
            if (_ini.IsFortniteRunning())
            {
                Notify("Close Fortnite before applying, then try again.", true);
                return;
            }
            if (IsLumen && _ini.ReadRenderingMode(_configPath) != RenderingMode.DirectX12)
                throw new InvalidOperationException("Apply DirectX 12 before using Lumen Fix.");
            bool photo = !IsLumen && SelectedMode != RenderingMode.Performance && _settings.All(s => s.Value == 5);
            if (photo && !Confirm("Photography is for screenshots only. Certain outfits can cause severe graphical glitches and major performance drops.\n\nApply these values?", "Photography")) return;
            bool rendererChanged = _plan.Any(c => c.Section == IniSettingsService.RendererSection);
            string backup = _ini.ApplyChanges(_configPath, _plan, ReadOnlyCheck.IsChecked == true, _fingerprint);
            LoadFile();
            Notify(rendererChanged ? "Saved and backed up. Restart Fortnite to use the new renderer." : "Settings saved. Backup created.");
            OperationText.ToolTip = backup;
        }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private bool CanDiscard() => !HasChanges || Confirm("Discard your pending changes?", "Unsaved changes");

    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        if (CanDiscard()) { LoadFile(); Notify("Reloaded from file."); }
    }

    private void DiscardClick(object sender, RoutedEventArgs e) { LoadFile(); Notify("Pending changes discarded."); }

    private void ChooseFileClick(object sender, RoutedEventArgs e)
    {
        if (!CanDiscard()) return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose Fortnite configuration",
            Filter = "Fortnite settings|GameUserSettings.ini|INI files|*.ini",
            FileName = "GameUserSettings.ini"
        };
        if (dialog.ShowDialog(this) != true) return;
        _configPath = dialog.FileName;
        LoadFile();
    }

    private void DetectFileClick(object sender, RoutedEventArgs e)
    {
        if (!CanDiscard()) return;
        _configPath = _ini.DefaultConfigPath;
        LoadFile();
        Notify(File.Exists(_configPath) ? "Fortnite configuration found." : "Launch Fortnite once or choose its configuration file.", !File.Exists(_configPath));
    }

    private void OpenFolderClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetDirectoryName(_configPath)}\"") { UseShellExecute = true }); }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private void RestoreClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ini.IsFortniteRunning()) { Notify("Close Fortnite before restoring a backup.", true); return; }
            if (!Confirm("Restore the latest full backup? This replaces the current INI, discards pending changes and removes read-only protection.", "Restore backup")) return;
            _ini.RestoreLatestBackup(_configPath);
            LoadFile();
            Notify("Backup restored. File is unlocked.");
        }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private void UnlockClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_ini.Fingerprint(_configPath) != _fingerprint) throw new InvalidOperationException("The file changed outside the app. Reload it first.");
            _ini.SetReadOnly(_configPath, false);
            _baselineReadOnly = false;
            ReadOnlyCheck.IsChecked = false;
            RefreshFileState();
            UpdatePlan();
            Notify("File unlocked. Fortnite can save settings again.");
        }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private void CopyPreviewClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(PreviewBox.Text); Notify("Pending values copied."); }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private void HelpClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://www.epicgames.com/help/en-US/c-202300000001636/c-202300000001719/a202300000013484") { UseShellExecute = true }); }
        catch (Exception ex) { Notify(ex.Message, true); }
    }

    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        ApplyTheme();
        if (!_persistPreferences) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_themePath)!);
            File.WriteAllText(_themePath, ThemeBox.SelectedIndex.ToString());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void ApplyTheme()
    {
        if (!_ready) return;
        var theme = ThemeBox.SelectedIndex switch { 1 => ThemeMode.Light, 2 => ThemeMode.Dark, _ => ThemeMode.System };
        if (Application.Current.ThemeMode != theme) Application.Current.ThemeMode = theme;
        bool dark = ThemeBox.SelectedIndex == 2 || ThemeBox.SelectedIndex == 0 && IsSystemDark();
        void Brush(string key, string light, string night) =>
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        Brush("TextPrimaryBrush", "#202020", "#F4F4F4");
        Brush("TextSecondaryBrush", "#616161", "#BCBCBC");
        Brush("CardBrush", "#BFFFFFFF", "#95333333");
        Brush("StrokeBrush", "#18000000", "#16FFFFFF");
        Brush("FooterBrush", "#F5F8F8F8", "#F52B2B2B");
        Brush("SelectedBrush", "#12000000", "#15FFFFFF");
        Brush("WarningBrush", "#815300", "#F2C779");
        Brush("SuccessBrush", "#126D3C", "#8ADCB0");
        if (SystemParameters.HighContrast)
        {
            Application.Current.Resources["TextPrimaryBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["TextSecondaryBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["CardBrush"] = SystemColors.WindowBrush;
            Application.Current.Resources["FooterBrush"] = SystemColors.WindowBrush;
            Application.Current.Resources["StrokeBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["SelectedBrush"] = SystemColors.ControlBrush;
            Application.Current.Resources["WarningBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["SuccessBrush"] = SystemColors.WindowTextBrush;
        }
        bool mica = !SystemParameters.HighContrast && MicaService.Apply(this, dark);
        if (!mica) Background = SystemParameters.HighContrast ? SystemColors.WindowBrush :
            new SolidColorBrush(dark ? Color.FromRgb(32, 32, 32) : Color.FromRgb(243, 243, 243));
        if (_baseline.Count > 0) RefreshFileState();
    }

    private static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private void OnClosing(object? sender, CancelEventArgs e) { if (!CanDiscard()) e.Cancel = true; }

    private bool Confirm(string message, string title) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void Notify(string message, bool error = false)
    {
        OperationText.Text = message;
        OperationText.Foreground = (Brush)FindResource(error ? "WarningBrush" : "SuccessBrush");
        OperationText.Visibility = Visibility.Visible;
        OperationText.ToolTip = message;
    }
}
