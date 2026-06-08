using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FortniteCinematicSettings.Models;

namespace FortniteCinematicSettings.Services;

public sealed class IniSettingsService
{
    private static readonly Regex SectionPattern = new(@"^\s*\[.+\]\s*$", RegexOptions.Compiled);

    public string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame",
        "Saved",
        "Config",
        "WindowsClient",
        "GameUserSettings.ini");

    public bool IsFortniteRunning()
    {
        try
        {
            return Process.GetProcesses().Any(p =>
                p.ProcessName.Contains("Fortnite", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public Dictionary<string, string> ReadKnownValues(string path, IEnumerable<QualitySetting> qualitySettings)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var keys = qualitySettings.Select(x => x.Key)
            .Concat(["bUseNanite", "bRayTracing"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path))
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string key = line[..equalsIndex].Trim();
            if (keys.Contains(key))
            {
                result[key] = line[(equalsIndex + 1)..].Trim();
            }
        }

        return result;
    }

    public string Apply(
        string path,
        IEnumerable<QualitySetting> qualitySettings,
        bool nanite,
        bool rayTracing,
        bool readOnly)
    {
        var bySection = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [SettingsCatalog.MainSection] = qualitySettings
                .Where(x => x.Section == SettingsCatalog.MainSection)
                .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase)
                .Concat(new Dictionary<string, string>
                {
                    ["bUseNanite"] = nanite.ToString(),
                    ["bRayTracing"] = rayTracing.ToString()
                })
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            [SettingsCatalog.ScalabilitySection] = qualitySettings
                .Where(x => x.Section == SettingsCatalog.ScalabilitySection)
                .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase)
        };

        return ApplySections(path, bySection, readOnly, "settings");
    }

    public string ApplyLumenFix(string path, bool readOnly)
    {
        var bySection = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [SettingsCatalog.MainSection] = new Dictionary<string, string>
            {
                ["DesiredGlobalIlluminationQuality"] = "3",
                ["PreNaniteGlobalIlluminationQuality"] = "3",
                ["bRayTracing"] = "True"
            },
            [SettingsCatalog.ScalabilitySection] = new Dictionary<string, string>
            {
                ["sg.GlobalIlluminationQuality"] = "3"
            }
        };

        return ApplySections(path, bySection, readOnly, "Lumen Fix");
    }

    public string RestoreLatestBackup(string path)
    {
        string? backup = FindLatestBackup(path);
        if (backup is null)
        {
            throw new InvalidOperationException("No utility backup was found next to GameUserSettings.ini.");
        }

        if (File.Exists(path))
        {
            ClearReadOnly(path);
        }

        File.Copy(backup, path, true);
        ClearReadOnly(path);
        return backup;
    }

    public string? FindLatestBackup(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (folder is null || !Directory.Exists(folder))
        {
            return null;
        }

        return Directory.GetFiles(folder, "GameUserSettings.fgmu-backup-*.ini")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public bool IsReadOnly(string path) =>
        File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);

    public void SetReadOnly(string path, bool value)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        File.SetAttributes(path, value
            ? attributes | FileAttributes.ReadOnly
            : attributes & ~FileAttributes.ReadOnly);
    }

    private string ApplySections(
        string path,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> sections,
        bool readOnly,
        string operationName)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("GameUserSettings.ini was not found.", path);
        }

        ClearReadOnly(path);
        string backup = CreateBackup(path);
        string original = File.ReadAllText(path);
        string newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = Regex.Split(original, "\r\n|\n|\r").ToList();

        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        foreach (var section in sections)
        {
            UpdateSection(lines, section.Key, section.Value);
        }

        File.WriteAllText(path, string.Join(newline, lines) + newline, new UTF8Encoding(false));
        SetReadOnly(path, readOnly);
        return $"Applied {operationName}. Backup created at:\n{backup}";
    }

    private static void UpdateSection(
        List<string> lines,
        string sectionName,
        IReadOnlyDictionary<string, string> values)
    {
        int sectionStart = lines.FindIndex(x =>
            string.Equals(x.Trim(), sectionName, StringComparison.OrdinalIgnoreCase));

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && lines[^1].Length != 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(sectionName);
            sectionStart = lines.Count - 1;
        }

        int sectionEnd = lines.FindIndex(sectionStart + 1, x => SectionPattern.IsMatch(x));
        if (sectionEnd < 0)
        {
            sectionEnd = lines.Count;
        }

        foreach (var pair in values)
        {
            int existing = -1;
            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                int equalsIndex = lines[i].IndexOf('=');
                if (equalsIndex > 0 &&
                    string.Equals(lines[i][..equalsIndex].Trim(), pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    existing = i;
                    break;
                }
            }

            if (existing >= 0)
            {
                lines[existing] = $"{pair.Key}={pair.Value}";
            }
            else
            {
                lines.Insert(sectionEnd, $"{pair.Key}={pair.Value}");
                sectionEnd++;
            }
        }
    }

    private static string CreateBackup(string path)
    {
        string folder = Path.GetDirectoryName(path)!;
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backup = Path.Combine(folder, $"GameUserSettings.fgmu-backup-{stamp}.ini");
        int suffix = 2;

        while (File.Exists(backup))
        {
            backup = Path.Combine(folder, $"GameUserSettings.fgmu-backup-{stamp}-{suffix}.ini");
            suffix++;
        }

        File.Copy(path, backup);
        return backup;
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
