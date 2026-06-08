using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly IReadOnlyList<PresetDefinition> _presets;
    private readonly ICollectionView _settingsView;
    private string _configPath;
    private bool _loaded;

    public ObservableCollection<QualitySetting> QualitySettings { get; }
    public IReadOnlyList<QualityOption> QualityOptions => SettingsCatalog.QualityOptions;

    public MainWindow()
    {
        InitializeComponent();

        _configPath = _ini.DefaultConfigPath;
        QualitySettings = new ObservableCollection<QualitySetting>(SettingsCatalog.CreateQualitySettings());
        _presets = SettingsCatalog.CreatePresets();
        _settingsView = CollectionViewSource.GetDefaultView(QualitySettings);
        _settingsView.Filter = FilterSetting;
        foreach (var setting in QualitySettings)
        {
            setting.PropertyChanged += (_, _) => UpdatePreview();
        }

        DataContext = this;
        SettingsList.ItemsSource = _settingsView;
        PresetBox.ItemsSource = _presets;
        PresetBox.SelectedIndex = 4;
        ConfigPathBox.Text = _configPath;

        Loaded += MainWindow_Loaded;
        SourceInitialized += (_, _) =>
        {
            MicaService.Attach(this, ApplyTheme);
            ApplyTheme();
        };
        _loaded = true;

        ApplyPreset((PresetDefinition)PresetBox.SelectedItem);
        RefreshState("Ready.");
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        LoadCurrentValuesIntoControls();
        RefreshState("Ready.");
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_configPath))
        {
            ShowError("GameUserSettings.ini was not found. Launch Fortnite once, or choose the file manually.");
            return;
        }

        if (_ini.IsFortniteRunning() && !Confirm("Fortnite appears to be running. Close it first so the game does not overwrite the file when it exits.\n\nApply anyway?", "Fortnite is running"))
        {
            return;
        }

        try
        {
            string result;
            if (LumenFixCheck.IsChecked == true)
            {
                if (!Confirm("Lumen Fix ignores presets and only writes ray tracing plus Lumen/global illumination keys at value 3.\n\nIt is intended for systems where Epic removed the Lumen option because hardware ray tracing is unsupported. Performance or visual issues may still occur.\n\nApply Lumen Fix?", "Lumen Fix"))
                {
                    return;
                }

                result = _ini.ApplyLumenFix(_configPath, ReadOnlyCheck.IsChecked == true);
            }
            else
            {
                var preset = PresetBox.SelectedItem as PresetDefinition;
                if (preset?.RequiresWarning == true &&
                    !Confirm("Photography maxes every exposed quality value to 5. It should only be used for screenshots.\n\nIt can cause severe graphical glitches when other players wear specific outfits and can cause severe performance drops.\n\nApply Photography anyway?", "Photography warning"))
                {
                    return;
                }

                result = _ini.Apply(
                    _configPath,
                    QualitySettings,
                    NaniteCheck.IsChecked == true,
                    RayTracingCheck.IsChecked == true,
                    ReadOnlyCheck.IsChecked == true);
            }

            RefreshState(result);
            LoadCurrentValuesIntoControls();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadCurrentValuesIntoControls();
        RefreshState("Refreshed from disk.");
    }

    private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Fortnite GameUserSettings.ini",
            Filter = "GameUserSettings.ini|GameUserSettings.ini|INI files (*.ini)|*.ini|All files (*.*)|*.*",
            FileName = "GameUserSettings.ini"
        };

        string? folder = Path.GetDirectoryName(_configPath);
        if (folder is not null && Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }

        if (dialog.ShowDialog(this) == true)
        {
            _configPath = dialog.FileName;
            ConfigPathBox.Text = _configPath;
            LoadCurrentValuesIntoControls();
            RefreshState("Using selected file.");
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folder = Path.GetDirectoryName(_configPath);
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string backup = _ini.RestoreLatestBackup(_configPath);
            LoadCurrentValuesIntoControls();
            RefreshState($"Restored backup:\n{backup}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LumenFixCheck?.IsChecked == true)
        {
            return;
        }

        if (PresetBox.SelectedItem is PresetDefinition preset)
        {
            ApplyPreset(preset);
            PresetDescriptionText.Text = preset.Description;
        }
    }

    private void LumenFixCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        bool active = LumenFixCheck.IsChecked == true;
        LumenFixStateText.Text = active ? "On" : "Off";
        PresetBox.IsEnabled = !active;
        NaniteCheck.IsEnabled = !active;
        RayTracingCheck.IsEnabled = !active;
        SettingsList.IsEnabled = !active;
        SearchBox.IsEnabled = !active;
        ApplyButton.Content = active ? "Apply Lumen Fix" : "Apply settings";

        PresetDescriptionText.Text = active
            ? "Lumen Fix is active and cannot mix with presets or manual quality controls."
            : ((PresetDefinition?)PresetBox.SelectedItem)?.Description ?? string.Empty;

        UpdatePreview();
    }

    private void FeatureToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        NaniteStateText.Text = NaniteCheck.IsChecked == true ? "On" : "Off";
        RayTracingStateText.Text = RayTracingCheck.IsChecked == true ? "On" : "Off";
        ReadOnlyStateText.Text = ReadOnlyCheck.IsChecked == true ? "On" : "Off";
        UpdatePreview();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _settingsView.Refresh();
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded)
        {
            ApplyTheme();
        }
    }

    private void ApplyPreset(PresetDefinition preset)
    {
        foreach (var setting in QualitySettings)
        {
            if (preset.Values.TryGetValue(setting.Key, out int value))
            {
                setting.Value = value;
            }
        }

        NaniteCheck.IsChecked = preset.Nanite;
        RayTracingCheck.IsChecked = preset.RayTracing;
        PresetDescriptionText.Text = preset.Description;
        UpdatePreview();
    }

    private void LoadCurrentValuesIntoControls()
    {
        var values = _ini.ReadKnownValues(_configPath, QualitySettings);
        foreach (var setting in QualitySettings)
        {
            if (values.TryGetValue(setting.Key, out string? raw) && int.TryParse(raw, out int value))
            {
                setting.Value = Math.Clamp(value, 1, 5);
            }
        }

        if (values.TryGetValue("bUseNanite", out string? nanite))
        {
            NaniteCheck.IsChecked = nanite.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        if (values.TryGetValue("bRayTracing", out string? rayTracing))
        {
            RayTracingCheck.IsChecked = rayTracing.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        NaniteStateText.Text = NaniteCheck.IsChecked == true ? "On" : "Off";
        RayTracingStateText.Text = RayTracingCheck.IsChecked == true ? "On" : "Off";
        ReadOnlyStateText.Text = ReadOnlyCheck.IsChecked == true ? "On" : "Off";
        UpdatePreview();
    }

    private void RefreshState(string message)
    {
        bool exists = File.Exists(_configPath);
        bool readOnly = _ini.IsReadOnly(_configPath);
        string? backup = _ini.FindLatestBackup(_configPath);

        StatusText.Text = exists ? "Found" : "Not found";
        StatusText.Foreground = (Brush)FindResource(exists ? "SuccessBrush" : "WarningBrush");
        ProtectionText.Text = exists ? (readOnly ? "Read-only enabled" : "Read-only off") : "Unavailable";
        OpenFolderButton.IsEnabled = Directory.Exists(Path.GetDirectoryName(_configPath));
        UndoButton.IsEnabled = backup is not null;
        ApplyButton.IsEnabled = exists;
        MessageText.Text = message;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var builder = new StringBuilder();
        if (LumenFixCheck?.IsChecked == true)
        {
            builder.AppendLine("; Mode: Lumen Fix");
            builder.AppendLine(SettingsCatalog.MainSection);
            builder.AppendLine("DesiredGlobalIlluminationQuality=3");
            builder.AppendLine("PreNaniteGlobalIlluminationQuality=3");
            builder.AppendLine("bRayTracing=True");
            builder.AppendLine();
            builder.AppendLine(SettingsCatalog.ScalabilitySection);
            builder.AppendLine("sg.GlobalIlluminationQuality=3");
        }
        else
        {
            builder.AppendLine("; Pending settings");
            builder.AppendLine(SettingsCatalog.MainSection);
            builder.AppendLine($"bUseNanite={NaniteCheck?.IsChecked == true}");
            foreach (var setting in QualitySettings.Where(x => x.Section == SettingsCatalog.MainSection))
            {
                builder.AppendLine($"{setting.Key}={setting.Value}");
            }
            builder.AppendLine($"bRayTracing={RayTracingCheck?.IsChecked == true}");
            builder.AppendLine();
            builder.AppendLine(SettingsCatalog.ScalabilitySection);
            foreach (var setting in QualitySettings.Where(x => x.Section == SettingsCatalog.ScalabilitySection))
            {
                builder.AppendLine($"{setting.Key}={setting.Value}");
            }
        }

        PreviewBox.Text = builder.ToString();
    }

    private bool FilterSetting(object item)
    {
        if (item is not QualitySetting setting)
        {
            return false;
        }

        string query = SearchBox?.Text?.Trim() ?? string.Empty;
        return query.Length == 0 ||
               setting.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               setting.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               setting.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyTheme()
    {
        bool dark = ThemeBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() switch
            {
                "Light" => false,
                "Dark" => true,
                _ => IsSystemDarkTheme()
            }
            : IsSystemDarkTheme();

        SetBrush("TextPrimaryBrush", dark ? "#F8F8F8" : "#1A1A1A");
        SetBrush("TextSecondaryBrush", dark ? "#C8CCD6" : "#4B5565");
        SetBrush("TextMutedBrush", dark ? "#99A0AE" : "#697386");
        bool transparency = MicaService.IsTransparencyEnabled();

        SetBrush("LayerBrush", dark ? "#B52B2926" : "#D8F3F3F3");
        SetBrush("SidebarBrush", transparency
            ? (dark ? "#982E2C29" : "#B8F0F0F0")
            : (dark ? "#FF272522" : "#FFF0F0F0"));
        SetBrush("CardBrush", transparency
            ? (dark ? "#BC42403D" : "#D8FFFFFF")
            : (dark ? "#FF403E3B" : "#FFFFFFFF"));
        SetBrush("CardHoverBrush", transparency
            ? (dark ? "#CF4B4946" : "#ECF7F7F7")
            : (dark ? "#FF4B4946" : "#FFF7F7F7"));
        SetBrush("CardStrokeBrush", dark ? "#3CFFFFFF" : "#22000000");
        SetBrush("ControlBrush", transparency
            ? (dark ? "#C8413F3C" : "#E8FFFFFF")
            : (dark ? "#FF413F3C" : "#FFFFFFFF"));
        SetBrush("ControlHoverBrush", transparency
            ? (dark ? "#E052504D" : "#F5FFFFFF")
            : (dark ? "#FF52504D" : "#FFFFFFFF"));
        SetBrush("SelectedNavBrush", dark ? "#D4474542" : "#E5E5E5");
        SetBrush("DropDownTextBrush", dark ? "#FFFFFF" : "#111111");
        SetBrush("ScrollThumbBrush", dark ? "#70FFFFFF" : "#65000000");
        SetBrush("ScrollThumbHoverBrush", dark ? "#B0FFFFFF" : "#95000000");
        SetBrush("AccentBrush", dark ? "#60CDFF" : "#0067C0");
        SetBrush("AccentHoverBrush", dark ? "#79D6FF" : "#1975C5");
        SetBrush("WarningBrush", dark ? "#F7C15F" : "#A76500");
        SetBrush("SuccessBrush", dark ? "#70E5A4" : "#107C41");

        MicaService.Apply(this, dark);
    }

    private void SetBrush(string key, string color)
    {
        Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            object? value = Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                ?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return true;
        }
    }

    private bool Confirm(string message, string title) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Fortnite Graphics Master Utility", MessageBoxButton.OK, MessageBoxImage.Error);
        RefreshState(message);
    }
}
