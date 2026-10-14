using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace GlassClock
{
    static class Paths
    {
        public static readonly string Dir, ConfigFile, Fonts, Bundled, LogFile, DiagFile;
        static Paths()
        {
            Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassClock");
            ConfigFile = Path.Combine(Dir, "config_v3.ini");
            Fonts = Path.Combine(Dir, "fonts");
            Bundled = Path.Combine(Dir, "bundled-fonts");
            LogFile = Path.Combine(Dir, "glassclock.log");
            DiagFile = Path.Combine(Dir, "diagnostics.txt");
            try { Directory.CreateDirectory(Dir); Directory.CreateDirectory(Fonts); Directory.CreateDirectory(Bundled); } catch { }
        }
    }

    static class Logger
    {
        public static void Write(string msg)
        {
            try
            {
                if (File.Exists(Paths.LogFile) && new FileInfo(Paths.LogFile).Length > 256 * 1024) File.Delete(Paths.LogFile);
                File.AppendAllText(Paths.LogFile, DateTime.Now.ToString("s") + "  " + msg + "\r\n");
            }
            catch { }
        }
    }

    /// <summary>All user settings. Public fields are saved / loaded by reflection (config.ini).</summary>
    class Cfg
    {
        // content
        public bool Clock24 = true, Seconds, AmPm = true, ShowDay, ShowDate = true, ShowTime = true, Dashes = true, Divider, SmallGlass;
        public string Big = "Day", DateFormat = "dd MMMM, yyyy", BigCase = "upper", SmallCase = "upper";
        // fonts + sizes
        public string Font = "Poppins", SmallFont = "Poppins";
        public double BigSize = 110, BigWeight = 700, BigSpace = 18, SmallSize = 22, SmallWeight = 500, SmallSpace = 16, Gap = 100;
        // glass material
        public string Variant = "regular", Theme = "clear", TintColor = "#FFFFFF", Accent = "#9DB8FF", SmallColor = "#FFFFFF";
        public double Tint, Veil = 45, Dim, Lens = 70, Rim = 85, Sheen = 55, Light = 135, Disp = 25, Spill = 35, Shadow = 45, SmallOpacity = 92, Opacity = 100;
        // window
        public string Layer = "desktop", WallpaperApps = "axon;razer;wallpaper;lively";
        public bool Locked, ClickThrough, GenericHost = true, Animations = true, Interactive = true;
        public double X = -99999, Y = -99999;

        public void Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path))
                {
                    int i = line.IndexOf('='); if (i <= 0) continue;
                    var f = typeof(Cfg).GetField(line.Substring(0, i).Trim()); if (f == null) continue;
                    string v = line.Substring(i + 1).Trim();
                    try
                    {
                        if (f.FieldType == typeof(bool)) f.SetValue(this, v == "1" || v.ToLower() == "true");
                        else if (f.FieldType == typeof(double)) f.SetValue(this, double.Parse(v, CultureInfo.InvariantCulture));
                        else f.SetValue(this, v);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public void Save(string path)
        {
            var sb = new StringBuilder();
            foreach (var f in typeof(Cfg).GetFields())
            {
                object v = f.GetValue(this); string s;
                if (v is bool) s = (bool)v ? "1" : "0";
                else if (v is double) s = ((double)v).ToString("0.###", CultureInfo.InvariantCulture);
                else s = (string)v;
                sb.AppendLine(f.Name + "=" + s);
            }
            try { File.WriteAllText(path, sb.ToString()); } catch { }
        }

        public void ResetKeepingPosition()
        {
            double x = X, y = Y;
            var fresh = new Cfg();
            foreach (var f in typeof(Cfg).GetFields()) f.SetValue(this, f.GetValue(fresh));
            X = x; Y = y;
        }
    }

    static class Autostart
    {
        const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

        public static bool IsOn()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("GlassClock") != null; }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (on) k.SetValue("GlassClock", "\"" + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "\"");
                    else k.DeleteValue("GlassClock", false);
                }
            }
            catch (Exception e) { Logger.Write("autostart: " + e.Message); }
        }
    }

    static class Presets
    {
        public static readonly string[] Names = { "clear", "frost", "smoke", "aurora", "sunset", "rose", "gold", "neon", "mono" };

        public static void Apply(Cfg C, string n)
        {
            C.Theme = n;
            switch (n)
            {
                case "frost": C.TintColor = "#FFFFFF"; C.Tint = 26; C.Accent = "#BFD4FF"; C.Disp = 20; break;
                case "smoke": C.TintColor = "#05070D"; C.Tint = 38; C.Accent = "#8AB4FF"; C.Disp = 20; break;
                case "aurora": C.TintColor = "#2EC4B6"; C.Tint = 16; C.Accent = "#5CF2C8"; C.Disp = 30; break;
                case "sunset": C.TintColor = "#FF7A59"; C.Tint = 16; C.Accent = "#FFA070"; C.Disp = 25; break;
                case "rose": C.TintColor = "#FF6B9A"; C.Tint = 16; C.Accent = "#FF8FB5"; C.Disp = 25; break;
                case "gold": C.TintColor = "#FFC15A"; C.Tint = 22; C.Accent = "#FFD27A"; C.Disp = 20; break;
                case "neon": C.TintColor = "#00E5FF"; C.Tint = 14; C.Accent = "#00F5FF"; C.Disp = 85; break;
                case "mono": C.TintColor = "#FFFFFF"; C.Tint = 10; C.Accent = "#FFFFFF"; C.Disp = 0; break;
                default: C.TintColor = "#FFFFFF"; C.Tint = 0; C.Accent = "#9DB8FF"; C.Disp = 25; break;
            }
        }

        /// <summary>Regular = frostier + legible anywhere.  Clear = permanently more transparent, dimmed for legibility (as in Apple's guidance).</summary>
        public static void ApplyVariant(Cfg C, string v)
        {
            C.Variant = v;
            if (v == "clear") { C.Veil = 22; C.Dim = 12; C.Lens = 90; C.Sheen = 45; C.Shadow = 50; }
            else { C.Veil = 45; C.Dim = 0; C.Lens = 70; C.Sheen = 55; C.Shadow = 45; }
        }
    }
}
