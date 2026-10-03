# Fortnite Graphics Master Utility

A native Windows app for configuring Fortnite's graphics, switching rendering modes, and managing backups of `GameUserSettings.ini`.

[Installer](Releases/FortniteGraphicsMasterUtility-SETUP.exe) | [Standalone app](Releases/FortniteGraphicsMasterUtility-STANDALONE.exe)

![Graphics view](Screenshots/app-preview.png)

## What's new in version 3

- Redesigned Windows Fluent interface with Mica, native window controls, and remembered system, light, or dark appearance.
- Separate Graphics, Changes, and File & recovery views.
- DirectX 12 and Performance Mode selection.
- Apply, status, and GPU warnings stay pinned at the bottom while settings scroll.
- Changes view shows the exact values that will be written.
- Existing values outside the 1-5 selection range are preserved until changed.

## Rendering modes

| Mode | Configuration | Behavior |
| --- | --- | --- |
| DirectX 12 | `PreferredRHI=dx12`, `PreferredFeatureLevel=sm6` | Full rendering, with Nanite, Lumen and ray tracing controls. |
| Performance | `PreferredRHI=dx12`, `PreferredFeatureLevel=es31` | Lightweight DX12 rendering. Nanite and ray tracing are turned off, and Lumen quality edits are excluded. |

Close Fortnite, choose a mode, then select **Apply settings**. The change takes effect the next time Fortnite starts. Switching modes alone preserves unrelated quality settings.

Performance here means the current DX12 Performance renderer. Epic Launcher rendering arguments can override the INI. If the mode does not stick, check the launcher's additional command-line arguments. [Epic's rendering mode guidance](https://www.epicgames.com/help/en-US/c-202300000001636/c-202300000001719/a202300000013484).

## Quality and presets

Adjust view distance, anti-aliasing, shadows, global illumination, reflections, post processing, textures, effects, foliage, shading, landscape, and the exposed Lumen quality fields.

The existing scale is retained: **1 Low, 2 Medium, 3 High, 4 Epic, 5 Cinematic**. These labels represent this utility's INI values; Fortnite may interpret or clamp them differently between versions. A current game value such as `0` remains available and is not silently increased.

Presets include Low, Medium, High, Epic, Default cinematic, and Photography. Default cinematic uses value 5 except for view distance, anti-aliasing, textures, and effects, which use 3. Photography sets every exposed quality value to 5 and should be used only for screenshots: certain outfits can cause severe visual glitches and large performance drops.

## Lumen Fix

Lumen Fix is exclusive. Apply normal DirectX 12 first, then enable the fix. It excludes renderer changes, presets, Nanite, reflection settings, and other manual quality edits, and writes only:

```ini
[/Script/FortniteGame.FortGameUserSettings]
DesiredGlobalIlluminationQuality=3
PreNaniteGlobalIlluminationQuality=3
bRayTracing=True

[ScalabilityGroups]
sg.GlobalIlluminationQuality=3
```

It requests lighting settings through the configuration file; it cannot add hardware support or guarantee that Fortnite will honor the values.

## Installation and use

Run the installer or standalone app from the supplied Releases folder. No separate .NET installation is required. Windows 11 is recommended for Mica; a solid background is used when the backdrop is unavailable.

1. Close Fortnite.
2. Open the utility. It detects your Windows user's Fortnite configuration.
3. Choose a renderer and a preset, or edit individual settings.
4. Review the Changes view.
5. In File & recovery, enable read-only protection if you want to retain your edits.
6. Apply and start Fortnite.

Use **Choose file** when the automatic location is unavailable. **Discard** reloads the saved file without applying pending edits.

## Backups and protection

A complete timestamped backup is made beside the INI before every apply. **Restore latest backup** restores the previous file and removes read-only protection. Older utility backups remain discoverable.

Read-only protection helps prevent Fortnite from overwriting edits, but also prevents it from saving other settings. Use **Unlock file now** when needed.

The app preserves file encoding, line endings, comments, and unrelated settings. It blocks applying while Fortnite is running and asks you to reload if the file changed outside the app.

## GPU requirements

Cinematic and Photography settings can be extremely demanding. DLSS or another supported upscaler is recommended, especially below RTX 4090 / 5090 class hardware. Actual performance depends on resolution, hardware, scene complexity, and Fortnite version.

The utility edits the local configuration file only. It is not affiliated with or endorsed by Epic Games.

## Building

Install the .NET 10 SDK and Inno Setup 6, then run:

```powershell
.\build-release.ps1
```

Outputs are `publish/FortniteGraphicsMasterUtility.exe` and `release/FortniteGraphicsMasterUtility-Setup.exe`.

The build runs temporary-fixture settings tests. Optional native UI checks:

```powershell
dotnet run --project tests/FortniteCinematicSettings.Tests -c Release -- --ui release/ui-verification
```

These checks cover renderer switching, backups, Lumen isolation, theme dropdowns, and the pinned footer at multiple window sizes and simulated 150% scaling. They do not launch Fortnite or change your real configuration.
