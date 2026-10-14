# GlassClock

**A Liquid Glass day / date / time widget for the Windows desktop** - a big, widely-spaced glass word (`THURSDAY`) with the date and
time underneath, in the style of a Rainmeter skin. Designed to float over **animated wallpapers such as Razer Axon**,
Wallpaper Engine and Lively as well as plain wallpapers.

> Inspired by Apple's public design material on Liquid Glass (WWDC25 sessions 219 and 323). GlassClock is an independent
> project and is not affiliated with or endorsed by Apple.

## Highlights

* **Real see-through glass.** The glass is transparent, so the wallpaper (even a video) shows through. There is **no screen capture**,
  so it is smooth, uses ~0% CPU while idle and cannot lag or flicker the way capture-based widgets do.
* **Liquid Glass material:** frost, tint, lensing falloff at the edges, light-direction highlights, thickness shading, colour spill,
  chromatic dispersion, adaptive shadow, plus *Regular* and *Clear* variants (Clear adds a dimming layer, as Apple recommends).
* **Bundled font - Poppins** (wide geometric, thick strokes that show the glass well). Any installed font works, and you can import your own.
* **Animated Liquid Glass settings:** capsule controls, a slider thumb that turns into glass while held, a toggle that stretches,
  a segmented picker whose pill glides with a little overshoot, staggered page transitions, real acrylic on Windows 11,
  and a live preview over four backdrops.
* **Wallpaper-service aware placement:** sits directly *above* the wallpaper windows and *below* every app - and only moves when it has to.
* Tray icon, drag to move, mouse wheel to resize, lock, click-through, start with Windows, diagnostics tool.

## Install

### Option A - one click (no installs)
1. Download / clone this repository.
2. Double-click **`build.bat`**. It compiles with the C# compiler that already ships with Windows 10/11 and starts `dist\GlassClock.exe`.
3. Find the tray icon (bottom-right, maybe under the `^`) -> **Settings**.

### Option B - .NET SDK / Visual Studio
```
dotnet build GlassClock.csproj -c Release
```
(`.github/workflows/build.yml` does exactly this on every push.)

Requirements: Windows 10 or 11. Acrylic in the settings window needs Windows 11 22H2+ (older systems get a solid dark window).

## Using it

| Action | How |
| --- | --- |
| Move | drag the clock |
| Resize | mouse wheel over the clock |
| Settings | tray icon -> *Settings*, or right-click the clock |
| Lock / click-through | Settings -> System |
| Switch big text (Day / Time / Date) | Settings -> Content |
| Import a font | Settings -> Text -> *Import font file* |

Settings are stored in `%APPDATA%\GlassClock\config_v3.ini`; logs in `glassclock.log` next to it.

## Razer Axon / animated wallpapers

Default layer **Desktop** puts the clock right above the wallpaper windows (Progman, WorkerW, Axon's wallpaper window...) and
below all apps. If it cannot see the clock:

1. Tray -> **Diagnostics...** - lists the window stack. The clock (`<== GLASSCLOCK`) must sit directly above the `[HOST]` windows.
   If Axon's wallpaper window is *not* tagged `[HOST]`, add part of its process name in **Settings -> System -> Wallpaper app process names**.
2. Try **Layer = Embedded** (experimental) - the clock becomes part of the desktop layer like a wallpaper.
3. **Layer = Normal** just shows it as an ordinary window (proves it is running).

More in [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## Project layout

```
src/Core    Native.cs (Win32), Placement.cs (z-order / embed / diagnostics), Config.cs, FontBook.cs, Util.cs
src/Glass   GlassText.cs (layout -> outlines), GlassStyle.cs (brushes), GlassRenderer.cs (the material)
src/UI      ClockWindow.cs, SettingsWindow.cs, Controls.cs (glass controls), Motion.cs (springs), Tray.cs
fonts/      Poppins (OFL) - embedded into the exe
docs/       architecture, how Apple's Liquid Glass ideas were mapped, customisation, troubleshooting
```
The code is deliberately **C# 5 compatible** so `build.bat` works with the compiler built into Windows.

## Docs

* [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
* [docs/LIQUID_GLASS.md](docs/LIQUID_GLASS.md)
* [docs/CUSTOMIZATION.md](docs/CUSTOMIZATION.md)
* [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)

## License

MIT (see `LICENSE`). Poppins is (c) The Poppins Project Authors, SIL OFL 1.1 (see `fonts/OFL.txt`).
