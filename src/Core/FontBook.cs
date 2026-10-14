using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>System fonts + bundled fonts (embedded in the exe, extracted on first run) + fonts the user imported.</summary>
    static class FontBook
    {
        public static List<string> Names = new List<string>();
        static Dictionary<string, FontFamily> custom = new Dictionary<string, FontFamily>();

        /// <summary>Fonts embedded as resources (see build.bat / csproj) are written to %APPDATA%\GlassClock\bundled-fonts once.</summary>
        public static void ExtractBundled()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (string res in asm.GetManifestResourceNames())
                {
                    string r = res.ToLower();
                    if (!r.EndsWith(".ttf") && !r.EndsWith(".otf")) continue;
                    string file = Path.Combine(Paths.Bundled, res);
                    if (File.Exists(file)) continue;
                    using (Stream s = asm.GetManifestResourceStream(res))
                    using (FileStream f = File.Create(file)) s.CopyTo(f);
                }
            }
            catch (Exception e) { Logger.Write("fonts: " + e.Message); }
        }

        public static void Reload()
        {
            Names.Clear(); custom.Clear();
            try { foreach (var ff in Fonts.SystemFontFamilies) Names.Add(ff.Source); } catch { }
            string exeFonts = "";
            try { exeFonts = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "fonts"); } catch { }
            ScanDir(Paths.Bundled); ScanDir(Paths.Fonts); ScanDir(exeFonts);
            Names.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        static void Register(string name, FontFamily ff)
        {
            if (string.IsNullOrEmpty(name) || custom.ContainsKey(name)) return;
            custom[name] = ff;
            if (!Names.Contains(name)) Names.Add(name);
        }

        static void ScanDir(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                var baseUri = new Uri(dir.TrimEnd('\\') + "\\");
                foreach (string file in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext != ".ttf" && ext != ".otf") continue;
                    try
                    {
                        var gt = new GlyphTypeface(new Uri(file));
                        var us = CultureInfo.GetCultureInfo("en-US");
                        string n1 = null, n2 = null;
                        if (!gt.FamilyNames.TryGetValue(us, out n1)) foreach (var kv in gt.FamilyNames) { n1 = kv.Value; break; }
                        if (!gt.Win32FamilyNames.TryGetValue(us, out n2)) foreach (var kv in gt.Win32FamilyNames) { n2 = kv.Value; break; }
                        if (n1 != null) Register(n1, new FontFamily(baseUri, "./#" + n1));
                        if (n2 != null && n2 != n1) Register(n2, new FontFamily(baseUri, "./#" + n2));
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static FontFamily Get(string name)
        {
            FontFamily ff;
            if (name != null && custom.TryGetValue(name, out ff)) return ff;
            try { return new FontFamily(string.IsNullOrEmpty(name) ? "Segoe UI" : name); }
            catch { return new FontFamily("Segoe UI"); }
        }
    }
}
