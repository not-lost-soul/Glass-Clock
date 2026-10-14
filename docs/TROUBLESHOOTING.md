# Troubleshooting

## I can't see the clock
1. Tray icon -> **Layer -> Normal window**. If it appears, the app is fine and it is a placement issue (below).
2. Tray -> **Diagnostics...** and look at the list. The line `<== GLASSCLOCK` must be directly above the `[HOST]` lines (the wallpaper windows).
3. If the wallpaper window is not marked `[HOST]`: open Settings -> System and add part of its process name to *Wallpaper app process names*
   (for example `razer`, `axon`, `wallpaper64`, `lively`), or keep *Treat borderless full-screen background windows as wallpaper* on.
4. Try **Layer = Embedded** (experimental).
5. Look at `%APPDATA%\GlassClock\glassclock.log` and attach it, with the diagnostics, to a GitHub issue.

## The glass looks too faint / too bright / too dark
The wallpaper shows *through* the glass, so tune it once for your wallpaper: raise **Frost**, add **Tint**, or switch **Material** to Clear
and use **Dimming**. The preview at the top of Settings can be switched between light and dark backdrops by clicking it.

## The font looks thin
Weights 600-900 show glass best. Poppins Bold (700) is the default. Thin fonts leave too little room for the glass effect.

## Build errors
`build.bat` prints the compiler errors. The code is C# 5 so the in-box compiler works; if you use a newer SDK, `dotnet build` also works.

## The clock moved off-screen after changing monitors
Tray -> **Re-centre**.
