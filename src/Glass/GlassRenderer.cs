using System;
using System.Windows;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>
    /// Draws the glass into a DrawingContext. The interior stays transparent, so whatever the wallpaper is doing
    /// (including Razer Axon video wallpapers) shows through naturally - no screen capture, no lag.
    /// The result is rendered ONCE into a bitmap (see ClockWindow), so the idle cost is zero.
    /// </summary>
    static class GlassRenderer
    {
        public static void Draw(DrawingContext dc, GlassLayout L, GlassStyle S)
        {
            Rect canvas = new Rect(0, 0, L.W, L.H);
            foreach (Ln ln in L.Lines)
            {
                if (ln.G == null) continue;
                if (ln.Solid) Solid(dc, ln, S, canvas); else Glass(dc, ln, S, canvas);
            }
        }

        static Geometry Outside(Geometry g, Rect canvas)
        {
            try { return Geometry.Combine(new RectangleGeometry(canvas), g, GeometryCombineMode.Exclude, null); }
            catch { return null; }
        }

        static Pen P(Brush b, double w)
        {
            var p = new Pen(b, Math.Max(1.0, w)); p.LineJoin = PenLineJoin.Round; return p;
        }

        // soft shadow, drawn only OUTSIDE the glyphs so the glass interior stays see-through
        static void Shadow(DrawingContext dc, Geometry g, double u, double amt, Rect canvas)
        {
            if (amt < 0.01) return;
            Geometry outside = Outside(g, canvas);
            if (outside != null) dc.PushClip(outside);
            dc.PushTransform(new TranslateTransform(0, u * 0.05));
            double[] sw = { 0.03, 0.09, 0.17, 0.28 }, sa = { 0.34, 0.20, 0.11, 0.06 };
            for (int i = 0; i < 4; i++)
            {
                var b = new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Min(1.0, amt * sa[i])), 0, 0, 0));
                dc.DrawGeometry(null, P(b, u * sw[i]), g);
            }
            dc.Pop();
            if (outside != null) dc.Pop();
        }

        static void Solid(DrawingContext dc, Ln ln, GlassStyle S, Rect canvas)
        {
            Shadow(dc, ln.G, ln.Size * 1.6, S.Shadow * 0.9, canvas);
            dc.DrawGeometry(S.Small, null, ln.G);
        }

        static void Glass(DrawingContext dc, Ln ln, GlassStyle S, Rect canvas)
        {
            Geometry g = ln.G;
            Rect r = g.Bounds;
            if (r.IsEmpty || r.Width < 0.5 || r.Height < 0.5) return;
            double u = ln.Size, lw = 0.6 + 0.8 * S.Lens;       // lens strength scales the edge depth

            Shadow(dc, g, u, S.Shadow, canvas);

            dc.PushClip(g);
            dc.DrawRectangle(S.Dim, null, r);                                   // dimming layer (Clear variant)
            dc.DrawRectangle(S.Veil, null, r);                                  // frost
            dc.DrawRectangle(S.Tint, null, r);                                  // adaptive tint
            dc.DrawGeometry(null, P(S.InnerGlow, u * 0.34 * lw), g);            // lensing falloff
            dc.DrawGeometry(null, P(S.Spill, u * 0.30 * lw), g);                // colour spill
            dc.DrawRectangle(S.Streak, null, r);                                // sheen
            dc.PushOpacity(0.38);
            dc.DrawGeometry(null, P(S.Bevel, u * 0.22 * lw), g);                // soft highlight
            dc.Pop();
            dc.DrawGeometry(null, P(S.Bevel, u * 0.085 * lw), g);               // crisp highlight
            dc.DrawGeometry(null, P(S.Dark, u * 0.09 * lw), g);                 // thickness
            dc.Pop();

            // thin specular outline + chromatic dispersion
            dc.DrawGeometry(null, P(S.Bevel, Math.Min(2.6, u * 0.010)), g);
            if (S.Disp > 0.03)
            {
                double o = 0.4 + u * 0.005;
                dc.PushTransform(new TranslateTransform(-o, 0)); dc.DrawGeometry(null, S.DispR, g); dc.Pop();
                dc.PushTransform(new TranslateTransform(o, 0)); dc.DrawGeometry(null, S.DispB, g); dc.Pop();
            }
        }
    }
}
