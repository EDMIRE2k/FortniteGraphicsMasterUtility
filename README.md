# Fortnite Graphics Master Utility

A native Windows 11 utility for safely managing Fortnite's hidden graphics settings in `GameUserSettings.ini`.

The app uses Windows Mica, supports light and dark themes, creates automatic backups, and provides presets plus individual quality controls ranging from Low to Cinematic.

<img width="1505" height="964" alt="image" src="https://github.com/user-attachments/assets/29a461ed-f7e8-454a-bfd3-4a5412796939" />


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

## Notes

- The utility modifies only the local `GameUserSettings.ini`.
- It does not modify Fortnite installation files.
- Backups are stored beside the original INI.
- Windows transparency effects must be enabled for the full Mica appearance.

Fortnite is a trademark of Epic Games. This project is not affiliated with or endorsed by Epic Games.
