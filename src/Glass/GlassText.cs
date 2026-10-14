using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>One line of glass (or a divider bar).</summary>
    class Ln
    {
        public string T, RefText; public double Size, Space; public Typeface Tf; public int Kind; public bool Solid;   // Kind 0 text, 1 divider
        public double W, L0, RefTop, RefH, BoxW, X, Y;
        public Geometry G;
        public Ln(string t, double size, double space, Typeface tf, string refText)
        { T = t; Size = size; Space = space; Tf = tf; RefText = refText; }
    }

    class GlassLayout
    {
        public List<Ln> Lines = new List<Ln>();
        public double W, H, M;
        public Rect Content;
    }

    static class TextGeo
    {
        /// <summary>Outline of a string with custom letter spacing, baseline-independent (top of the layout box at oy).</summary>
        public static GeometryGroup Make(string s, Typeface tf, double size, double space, double ox, double oy)
        {
            var grp = new GeometryGroup(); grp.FillRule = FillRule.Nonzero;
            double x = ox;
            foreach (char c in s)
            {
                var ft = new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, Brushes.Black);
                if (c != ' ') grp.Children.Add(ft.BuildGeometry(new Point(x, oy)));
                x += ft.WidthIncludingTrailingWhitespace + space;
            }
            return grp;
        }
    }

    static class GlassText
    {
        static int Wt(double w) { int i = (int)Math.Round(w / 10.0) * 10; return i < 1 ? 1 : (i > 999 ? 999 : i); }

        static Typeface Tf(string family, double weight)
        {
            return new Typeface(FontBook.Get(family), FontStyles.Normal, FontWeight.FromOpenTypeWeight(Wt(weight)), FontStretches.Normal);
        }

        public static string Cs(string s, string mode)
        {
            if (mode == "upper") return s.ToUpper(CultureInfo.CurrentCulture);
            if (mode == "lower") return s.ToLower(CultureInfo.CurrentCulture);
            return s;
        }

        public static string DateText(Cfg C, DateTime n)
        {
            try { return n.ToString(C.DateFormat, CultureInfo.CurrentCulture); }
            catch { return n.ToString("D", CultureInfo.CurrentCulture); }
        }

        public static string TimeText(Cfg C, DateTime n, bool dashes, bool zeros)
        {
            int hr = C.Clock24 ? n.Hour : (n.Hour % 12 == 0 ? 12 : n.Hour % 12);
            string hh = C.Clock24 ? hr.ToString("00") : hr.ToString(), mm = n.Minute.ToString("00"), ss = n.Second.ToString("00");
            if (zeros) { hh = "00"; mm = "00"; ss = "00"; }
            string t = hh + ":" + mm + (C.Seconds ? ":" + ss : "");
            if (!C.Clock24 && C.AmPm) t += " " + (zeros ? "PM" : (n.Hour >= 12 ? "PM" : "AM"));
            return dashes ? "- " + t + " -" : t;
        }

        /// <summary>Changes whenever the visible text changes.</summary>
        public static string Key(Cfg C)
        {
            DateTime n = DateTime.Now;
            return n.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + (C.Seconds ? n.Second.ToString() : "");
        }

        public static GlassLayout Build(Cfg C)
        {
            Typeface tfB = Tf(C.Font, C.BigWeight), tfS = Tf(C.SmallFont, C.SmallWeight);
            DateTime n = DateTime.Now;
            double bs = C.BigSize, ss = C.SmallSize, gk = C.Gap / 100.0;

            string bigT, bigRef = null;
            if (C.Big == "Time") { bigT = TimeText(C, n, false, false); bigRef = TimeText(C, n, false, true); }
            else if (C.Big == "Date") bigT = Cs(DateText(C, n), C.BigCase);
            else bigT = Cs(n.ToString("dddd", CultureInfo.CurrentCulture), C.BigCase);

            var all = new List<Ln>();
            Ln big = new Ln(bigT, bs, bs * C.BigSpace / 100.0, tfB, bigRef);
            all.Add(big);

            var small = new List<Ln>();
            double sp = ss * C.SmallSpace / 100.0;
            if (C.ShowDay && C.Big != "Day") small.Add(new Ln(Cs(n.ToString("dddd", CultureInfo.CurrentCulture), C.SmallCase), ss, sp, tfS, null));
            if (C.ShowDate && C.Big != "Date") small.Add(new Ln(Cs(DateText(C, n), C.SmallCase), ss, sp, tfS, null));
            if (C.ShowTime && C.Big != "Time") small.Add(new Ln(TimeText(C, n, C.Dashes, false), ss, sp, tfS, TimeText(C, n, C.Dashes, true)));
            foreach (Ln l in small) { l.Solid = !C.SmallGlass; all.Add(l); }

            Ln div = null;
            if (C.Divider && small.Count > 0) { div = new Ln("", Math.Max(20.0, bs * 0.12), 0, tfB, null); div.Kind = 1; }

            foreach (Ln l in all)
            {
                Rect b = TextGeo.Make(l.T, l.Tf, l.Size, l.Space, 0, 0).Bounds;
                l.W = b.IsEmpty ? 0 : b.Width; l.L0 = b.IsEmpty ? 0 : b.Left;
                Rect rb = TextGeo.Make("H", l.Tf, l.Size, 0, 0, 0).Bounds;
                l.RefTop = rb.IsEmpty ? 0 : rb.Top; l.RefH = rb.IsEmpty ? l.Size * 0.7 : rb.Height;
                Rect bw = TextGeo.Make(l.RefText != null ? l.RefText : l.T, l.Tf, l.Size, l.Space, 0, 0).Bounds;
                l.BoxW = Math.Max(l.W, bw.IsEmpty ? 0 : bw.Width);
            }
            if (div != null) div.RefH = Math.Max(2.0, bs * 0.022);

            var seq = new List<Ln>();
            seq.Add(big); if (div != null) seq.Add(div); seq.AddRange(small);

            double gapBig = bs * 0.20 * gk, gapSmall = ss * 0.75 * gk, y = 0;
            for (int i = 0; i < seq.Count; i++)
            {
                Ln l = seq[i]; l.Y = y; y += l.RefH;
                if (i < seq.Count - 1)
                {
                    if (l == big || l.Kind == 1) y += gapBig * (seq[i + 1].Kind == 1 ? 1.0 : 0.85);
                    else y += gapSmall;
                }
            }
            double contentH = Math.Max(y, 4.0), contentW = 10;
            foreach (Ln l in seq) if (l.Kind == 0) contentW = Math.Max(contentW, l.BoxW);
            foreach (Ln l in seq)
            {
                if (l.Kind == 1) l.W = Math.Max(30.0, ss * 3.2);
                l.X = (contentW - l.W) / 2;
            }

            double M = 28 + 0.22 * bs + 0.03 * contentW;
            var L = new GlassLayout();
            L.M = M; L.W = contentW + 2 * M; L.H = contentH + 2 * M; L.Content = new Rect(M, M, contentW, contentH);
            if (div != null) all.Add(div);
            foreach (Ln l in all)
            {
                if (l.Kind == 1) l.G = new RectangleGeometry(new Rect(M + l.X, M + l.Y, l.W, l.RefH), l.RefH / 2, l.RefH / 2);
                else l.G = TextGeo.Make(l.T, l.Tf, l.Size, l.Space, M + l.X - l.L0, M + l.Y - l.RefTop);
            }
            L.Lines = all;
            return L;
        }
    }
}
