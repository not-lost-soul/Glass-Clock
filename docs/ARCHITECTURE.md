# Architecture

```
Program.Main
  FontBook.ExtractBundled/Reload   fonts embedded in the exe -> %APPDATA%\GlassClock\bundled-fonts, then system + user fonts
  Cfg.Load                          config_v3.ini (public fields, reflection)
  ClockWindow                       the desktop widget
     GlassText.Build   -> GlassLayout   (text -> vector outlines + positions, per-character letter spacing)
     GlassStyle.From   -> brushes       (the material, from Cfg)
     GlassRenderer.Draw                 (draws shadow / glass / rim into a DrawingContext)
     RenderTargetBitmap                 rendered ONCE per change, shown by two <Image>s (cross-fade on change)
     Placement                          window-stack logic (desktop / embedded / diagnostics)
  Tray                               NotifyIcon menu
  SettingsWindow                     animated Liquid Glass UI built from Controls.cs
```

## Why it is fast
The window never draws per frame. The glass (many gradient strokes) is rendered to a bitmap only when the text changes
(once a minute) or a setting changes (debounced 35 ms while dragging a slider). Animations (materialize, minute cross-fade,
press feedback) only animate the opacity / scale of that cached bitmap.

The previous prototypes captured the screen several times per second to fake refraction and re-rendered heavy pens every
frame, which is what made them laggy and flickery. Because the glass interior is simply transparent, the real wallpaper -
including a video wallpaper - shows through with no capture.

## Window placement (`Core/Placement.cs`)
* **Desktop** (default): enumerate top-level windows top->bottom, find the contiguous run of *wallpaper host* windows at the very
  bottom, and place the clock directly above them. A window is a host if it is `Progman`/`WorkerW`, or a big (>=95% of the screen),
  caption-less window that is tool/no-activate, matches a listed process name (default `axon;razer;wallpaper;lively`), or - with
  *generic host* on - any background borderless full-screen window. The clock is only repositioned when it is in the wrong place,
  never "sent to bottom" repeatedly (which both flickers and can bury the clock *under* a wallpaper window).
* **Embedded** (experimental): sends `0x052C` to Progman, parents the clock to the wallpaper `WorkerW` (Windows 11 24H2: child of
  Progman) like Lively / Wallpaper Engine. Not draggable (the desktop icons layer sits above); use the position sliders.
* **Normal / On top**: ordinary windows.

## Controls (`UI/Controls.cs`, `UI/Motion.cs`)
Custom `FrameworkElement`s drawn with `OnRender`. `Spring` (semi-implicit Euler) drives hover / press / slide; one shared
`CompositionTarget.Rendering` loop runs **only while something is moving**.

## Conventions
* C# 5 only (no `$""`, `?.`, `nameof`, expression-bodied members...) so the in-box compiler can build it.
* Settings = public fields on `Cfg`; add a field and it is persisted automatically.
