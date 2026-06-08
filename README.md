# Fortnite Graphics Master Utility

A native Windows 11 utility for safely managing Fortnite's hidden graphics settings in `GameUserSettings.ini`.

The app uses Windows Mica, supports light and dark themes, creates automatic backups, and provides presets plus individual quality controls ranging from Low to Cinematic.

![Fortnite Graphics Master Utility](Screenshots/app-preview.png)

## Features

- Native Windows 11 Mica design
- System, light, and dark themes
- Automatically locates Fortnite's `GameUserSettings.ini`
- Quality controls from `1 - Low` through `5 - Cinematic`
- Low, Medium, High, Epic, Default Cinematic, and Photography presets
- Exclusive Lumen Fix mode for devices where the Lumen option was removed
- Automatic timestamped backups
- One-click backup restore
- Optional read-only protection
- Fortnite-running warning
- Searchable settings list
- Live preview of pending INI values

## Presets

| Preset | Description |
| --- | --- |
| Low | Sets every exposed quality value to 1 for maximum performance. |
| Medium | Sets every exposed quality value to 2. |
| High | Sets every exposed quality value to 3 and enables Nanite and ray tracing. |
| Epic | Sets every exposed quality value to 4. |
| Default Cinematic | Recommended cinematic preset for normal matches. |
| Photography | Sets every exposed quality value to 5. Intended only for screenshots. |

Photography mode can cause severe graphical glitches with certain outfits and major performance drops.

## Lumen Fix

Lumen Fix is an exclusive mode that ignores presets and manual quality controls. It only applies:

```ini
DesiredGlobalIlluminationQuality=3
PreNaniteGlobalIlluminationQuality=3
bRayTracing=True
sg.GlobalIlluminationQuality=3
```

It is intended for devices where Epic removed the Lumen option because the hardware does not support hardware ray tracing. Performance or visual issues may still occur.

## Installation

Download and run the installer:

[FortniteGraphicsMasterUtility-Setup.exe](Releases/FortniteGraphicsMasterUtility-Setup.exe)

A standalone executable is also available:

[FortniteGraphicsMasterUtility.exe](Releases/FortniteGraphicsMasterUtility.exe)

## Usage

1. Close Fortnite.
2. Launch Fortnite Graphics Master Utility.
3. Choose a preset or configure individual quality settings.
4. Keep read-only protection enabled if you want to prevent Fortnite from resetting the values.
5. Click **Apply settings**.

Use **Restore backup** to undo the latest change.

## GPU Warning

Cinematic and Photography values have immense GPU requirements. DLSS, TSR, XeSS, or another upscaler is recommended unless the system has at least an RTX 4090 or RTX 5090.

## Building

Requirements:

- Windows 11
- .NET 10 SDK
- Inno Setup 6

Build the standalone executable and installer:

```powershell
.\build-release.ps1
```

Generated files:

- `publish/FortniteGraphicsMasterUtility.exe`
- `release/FortniteGraphicsMasterUtility-Setup.exe`

## Project Structure

```text
src/FortniteCinematicSettings/       WPF application source
tests/FortniteCinematicSettings.Tests/ Settings service smoke tests
installer/                           Inno Setup installer definition
tools/                               Branding asset generation script
Releases/                            Ready-to-distribute installer and portable app
Screenshots/                         GitHub README preview image
build-release.ps1                    Full build and installer pipeline
```

## Notes

- The utility modifies only the local `GameUserSettings.ini`.
- It does not modify Fortnite installation files.
- Backups are stored beside the original INI.
- Windows transparency effects must be enabled for the full Mica appearance.

Fortnite is a trademark of Epic Games. This project is not affiliated with or endorsed by Epic Games.
