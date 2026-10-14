# Customisation

Everything is in **Settings** (tray icon -> Settings). Double-click a slider to reset it; the small tick marks the default.

* **Look** - Material (Regular / Clear), style presets (clear, frost, smoke, aurora, sunset, rose, gold, neon, mono), tint colour + amount,
  light spill colour, frost, dimming, lensing, edge highlights, sheen, light direction, dispersion, colour spill, shadow, opacity.
* **Content** - big word: Day / Time / Date; case; small lines (day, date, time); dashes `- 12:06 -`; divider; make small text glass;
  24 h / AM-PM / seconds; date format presets and a custom .NET format string (`dddd`, `MMMM`, `dd`, `yyyy`...).
* **Text** - fonts (big and small separately), import `.ttf` / `.otf`, size, weight, letter-spacing, solid colour/opacity, line gap.
* **System** - layer, wallpaper process names, lock, click-through, start with Windows, position, animations, diagnostics, reset.

## Making your own preset
Edit `Presets.Apply` in `src/Core/Config.cs` - a preset is just a few `Cfg` fields (tint colour, amount, accent, dispersion).

## Config file
`%APPDATA%\GlassClock\config_v3.ini` is plain `Name=Value`. Delete it to reset everything.
