namespace FortniteCinematicSettings.Models;

public sealed record QualityOption(int Value, string Label)
{
    public string DisplayName => $"{Value} - {Label}";

    public override string ToString() => DisplayName;
}
