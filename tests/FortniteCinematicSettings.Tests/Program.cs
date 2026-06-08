using FortniteCinematicSettings.Services;

string source = args.Length > 0
    ? args[0]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame",
        "Saved",
        "Config",
        "WindowsClient",
        "GameUserSettings.ini");

if (!File.Exists(source))
{
    throw new FileNotFoundException("A source GameUserSettings.ini is required for the smoke tests.", source);
}

string temp = Path.Combine(Path.GetTempPath(), "FortniteGraphicsMasterUtility-Test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
string config = Path.Combine(temp, "GameUserSettings.ini");
File.Copy(source, config);

try
{
    var service = new IniSettingsService();
    var settings = SettingsCatalog.CreateQualitySettings();
    var presets = SettingsCatalog.CreatePresets();

    var low = presets.Single(x => x.Name == "Low");
    foreach (var setting in settings)
    {
        setting.Value = low.Values[setting.Key];
    }

    service.Apply(config, settings, low.Nanite, low.RayTracing, true);
    string lowText = File.ReadAllText(config);
    Require(lowText.Contains("sg.ViewDistanceQuality=1"), "Low preset did not write value 1.");
    Require(lowText.Contains("bRayTracing=False"), "Low preset did not disable ray tracing.");
    Require(service.IsReadOnly(config), "Read-only was not set.");

    service.ApplyLumenFix(config, false);
    string lumenText = File.ReadAllText(config);
    Require(lumenText.Contains("DesiredGlobalIlluminationQuality=3"), "Lumen Fix GI was not 3.");
    Require(lumenText.Contains("sg.GlobalIlluminationQuality=3"), "Lumen Fix scalability GI was not 3.");
    Require(lumenText.Contains("sg.ViewDistanceQuality=1"), "Lumen Fix changed an unrelated setting.");

    service.RestoreLatestBackup(config);
    Require(!service.IsReadOnly(config), "Restore did not clear read-only.");
    Require(Directory.GetFiles(temp, "GameUserSettings.fgmu-backup-*.ini").Length >= 2, "Backups were not created.");

    Console.WriteLine("All settings service smoke tests passed.");
}
finally
{
    foreach (string file in Directory.GetFiles(temp))
    {
        File.SetAttributes(file, FileAttributes.Normal);
    }

    Directory.Delete(temp, true);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
