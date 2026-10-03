using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FortniteCinematicSettings.Models;

namespace FortniteCinematicSettings.Services;

public sealed record SettingChange(string Section, string Key, string Before, string After)
{
    public string DisplaySection => Section.Trim('[', ']');
}

public sealed class IniSettingsService
{
    public const string RendererSection = "[D3DRHIPreference]";
    private static readonly Regex SectionPattern = new(@"^\s*(\[[^\]]+\])\s*(?:[;#].*)?$", RegexOptions.Compiled);
    public string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");

    public bool IsFortniteRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.ProcessName.StartsWith("FortniteClient", StringComparison.OrdinalIgnoreCase) ||
                        process.ProcessName.Equals("FortniteGame", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { }
            }
        }
        return false;
    }

    public static string Address(string section, string key) => section + "\n" + key;

    public Dictionary<string, string> ReadValues(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return values;
        string section = "";
        foreach (string line in File.ReadLines(path))
        {
            var match = SectionPattern.Match(line);
            if (match.Success) { section = match.Groups[1].Value; continue; }
            string text = line.Trim();
            if (text.StartsWith(';') || text.StartsWith('#')) continue;
            int equals = text.IndexOf('=');
            if (equals > 0) values[Address(section, text[..equals].Trim())] = text[(equals + 1)..].Trim();
        }
        return values;
    }

    public Dictionary<string, string> ReadKnownValues(string path, IEnumerable<QualitySetting> qualitySettings)
    {
        var values = ReadValues(path);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in qualitySettings)
            if (values.TryGetValue(Address(setting.Section, setting.Key), out var value)) result[setting.Key] = value;
        foreach (string key in new[] { "bUseNanite", "bRayTracing" })
            if (values.TryGetValue(Address(SettingsCatalog.MainSection, key), out var value)) result[key] = value;
        return result;
    }

    public RenderingMode? ReadRenderingMode(string path)
    {
        var values = ReadValues(path);
        values.TryGetValue(Address(RendererSection, "PreferredRHI"), out var rhi);
        values.TryGetValue(Address(RendererSection, "PreferredFeatureLevel"), out var feature);
        if (!string.Equals(rhi, "dx12", StringComparison.OrdinalIgnoreCase)) return null;
        return feature?.ToLowerInvariant() switch
        {
            "es31" => RenderingMode.Performance,
            "sm6" => RenderingMode.DirectX12,
            _ => null
        };
    }

    public static bool RequiresFullRenderer(QualitySetting setting) =>
        setting.Section == SettingsCatalog.MainSection ||
        setting.Key is "sg.GlobalIlluminationQuality" or "sg.ReflectionQuality";

    public IReadOnlyList<SettingChange> CreatePlan(
        IReadOnlyDictionary<string, string> current, IEnumerable<QualitySetting> settings,
        bool nanite, bool rayTracing, RenderingMode? mode, bool lumenFix)
    {
        var changes = new List<SettingChange>();
        void Add(string section, string key, string value)
        {
            string before = current.TryGetValue(Address(section, key), out var old) ? old : "(not set)";
            if (!before.Equals(value, StringComparison.OrdinalIgnoreCase))
                changes.Add(new(section, key, before, value));
        }
        if (lumenFix)
        {
            Add(SettingsCatalog.MainSection, "DesiredGlobalIlluminationQuality", "3");
            Add(SettingsCatalog.MainSection, "PreNaniteGlobalIlluminationQuality", "3");
            Add(SettingsCatalog.MainSection, "bRayTracing", "True");
            Add(SettingsCatalog.ScalabilitySection, "sg.GlobalIlluminationQuality", "3");
            return changes;
        }
        if (mode.HasValue)
        {
            Add(RendererSection, "PreferredRHI", "dx12");
            Add(RendererSection, "PreferredFeatureLevel", mode == RenderingMode.Performance ? "es31" : "sm6");
        }
        bool performance = mode == RenderingMode.Performance;
        foreach (var setting in settings)
            if (!performance || !RequiresFullRenderer(setting))
                Add(setting.Section, setting.Key, setting.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(SettingsCatalog.MainSection, "bUseNanite", (!performance && nanite).ToString());
        Add(SettingsCatalog.MainSection, "bRayTracing", (!performance && rayTracing).ToString());
        return changes;
    }

    public string Fingerprint(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "";

    public string ApplyChanges(string path, IReadOnlyList<SettingChange> changes, bool readOnly, string? expectedFingerprint = null)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Choose an existing GameUserSettings.ini first.", path);
        if (expectedFingerprint is not null && Fingerprint(path) != expectedFingerprint)
            throw new InvalidOperationException("The file changed outside the app. Reload it before applying.");
        byte[] original = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(original), Encoding.UTF8, true);
        string text = reader.ReadToEnd();
        Encoding encoding = reader.CurrentEncoding;
        bool bom = encoding.GetPreamble().Length > 0 && original.AsSpan().StartsWith(encoding.GetPreamble());
        string newline = text.Contains("\r\n") ? "\r\n" : text.Contains('\r') ? "\r" : "\n";
        bool trailingNewline = text.EndsWith('\n') || text.EndsWith('\r');
        var lines = Regex.Split(text, "\r\n|\n|\r").ToList();
        if (trailingNewline) lines.RemoveAt(lines.Count - 1);
        foreach (var group in changes.GroupBy(x => x.Section))
            UpdateSection(lines, group.Key, group.ToDictionary(x => x.Key, x => x.After, StringComparer.OrdinalIgnoreCase));
        string updated = string.Join(newline, lines) + (trailingNewline ? newline : "");
        byte[] payload = encoding.GetBytes(updated);
        if (bom) payload = [.. encoding.GetPreamble(), .. payload];
        string backup = CreateBackup(path);
        ReplaceContents(path, payload, readOnly);
        return backup;
    }

    public string Apply(string path, IEnumerable<QualitySetting> settings, bool nanite, bool rayTracing, bool readOnly) =>
        ApplyChanges(path, CreatePlan(ReadValues(path), settings, nanite, rayTracing, null, false), readOnly);

    public string ApplyLumenFix(string path, bool readOnly)
    {
        if (ReadRenderingMode(path) != RenderingMode.DirectX12)
            throw new InvalidOperationException("Apply DirectX 12 before using Lumen Fix.");
        return ApplyChanges(path, CreatePlan(ReadValues(path), [], false, true, null, true), readOnly);
    }

    public IReadOnlyList<string> FindBackups(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (folder is null || !Directory.Exists(folder)) return [];
        string stem = Path.GetFileNameWithoutExtension(path);
        return Directory.GetFiles(folder, stem + ".fgmu-backup-*.ini")
            .Concat(Directory.GetFiles(folder, stem + ".biomeforge-backup-*.ini"))
            .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
    }

    public string? FindLatestBackup(string path) => FindBackups(path).FirstOrDefault();

    public string RestoreLatestBackup(string path)
    {
        string backup = FindLatestBackup(path) ?? throw new InvalidOperationException("No backup is available for this file.");
        ReplaceContents(path, File.ReadAllBytes(backup), false);
        return backup;
    }

    public bool IsReadOnly(string path) => File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
    public void SetReadOnly(string path, bool value)
    {
        var attributes = File.GetAttributes(path);
        File.SetAttributes(path, value ? attributes | FileAttributes.ReadOnly : attributes & ~FileAttributes.ReadOnly);
    }

    private void ReplaceContents(string path, byte[] payload, bool readOnly)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool wasReadOnly = IsReadOnly(path);
        bool replaced = false;
        try
        {
            File.WriteAllBytes(temp, payload);
            if (File.Exists(path)) { SetReadOnly(path, false); File.Replace(temp, path, null); }
            else File.Move(temp, path);
            replaced = true;
            SetReadOnly(path, readOnly);
        }
        finally
        {
            if (!replaced && File.Exists(path)) SetReadOnly(path, wasReadOnly);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void UpdateSection(List<string> lines, string name, IReadOnlyDictionary<string, string> values)
    {
        string current = "";
        int insertAt = -1;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lines.Count; i++)
        {
            var match = SectionPattern.Match(lines[i]);
            if (match.Success) current = match.Groups[1].Value;
            if (!current.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            insertAt = i + 1;
            if (match.Success) continue;
            int equals = lines[i].IndexOf('=');
            if (equals < 1) continue;
            string key = lines[i][..equals].Trim();
            if (values.TryGetValue(key, out var value))
            {
                lines[i] = key + "=" + value;
                seen.Add(key);
            }
        }
        if (insertAt < 0)
        {
            if (lines.Count > 0 && lines[^1] != "") lines.Add("");
            lines.Add(name);
            insertAt = lines.Count;
        }
        foreach (var pair in values)
            if (!seen.Contains(pair.Key)) lines.Insert(insertAt++, pair.Key + "=" + pair.Value);
    }

    private static string CreateBackup(string path)
    {
        string backup = Path.Combine(Path.GetDirectoryName(path)!,
            $"{Path.GetFileNameWithoutExtension(path)}.fgmu-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.ini");
        File.Copy(path, backup, false);
        File.SetAttributes(backup, FileAttributes.Normal);
        File.SetLastWriteTimeUtc(backup, DateTime.UtcNow);
        return backup;
    }
}
