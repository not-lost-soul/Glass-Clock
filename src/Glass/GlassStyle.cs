using System;
using System.Windows;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>
    /// All brushes of the Liquid Glass material, built from the settings.
    /// Layers follow Apple's description: highlights that respond to a light direction, lensing at the edges,
    /// colour spill, adaptive tint, shadow, and a dimming layer for the "Clear" variant.
    /// </summary>
    class GlassStyle
    {
        public Brush Veil, Tint, Dim, Bevel, Dark, Spill, Streak, InnerGlow, Small;
        public Pen DispR, DispB;
        public double Rim, Lens, Disp, Shadow;

        static LinearGradientBrush Grad(Point s, Point e, double[] offs, Color[] cols)
        {
            var b = new LinearGradientBrush(); b.StartPoint = s; b.EndPoint = e;
            for (int i = 0; i < offs.Length; i++) b.GradientStops.Add(new GradientStop(cols[i], offs[i]));
            b.Freeze(); return b;
        }

        public static GlassStyle From(Cfg C)
        {
            var S = new GlassStyle();
            double veil = C.Veil / 100.0, rim = C.Rim / 100.0, lens = C.Lens / 100.0, spill = C.Spill / 100.0,
                   dim = C.Dim / 100.0, sheen = C.Sheen / 100.0, disp = C.Disp / 100.0;
            S.Rim = rim; S.Lens = lens; S.Disp = disp; S.Shadow = C.Shadow / 100.0;

            double rad = C.Light * Math.PI / 180.0, lx = Math.Cos(rad), ly = -Math.Sin(rad);
            Point ls = new Point(0.5 + 0.5 * lx, 0.5 + 0.5 * ly), le = new Point(0.5 - 0.5 * lx, 0.5 - 0.5 * ly);

            Color tc = Util.Hex(C.TintColor), acc = Util.Hex(C.Accent);
            Color none = Color.FromArgb(0, 255, 255, 255);

            // frost: lighter on the lit side (the wallpaper itself shows through - nothing is captured or faked)
            S.Veil = Grad(ls, le, new double[] { 0, 1 }, new Color[] { Util.White(0.50 * veil), Util.White(0.16 * veil) });
            S.Tint = Util.Solid(Util.A(tc, C.Tint / 100.0 * 0.6));
            S.Dim = Util.Solid(Color.FromArgb((byte)(255 * 0.45 * dim), 0, 0, 0));
            // highlights layer: bright where the light hits, softer bounce on the opposite edge
            S.Bevel = Grad(ls, le, new double[] { 0, 0.22, 0.5, 0.78, 1 },
                new Color[] { Util.White(0.95 * rim), Util.White(0.34 * rim), Util.White(0.04 * rim), Util.White(0.20 * rim), Util.White(0.60 * rim) });
            // thickness: darker inner edge away from the light
            S.Dark = Grad(ls, le, new double[] { 0, 0.55, 1 },
                new Color[] { Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), Color.FromArgb((byte)(40 + 50 * lens), 0, 0, 0) });
            // light concentrated / spilled along the far edge
            Color sc = Util.Mix(Colors.White, acc, 0.75);
            S.Spill = Grad(ls, le, new double[] { 0, 0.45, 1 },
                new Color[] { Util.A(sc, 0), Util.A(sc, 0), Util.A(sc, Math.Min(0.85, 0.10 + 0.60 * spill)) });
            // soft streak across the glass
            S.Streak = Grad(new Point(0, 0.1), new Point(1, 0.9), new double[] { 0, 0.34, 0.5, 0.66, 1 },
                new Color[] { none, none, Util.White(0.30 * sheen), none, none });
            // lensing falloff: the rim of the glass is brighter than its clear centre
            S.InnerGlow = Util.Solid(Util.White(0.05 + 0.17 * lens));

            Color sm = Util.Hex(C.SmallColor);
            S.Small = Util.Solid(Util.A(sm, C.SmallOpacity / 100.0));

            var rr = Util.Solid(Color.FromArgb((byte)(170 * disp), 255, 70, 110));
            var bb = Util.Solid(Color.FromArgb((byte)(170 * disp), 70, 140, 255));
            S.DispR = new Pen(rr, 1.0); S.DispR.Freeze(); S.DispB = new Pen(bb, 1.0); S.DispB.Freeze();
            return S;
        }
    }
}
