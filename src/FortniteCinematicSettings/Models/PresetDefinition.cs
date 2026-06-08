namespace FortniteCinematicSettings.Models;

public sealed record PresetDefinition(
    string Name,
    string Description,
    bool Nanite,
    bool RayTracing,
    IReadOnlyDictionary<string, int> Values,
    bool RequiresWarning = false)
{
    public override string ToString() => Name;
}
