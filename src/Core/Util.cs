using System;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>Flags passed to ClockWindow.Refresh() so only the needed work is done.</summary>
    static class F { public const int Vis = 1, Layout = 2, Mode = 4; }

    static class Util
    {
        public static double C01(double v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
        public static double Clamp(double v, double a, double b) { return v < a ? a : (v > b ? b : v); }

        public static Color Hex(string s)
        {
            try
            {
                s = s.TrimStart('#');
                return Color.FromRgb(Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16));
            }
            catch { return Colors.White; }
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        public static Color A(Color c, double alpha)
        {
            return Color.FromArgb((byte)(255 * C01(alpha)), c.R, c.G, c.B);
        }

        public static Color White(double alpha) { return Color.FromArgb((byte)(255 * C01(alpha)), 255, 255, 255); }

        public static SolidColorBrush Solid(Color c)
        {
            var b = new SolidColorBrush(c); b.Freeze(); return b;
        }

        static double dpiScale;
        /// <summary>System DPI scale (1.0 = 96 dpi). WPF without a manifest is system-DPI-aware, so this matches WPF's own scale.</summary>
        public static double Scale()
        {
            if (dpiScale > 0) return dpiScale;
            try
            {
                using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) dpiScale = g.DpiX / 96.0;
            }
            catch { dpiScale = 1.0; }
            if (dpiScale < 0.5) dpiScale = 1.0;
            return dpiScale;
        }
    }
}
