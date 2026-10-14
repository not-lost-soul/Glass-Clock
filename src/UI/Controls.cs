using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>Drawing helpers shared by the glass controls.</summary>
    static class Gl
    {
        public static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly Color Accent = Color.FromRgb(10, 132, 255);

        public static FormattedText Text(string s, double size, FontWeight w, double alpha)
        {
            return new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(UiFont, FontStyles.Normal, w, FontStretches.Normal), size, Util.Solid(Util.White(alpha)));
        }

        /// <summary>Capsule with a glass fill, rim light and soft inner highlight.</summary>
        public static void Pill(DrawingContext dc, Rect r, double fillA, double rimA)
        {
            double rad = r.Height / 2;
            var fill = new LinearGradientBrush(Util.White(fillA * 1.5), Util.White(fillA * 0.7), 90);
            var rim = new LinearGradientBrush(Util.White(rimA), Util.White(rimA * 0.25), 90);
            dc.DrawRoundedRectangle(fill, new Pen(rim, 1.0), r, rad, rad);
        }
    }

    // ============================================================ slider
    /// <summary>Slider whose thumb turns into a larger, glassier lens while you hold it. Double-click resets. The small tick marks the default.</summary>
    class GlassSlider : MotionControl
    {
        public double Min, Max, Value, Def;
        public event Action<double> Changed;
        Spring press = new Spring(0);
        bool drag;
        const double Pad = 24;

        public GlassSlider(double min, double max, double val, double def)
        {
            Min = min; Max = max; Value = Util.Clamp(val, min, max); Def = def; Cursor = Cursors.Hand;
        }

        protected override Size MeasureOverride(Size a) { return new Size(double.IsInfinity(a.Width) ? 200 : a.Width, 36); }
        protected override bool Step(double dt) { press.Step(dt, 420, 24); return !press.Rest; }

        double XOf(double v)
        {
            double t = Max > Min ? (v - Min) / (Max - Min) : 0;
            return Pad + t * Math.Max(1, ActualWidth - 2 * Pad);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight, cy = h / 2;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
            dc.DrawRoundedRectangle(Util.Solid(Util.White(0.16)), null, new Rect(Pad, cy - 3, Math.Max(1, w - 2 * Pad), 6), 3, 3);
            double x = XOf(Value);
            var fillBr = new LinearGradientBrush(Util.Mix(Gl.Accent, Colors.White, 0.25), Gl.Accent, 0);
            dc.DrawRoundedRectangle(fillBr, null, new Rect(Pad, cy - 3, Math.Max(0, x - Pad), 6), 3, 3);
            if (Def > Min && Def < Max)
            {
                double xd = XOf(Def);
                dc.DrawRoundedRectangle(Util.Solid(Util.White(0.40)), null, new Rect(xd - 1, cy - 9, 2, 18), 1, 1);
            }
            double p = press.X, tw = 28 + 22 * p, th = 20 + 10 * p;
            Rect thumb = new Rect(x - tw / 2, cy - th / 2, tw, th);
            dc.DrawRoundedRectangle(Util.Solid(Color.FromArgb(70, 0, 0, 0)), null, new Rect(thumb.X, thumb.Y + 2, thumb.Width, thumb.Height), th / 2, th / 2);
            double fa = 0.98 - 0.30 * p;                                     // turns glassy while pressed
            var tf = new LinearGradientBrush(Util.White(fa), Util.White(fa * 0.80), 90);
            var tr = new LinearGradientBrush(Util.White(0.95), Util.White(0.30), 90);
            dc.DrawRoundedRectangle(tf, new Pen(tr, 1.0), thumb, th / 2, th / 2);
        }

        void SetFrom(double mx)
        {
            double t = Util.C01((mx - Pad) / Math.Max(1, ActualWidth - 2 * Pad));
            double v = Min + t * (Max - Min);
            v = (Max - Min >= 20) ? Math.Round(v) : Math.Round(v * 10) / 10.0;
            if (v == Value) return;
            Value = v; InvalidateVisual();
            if (Changed != null) Changed(v);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2) { Value = Def; InvalidateVisual(); if (Changed != null) Changed(Def); return; }
            drag = true; CaptureMouse(); press.T = 1; Wake(); SetFrom(e.GetPosition(this).X);
        }

        protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFrom(e.GetPosition(this).X); }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!drag) return;
            drag = false; ReleaseMouseCapture(); press.T = 0; Wake();
        }
    }

    // ============================================================ toggle
    /// <summary>Switch whose knob stretches while pressed (liquid squash) and springs into place.</summary>
    class GlassToggle : MotionControl
    {
        public event Action<bool> Changed;
        bool on;
        Spring pos = new Spring(0), press = new Spring(0);

        public GlassToggle(bool value) { on = value; pos = new Spring(value ? 1 : 0); Cursor = Cursors.Hand; }

        protected override Size MeasureOverride(Size a) { return new Size(56, 34); }

        protected override bool Step(double dt)
        {
            pos.Step(dt, 330, 21); press.Step(dt, 430, 26);
            return !(pos.Rest && press.Rest);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double t = Util.Clamp(pos.X, -0.2, 1.2), p = press.X;
            Rect track = new Rect(1, 4, 54, 26);
            Color offC = Util.White(0.20), onC = Gl.Accent;
            dc.DrawRoundedRectangle(Util.Solid(Util.Mix(offC, onC, Util.C01(t))), new Pen(Util.Solid(Util.White(0.22)), 1.0), track, 13, 13);
            double kw = 28 + 10 * p, kh = 26 + 2 * p;
            double x = 3 + (54 - 3 - 3 - kw + 2) * t - 1;
            Rect knob = new Rect(x, (34 - kh) / 2, kw, kh);
            dc.DrawRoundedRectangle(Util.Solid(Color.FromArgb(70, 0, 0, 0)), null, new Rect(knob.X, knob.Y + 2, knob.Width, knob.Height), kh / 2, kh / 2);
            double fa = 0.98 - 0.25 * p;
            var f = new LinearGradientBrush(Util.White(fa), Util.White(fa * 0.82), 90);
            var r = new LinearGradientBrush(Util.White(0.95), Util.White(0.30), 90);
            dc.DrawRoundedRectangle(f, new Pen(r, 1.0), knob, kh / 2, kh / 2);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { CaptureMouse(); press.T = 1; Wake(); }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!IsMouseCaptured) return;
            ReleaseMouseCapture(); press.T = 0;
            Point p = e.GetPosition(this);
            if (p.X >= 0 && p.X <= ActualWidth && p.Y >= 0 && p.Y <= ActualHeight)
            {
                on = !on; pos.T = on ? 1 : 0;
                if (Changed != null) Changed(on);
            }
            Wake();
        }
    }

    // ============================================================ segmented picker
    /// <summary>Segmented picker with a glass pill that slides (with a little overshoot) to the selected item.</summary>
    class GlassSegmented : MotionControl
    {
        string[] items; int sel; double[] xs, ws; double total;
        Spring px, pw;
        public event Action<int> Changed;

        public GlassSegmented(string[] it, int selected)
        {
            items = it; sel = Math.Max(0, Math.Min(it.Length - 1, selected));
            xs = new double[it.Length]; ws = new double[it.Length];
            double x = 3;
            for (int i = 0; i < it.Length; i++)
            {
                double w = Gl.Text(it[i], 13, FontWeights.SemiBold, 1).Width + 28;
                xs[i] = x; ws[i] = w; x += w;
            }
            total = x + 3;
            px = new Spring(xs[sel]); pw = new Spring(ws[sel]);
            Cursor = Cursors.Hand; HorizontalAlignment = HorizontalAlignment.Left;
        }

        protected override Size MeasureOverride(Size a) { return new Size(total, 36); }

        protected override bool Step(double dt)
        {
            px.Step(dt, 300, 17); pw.Step(dt, 300, 17);
            return !(px.Rest && pw.Rest);
        }

        protected override void OnRender(DrawingContext dc)
        {
            Gl.Pill(dc, new Rect(0, 0, total, 36), 0.07, 0.22);
            Gl.Pill(dc, new Rect(px.X, 3, Math.Max(12, pw.X), 30), 0.28, 0.60);
            for (int i = 0; i < items.Length; i++)
            {
                FormattedText ft = Gl.Text(items[i], 13, i == sel ? FontWeights.SemiBold : FontWeights.Medium, i == sel ? 1.0 : 0.62);
                dc.DrawText(ft, new Point(xs[i] + (ws[i] - ft.Width) / 2, (36 - ft.Height) / 2));
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            double mx = e.GetPosition(this).X;
            for (int i = 0; i < items.Length; i++)
            {
                if (mx >= xs[i] && mx <= xs[i] + ws[i])
                {
                    if (i == sel) return;
                    sel = i; px.T = xs[i]; pw.T = ws[i]; Wake();
                    if (Changed != null) Changed(i);
                    return;
                }
            }
        }
    }

    // ============================================================ button / chip
    class GlassButton : MotionControl
    {
        public string Label; public bool Selected, Danger; public Color? Dot;
        public event Action Click;
        Spring hov = new Spring(0), prs = new Spring(0);
        bool down;

        public GlassButton(string label) { Label = label; Cursor = Cursors.Hand; HorizontalAlignment = HorizontalAlignment.Left; Margin = new Thickness(0, 3, 8, 3); }

        protected override Size MeasureOverride(Size a)
        {
            return new Size(Gl.Text(Label, 13, FontWeights.Medium, 1).Width + 32 + (Dot.HasValue ? 18 : 0), 34);
        }

        protected override bool Step(double dt)
        {
            hov.Step(dt, 380, 26); prs.Step(dt, 450, 27);
            return !(hov.Rest && prs.Rest);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight, ins = 1.5 * prs.X;
            Rect r = new Rect(ins, ins, Math.Max(2, w - 2 * ins), Math.Max(2, h - 2 * ins));
            if (Selected) dc.DrawRoundedRectangle(Util.Solid(Util.A(Gl.Accent, 0.38)), null, r, r.Height / 2, r.Height / 2);
            if (Danger) dc.DrawRoundedRectangle(Util.Solid(Util.A(Color.FromRgb(255, 69, 58), 0.30 + 0.2 * hov.X)), null, r, r.Height / 2, r.Height / 2);
            Gl.Pill(dc, r, 0.08 + 0.10 * hov.X + (Selected ? 0.12 : 0), 0.22 + 0.22 * hov.X + (Selected ? 0.30 : 0));
            double x = 16;
            if (Dot.HasValue)
            {
                dc.DrawEllipse(Util.Solid(Dot.Value), new Pen(Util.Solid(Util.White(0.7)), 1.0), new Point(22, h / 2), 5, 5);
                x = 36;
            }
            FormattedText ft = Gl.Text(Label, 13, Selected ? FontWeights.SemiBold : FontWeights.Medium, 0.96);
            dc.DrawText(ft, new Point(x, (h - ft.Height) / 2));
        }

        protected override void OnMouseEnter(MouseEventArgs e) { hov.T = 1; Wake(); }
        protected override void OnMouseLeave(MouseEventArgs e) { hov.T = 0; prs.T = 0; down = false; Wake(); }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { down = true; prs.T = 1; CaptureMouse(); Wake(); }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!down) return;
            down = false; ReleaseMouseCapture(); prs.T = 0; Wake();
            Point p = e.GetPosition(this);
            if (p.X >= 0 && p.X <= ActualWidth && p.Y >= 0 && p.Y <= ActualHeight && Click != null) Click();
        }
    }

    // ============================================================ colour swatch
    class GlassSwatch : MotionControl
    {
        public string Hex; public bool Selected;
        public event Action<string> Picked;
        Spring hov = new Spring(0);

        public GlassSwatch(string hex, bool selected) { Hex = hex; Selected = selected; Cursor = Cursors.Hand; Margin = new Thickness(0, 2, 6, 2); }

        protected override Size MeasureOverride(Size a) { return new Size(32, 32); }
        protected override bool Step(double dt) { hov.Step(dt, 380, 22); return !hov.Rest; }

        protected override void OnRender(DrawingContext dc)
        {
            double rad = 11 + 2.5 * hov.X;
            Point c = new Point(16, 16);
            if (Selected) dc.DrawEllipse(null, new Pen(Util.Solid(Util.White(0.95)), 2.0), c, 14.5, 14.5);
            dc.DrawEllipse(Util.Solid(Util.Hex(Hex)), new Pen(Util.Solid(Util.White(0.55)), 1.0), c, rad, rad);
            var shine = new LinearGradientBrush(Util.White(0.40), Util.White(0.0), 90);
            dc.DrawEllipse(shine, null, new Point(16, 12), rad * 0.75, rad * 0.5);
        }

        protected override void OnMouseEnter(MouseEventArgs e) { hov.T = 1; Wake(); }
        protected override void OnMouseLeave(MouseEventArgs e) { hov.T = 0; Wake(); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (Picked != null) Picked(Hex); }
    }
}
