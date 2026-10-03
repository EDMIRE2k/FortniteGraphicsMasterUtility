using FortniteCinematicSettings.Models;

namespace FortniteCinematicSettings.Services;

public static class SettingsCatalog
{
    public const string MainSection = "[/Script/FortniteGame.FortGameUserSettings]";
    public const string ScalabilitySection = "[ScalabilityGroups]";

    public static IReadOnlyList<QualityOption> QualityOptions { get; } =
    [
        new(1, "Low"),
        new(2, "Medium"),
        new(3, "High"),
        new(4, "Epic"),
        new(5, "Cinematic")
    ];

    public static IReadOnlyList<QualitySetting> CreateQualitySettings() =>
    [
        new("DesiredGlobalIlluminationQuality", "Global illumination", "Controls the requested Lumen global illumination tier.", MainSection, 3),
        new("DesiredReflectionQuality", "Reflections", "Controls the requested Lumen reflection tier.", MainSection, 3),
        new("PreNaniteGlobalIlluminationQuality", "Fallback global illumination", "Global illumination used before Nanite is active.", MainSection, 3),
        new("PreNaniteReflectionQuality", "Fallback reflections", "Reflection quality used before Nanite is active.", MainSection, 3),
        new("sg.ViewDistanceQuality", "View distance", "Controls how far high-detail objects remain visible.", ScalabilitySection, 3),
        new("sg.AntiAliasingQuality", "Anti-aliasing", "Controls edge smoothing and temporal reconstruction quality.", ScalabilitySection, 3),
        new("sg.ShadowQuality", "Shadows", "Controls shadow resolution, distance, and detail.", ScalabilitySection, 3),
        new("sg.GlobalIlluminationQuality", "Scalability global illumination", "Controls the active Lumen global illumination tier.", ScalabilitySection, 3),
        new("sg.ReflectionQuality", "Scalability reflections", "Controls the active reflection quality tier.", ScalabilitySection, 3),
        new("sg.PostProcessQuality", "Post processing", "Controls bloom, depth of field, and other post effects.", ScalabilitySection, 3),
        new("sg.TextureQuality", "Textures", "Controls texture resolution and streaming quality.", ScalabilitySection, 3),
        new("sg.EffectsQuality", "Effects", "Controls particles, translucency, and effect complexity.", ScalabilitySection, 3),
        new("sg.FoliageQuality", "Foliage", "Controls foliage density and draw distance.", ScalabilitySection, 3),
        new("sg.ShadingQuality", "Shading", "Controls material and lighting shading quality.", ScalabilitySection, 3),
        new("sg.LandscapeQuality", "Landscape", "Controls terrain and landscape detail.", ScalabilitySection, 3)
    ];

    public static IReadOnlyList<PresetDefinition> CreatePresets()
    {
        var settings = CreateQualitySettings();
        Dictionary<string, int> Uniform(int value) => settings.ToDictionary(x => x.Key, _ => value);

        var defaultCinematic = Uniform(5);
        defaultCinematic["sg.ViewDistanceQuality"] = 3;
        defaultCinematic["sg.AntiAliasingQuality"] = 3;
        defaultCinematic["sg.TextureQuality"] = 3;
        defaultCinematic["sg.EffectsQuality"] = 3;

        return
        [
            new("Low", "Quality tier 1 across the available settings.", false, false, Uniform(1)),
            new("Medium", "Balanced low-to-mid quality values.", false, false, Uniform(2)),
            new("High", "High quality with Lumen tier 3 values.", true, true, Uniform(3)),
            new("Epic", "Epic quality across every exposed setting.", true, true, Uniform(4)),
            new("Default cinematic", "Recommended cinematic preset for normal matches.", true, true, defaultCinematic),
            new("Photography", "Maxes every exposed quality value. Screenshots only.", true, true, Uniform(5), true)
        ];
    }
}
