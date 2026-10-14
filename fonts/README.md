# Bundled fonts

GlassClock ships **Poppins** (Light, Regular, Medium, Bold) - licensed under the SIL Open Font License 1.1, see `OFL.txt`.

The `.ttf` files in this folder are **embedded into `GlassClock.exe`** at build time (`build.bat` / the `.csproj`) and
extracted to `%APPDATA%\GlassClock\bundled-fonts` on first launch, so the exe works on its own.

Want another bundled font? Drop any OFL/free `.ttf` / `.otf` here and rebuild. (Users can also import fonts at runtime from
**Settings -> Text -> Import font file** without rebuilding.)
