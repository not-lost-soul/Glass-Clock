using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GlassClock
{
    /// <summary>
    /// Where the clock sits in the window stack.
    ///   Desktop  : directly ABOVE the wallpaper windows (Progman / WorkerW / Razer Axon / Wallpaper Engine ...) and below every app.
    ///              It is only moved when it is in the wrong place (no periodic "send to bottom", which caused flicker and
    ///              pushed the clock BEHIND the animated wallpaper window).
    ///   Embedded : (experimental) child of the desktop wallpaper host, like Lively / Wallpaper Engine do.
    /// </summary>
    static class Placement
    {
        static bool bottomDone;

        static string ClassOf(IntPtr h)
        {
            var sb = new StringBuilder(256); Native.GetClassName(h, sb, 256); return sb.ToString();
        }

        static string TitleOf(IntPtr h)
        {
            var sb = new StringBuilder(256); Native.GetWindowText(h, sb, 256); return sb.ToString();
        }

        static string ProcOf(IntPtr h, Dictionary<uint, string> cache)
        {
            uint pid; Native.GetWindowThreadProcessId(h, out pid);
            string n;
            if (cache.TryGetValue(pid, out n)) return n;
            n = "";
            try { n = Process.GetProcessById((int)pid).ProcessName; } catch { }
            cache[pid] = n;
            return n;
        }

        static bool NameMatch(string proc, string names)
        {
            if (string.IsNullOrEmpty(proc) || string.IsNullOrEmpty(names)) return false;
            string p = proc.ToLower();
            foreach (string t in names.Split(';'))
            {
                string k = t.Trim().ToLower();
                if (k.Length > 0 && p.Contains(k)) return true;
            }
            return false;
        }

        static bool IsBig(Native.RECT r)
        {
            int w = r.R - r.L, h = r.B - r.T;
            return w >= Native.GetSystemMetrics(0) * 0.95 && h >= Native.GetSystemMetrics(1) * 0.95;
        }

        static bool IsHost(IntPtr h, IntPtr fg, string names, bool generic, Dictionary<uint, string> cache)
        {
            string cls = ClassOf(h);
            if (cls == "Progman" || cls == "WorkerW") return true;
            Native.RECT r; if (!Native.GetWindowRect(h, out r)) return false;
            if (!IsBig(r)) return false;
            int style = Native.GetWindowLong(h, Native.GWL_STYLE), ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            if ((style & Native.WS_CAPTION) == Native.WS_CAPTION) return false;       // real app windows have a caption
            if (NameMatch(ProcOf(h, cache), names)) return true;                       // user-listed wallpaper app
            if (h == fg) return false;                                                 // the app you are using is never "wallpaper"
            if ((ex & (Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE)) != 0) return true;
            return generic;                                                            // borderless full-screen window in the background
        }

        static List<IntPtr> ZOrder()
        {
            var z = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l) { z.Add(h); return true; }, IntPtr.Zero);
            return z;
        }

        /// <summary>Put 'me' directly above the wallpaper windows. Cheap no-op when already there.</summary>
        public static void PlaceDesktop(IntPtr me, string names, bool generic)
        {
            if (me == IntPtr.Zero) return;
            try
            {
                var z = ZOrder();
                IntPtr fg = Native.GetForegroundWindow();
                var cache = new Dictionary<uint, string>();
                int hostTop = -1;
                for (int i = z.Count - 1; i >= 0; i--)
                {
                    IntPtr h = z[i];
                    if (h == me) continue;
                    if (!Native.IsWindowVisible(h) || Native.Cloaked(h)) continue;
                    Native.RECT r; if (!Native.GetWindowRect(h, out r)) continue;
                    if (r.R - r.L < 2 || r.B - r.T < 2) continue;
                    if (IsHost(h, fg, names, generic, cache)) hostTop = i; else break;
                }

                if (hostTop < 0)
                {
                    // nothing recognisable: fall back to "bottom", but only ONCE (never keep fighting for the bottom)
                    if (!bottomDone) { Native.SetWindowPos(me, Native.HWND_BOTTOM, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE); bottomDone = true; }
                    return;
                }
                bottomDone = false;

                int meIdx = z.IndexOf(me);
                if (meIdx == hostTop - 1) return;                                   // already directly above the wallpaper

                IntPtr after;
                if (hostTop == 0) after = Native.HWND_NOTOPMOST;
                else
                {
                    after = z[hostTop - 1];
                    if ((Native.GetWindowLong(after, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) != 0) after = Native.HWND_NOTOPMOST;   // never become topmost
                }
                Native.SetWindowPos(me, after, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
            catch (Exception e) { Logger.Write("PlaceDesktop: " + e.Message); }
        }

        // ---------------------------------------------------------------- embedded (experimental)
        public static bool IsEmbedded(IntPtr me)
        {
            return me != IntPtr.Zero && (Native.GetWindowLong(me, Native.GWL_STYLE) & Native.WS_CHILD) != 0;
        }

        public static bool Embed(IntPtr me)
        {
            try
            {
                IntPtr progman = Native.FindWindow("Progman", null);
                if (progman == IntPtr.Zero) return false;
                IntPtr res; Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out res);

                IntPtr host = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);       // Windows 11 24H2+: child of Progman
                if (host == IntPtr.Zero)
                {
                    IntPtr found = IntPtr.Zero;
                    Native.EnumWindows(delegate(IntPtr h, IntPtr l)
                    {
                        IntPtr shell = Native.FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (shell != IntPtr.Zero) found = Native.FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
                        return true;
                    }, IntPtr.Zero);
                    host = found;
                }
                if (host == IntPtr.Zero) host = progman;

                int style = Native.GetWindowLong(me, Native.GWL_STYLE);
                style = (style & ~Native.WS_POPUP) | Native.WS_CHILD | Native.WS_VISIBLE;
                Native.SetWindowLong(me, Native.GWL_STYLE, style);
                Native.SetParent(me, host);
                Native.SetWindowPos(me, Native.HWND_TOP, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                return true;
            }
            catch (Exception e) { Logger.Write("Embed: " + e.Message); return false; }
        }

        public static void Detach(IntPtr me)
        {
            try
            {
                if (!IsEmbedded(me)) return;
                int style = Native.GetWindowLong(me, Native.GWL_STYLE);
                style = (style & ~Native.WS_CHILD) | Native.WS_POPUP;
                Native.SetWindowLong(me, Native.GWL_STYLE, style);
                Native.SetParent(me, IntPtr.Zero);
            }
            catch (Exception e) { Logger.Write("Detach: " + e.Message); }
        }

        /// <summary>Move to a SCREEN pixel position (works for top-level and embedded windows).</summary>
        public static void MoveToScreenPx(IntPtr me, int x, int y)
        {
            if (me == IntPtr.Zero) return;
            if (IsEmbedded(me))
            {
                Native.POINT p; p.X = x; p.Y = y;
                Native.ScreenToClient(Native.GetParent(me), ref p);
                x = p.X; y = p.Y;
            }
            Native.SetWindowPos(me, IntPtr.Zero, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }

        // ---------------------------------------------------------------- diagnostics
        public static string Dump(IntPtr me, string names, bool generic)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GlassClock diagnostics  " + DateTime.Now);
            sb.AppendLine("Windows build : " + Native.WinBuild());
            sb.AppendLine("Screen px     : " + Native.GetSystemMetrics(0) + " x " + Native.GetSystemMetrics(1));
            sb.AppendLine("Wallpaper apps: " + names + "   generic-host=" + generic);
            sb.AppendLine("Embedded      : " + IsEmbedded(me));
            sb.AppendLine();
            sb.AppendLine("Window stack, TOP -> BOTTOM (visible windows).  The clock should sit directly above the lowest HOST windows.");
            var z = ZOrder(); IntPtr fg = Native.GetForegroundWindow(); var cache = new Dictionary<uint, string>();
            int n = 0;
            for (int i = 0; i < z.Count; i++)
            {
                IntPtr h = z[i];
                bool vis = Native.IsWindowVisible(h);
                if (!vis && h != me) continue;
                Native.RECT r; Native.GetWindowRect(h, out r);
                int style = Native.GetWindowLong(h, Native.GWL_STYLE), ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                string tag = h == me ? "  <== GLASSCLOCK" : (IsHost(h, fg, names, generic, cache) ? "  [HOST]" : "");
                string t = TitleOf(h); if (t.Length > 40) t = t.Substring(0, 40);
                sb.AppendLine(string.Format("{0,3}  {1,-10} {2,-26} {3,-18} {4,5}x{5,-5} {6}{7}{8}{9}  \"{10}\"{11}",
                    i, h.ToInt64().ToString("X"), ClassOf(h), ProcOf(h, cache), r.R - r.L, r.B - r.T,
                    (style & Native.WS_CAPTION) == Native.WS_CAPTION ? "caption " : "", (ex & Native.WS_EX_TOOLWINDOW) != 0 ? "tool " : "",
                    (ex & Native.WS_EX_TOPMOST) != 0 ? "topmost " : "", Native.Cloaked(h) ? "cloaked" : "", t, tag));
                if (++n > 60) break;
            }
            return sb.ToString();
        }
    }
}
