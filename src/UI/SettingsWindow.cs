using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GlassClock
{
    /// <summary>
    /// Settings, styled like the Liquid Glass design system (WWDC25 #323):
    /// capsule controls, a slider thumb that turns into glass while held, a toggle that stretches, a segmented picker whose
    /// pill glides to the selection, concentric rounded cards, staggered page transitions. On Windows 11 the window uses real acrylic.
    /// </summary>
    class SettingsWindow : Window
    {
        ClockWindow d; Cfg C;
        GlassSegmented tabs; ScrollViewer sv; Image preview; Border previewBox;
        StackPanel page, cur; int tab, bgIdx;

        static readonly string[] TabNames = { "Look", "Content", "Text", "System" };
        static readonly string[] Swatches = { "#FFFFFF", "#B4BED6", "#9DB8FF", "#6C8CFF", "#5CF2C8", "#2EC4B6", "#FFC15A", "#FFA070", "#FF6B9A", "#C28BFF", "#0A0E22", "#000000" };
        static readonly string[] DateFormats = { "dd MMMM, yyyy", "dd MMMM yyyy", "dddd, MMMM dd", "ddd, MMM d", "MMMM dd, yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "d MMM" };
        static Brush[] Backdrops;

        static LinearGradientBrush G(string a, string b, string c)
        {
            var br = new LinearGradientBrush(); br.StartPoint = new Point(0, 0); br.EndPoint = new Point(1, 1);
            br.GradientStops.Add(new GradientStop(Util.Hex(a), 0)); br.GradientStops.Add(new GradientStop(Util.Hex(b), 0.55)); br.GradientStops.Add(new GradientStop(Util.Hex(c), 1));
            br.Freeze(); return br;
        }

        static SettingsWindow()
        {
            Backdrops = new Brush[] { G("#FF8A5B", "#C94B6F", "#3B2A63"), G("#0F4C5C", "#1B7F8C", "#0A1E3F"), G("#7A8AA0", "#4E5D78", "#232B3D"), G("#BFE3FF", "#EAF6FF", "#FFFFFF") };
        }

        public SettingsWindow(ClockWindow clock)
        {
            d = clock; C = clock.C;
            Title = "GlassClock"; Width = 660; Height = 800; MinWidth = 540; MinHeight = 540;
            FontFamily = Gl.UiFont; FontSize = 13; Foreground = Brushes.White;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Util.Solid(Color.FromRgb(21, 23, 31));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // live preview of the glass over a few backdrops (click to change)
            preview = new Image(); preview.Stretch = Stretch.Uniform; preview.Margin = new Thickness(10);
            RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
            var hint = new TextBlock { Text = "click to change backdrop", FontSize = 11, Opacity = 0.55, Margin = new Thickness(14, 0, 0, 8), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, Foreground = Brushes.White };
            var pg = new Grid(); pg.Children.Add(preview); pg.Children.Add(hint);
            previewBox = new Border { Height = 170, CornerRadius = new CornerRadius(26), Margin = new Thickness(16, 14, 16, 8), ClipToBounds = true, Background = Backdrops[0], Child = pg, Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1), BorderBrush = new LinearGradientBrush(Util.White(0.35), Util.White(0.08), 90) };
            previewBox.MouseLeftButtonUp += delegate
            {
                bgIdx = (bgIdx + 1) % Backdrops.Length; previewBox.Background = Backdrops[bgIdx];
                if (C.Animations) { var a = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(300)); previewBox.BeginAnimation(UIElement.OpacityProperty, a); }
            };
            Grid.SetRow(previewBox, 0); grid.Children.Add(previewBox);

            tabs = new GlassSegmented(TabNames, 0); tabs.HorizontalAlignment = HorizontalAlignment.Center; tabs.Margin = new Thickness(0, 4, 0, 6);
            tabs.Changed += delegate(int i) { ShowTab(i, true); };
            Grid.SetRow(tabs, 1); grid.Children.Add(tabs);

            sv = new ScrollViewer(); sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            Grid.SetRow(sv, 2); grid.Children.Add(sv);
            Content = grid;

            d.Rendered += OnRendered;
            if (d.LastBitmap != null) preview.Source = d.LastBitmap;
            Closed += delegate { d.Rendered -= OnRendered; };
            Loaded += delegate
            {
                if (C.Animations) { var a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)); BeginAnimation(UIElement.OpacityProperty, a); }
            };
            ShowTab(0, true);
        }

        void OnRendered(BitmapSource b) { preview.Source = b; }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr h = new WindowInteropHelper(this).Handle;
            int dark = 1; Native.DwmSetWindowAttribute(h, 20, ref dark, 4);                    // dark title bar
            bool ok = false;
            if (Native.WinBuild() >= 22621)                                                   // Windows 11 22H2+: real acrylic behind the window
            {
                try
                {
                    var hs = HwndSource.FromHwnd(h); hs.CompositionTarget.BackgroundColor = Colors.Transparent;
                    Native.MARGINS m = new Native.MARGINS(); m.L = -1; m.R = -1; m.T = -1; m.B = -1;
                    int r1 = Native.DwmExtendFrameIntoClientArea(h, ref m);
                    int bt = 3; int r2 = Native.DwmSetWindowAttribute(h, 38, ref bt, 4);
                    ok = r1 == 0 && r2 == 0;
                }
                catch { ok = false; }
            }
            Background = Util.Solid(ok ? Color.FromArgb(0x5C, 14, 16, 24) : Color.FromRgb(21, 23, 31));
        }

        public void Reload() { ShowTab(tab, false); }

        // ------------------------------------------------------------------ pages
        void ShowTab(int i, bool animate)
        {
            tab = i;
            page = new StackPanel(); page.Margin = new Thickness(16, 4, 16, 16);
            switch (i)
            {
                case 0: Look(); break;
                case 1: ContentTab(); break;
                case 2: TextTab(); break;
                default: Sys(); break;
            }
            double off = animate ? 0 : sv.VerticalOffset;
            sv.Content = page;
            if (!animate) { sv.ScrollToVerticalOffset(off); return; }
            sv.ScrollToTop();
            if (!C.Animations) return;
            var ease = new CubicEase(); ease.EasingMode = EasingMode.EaseOut;
            int k = 0;
            foreach (UIElement el in page.Children)
            {
                var tt = new TranslateTransform(0, 22); el.RenderTransform = tt; el.Opacity = 0;
                var ao = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)); ao.BeginTime = TimeSpan.FromMilliseconds(k * 60); ao.EasingFunction = ease;
                var ay = new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(420)); ay.BeginTime = TimeSpan.FromMilliseconds(k * 60); ay.EasingFunction = ease;
                el.BeginAnimation(UIElement.OpacityProperty, ao);
                tt.BeginAnimation(TranslateTransform.YProperty, ay);
                k++;
            }
        }

        // ------------------------------------------------------------------ row builders
        void Begin(string title)
        {
            var card = new Border();
            card.CornerRadius = new CornerRadius(24); card.Padding = new Thickness(18, 14, 18, 12); card.Margin = new Thickness(0, 0, 0, 14);
            card.Background = new LinearGradientBrush(Util.White(0.10), Util.White(0.04), 90);
            card.BorderBrush = new LinearGradientBrush(Util.White(0.30), Util.White(0.07), 90); card.BorderThickness = new Thickness(1);
            cur = new StackPanel();
            if (title != null)
            {
                var t = new TextBlock { Text = title.ToUpper(), FontSize = 11, FontWeight = FontWeights.SemiBold, Opacity = 0.55, Margin = new Thickness(0, 0, 0, 6), Foreground = Brushes.White };
                cur.Children.Add(t);
            }
            card.Child = cur; page.Children.Add(card);
        }

        TextBlock Lbl(string text)
        {
            var l = new TextBlock { Text = text, Foreground = Brushes.White, Opacity = 0.92, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            return l;
        }

        void LabelAbove(string text)
        {
            var l = Lbl(text); l.Margin = new Thickness(0, 8, 0, 6); cur.Children.Add(l);
        }

        static string Fmt(double v, string unit) { return ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture) + unit; }

        void Note(string t)
        {
            var tb = new TextBlock { Text = t, FontSize = 12, Opacity = 0.55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 2), Foreground = Brushes.White };
            cur.Children.Add(tb);
        }

        void Slider(string label, double min, double max, double val, double def, string unit, Action<double> set, int flags)
        {
            var g = new Grid(); g.Margin = new Thickness(0, 2, 0, 2);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            var l = Lbl(label);
            var vt = new TextBlock { Text = Fmt(val, unit), Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Foreground = Brushes.White };
            var s = new GlassSlider(min, max, val, def);
            s.Changed += delegate(double v) { set(v); vt.Text = Fmt(v, unit); d.Refresh(flags); };
            Grid.SetColumn(s, 1); Grid.SetColumn(vt, 2);
            g.Children.Add(l); g.Children.Add(s); g.Children.Add(vt);
            cur.Children.Add(g);
        }

        void Toggle(string label, bool val, Action<bool> set, int flags)
        {
            var g = new Grid(); g.Margin = new Thickness(0, 4, 0, 4);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = Lbl(label); l.Margin = new Thickness(0, 0, 12, 0);
            var t = new GlassToggle(val);
            t.Changed += delegate(bool v) { set(v); d.Refresh(flags); };
            Grid.SetColumn(t, 1);
            g.Children.Add(l); g.Children.Add(t);
            cur.Children.Add(g);
        }

        void Seg(string label, string[] shown, string[] vals, string curVal, Action<string> set, int flags, bool reload)
        {
            LabelAbove(label);
            int sel = Math.Max(0, Array.IndexOf(vals, curVal));
            var s = new GlassSegmented(shown, sel);
            s.Changed += delegate(int i) { set(vals[i]); d.Refresh(flags); if (reload) ShowTab(tab, false); };
            cur.Children.Add(s);
        }

        void Chips(string label, string[] shown, string[] vals, string curVal, Action<string> set, int flags, bool reload)
        {
            LabelAbove(label);
            var wp = new WrapPanel();
            for (int i = 0; i < vals.Length; i++)
            {
                string v = vals[i];
                var b = new GlassButton(shown[i]); b.Selected = v == curVal;
                b.Click += delegate { set(v); d.Refresh(flags); if (reload) ShowTab(tab, false); };
                wp.Children.Add(b);
            }
            cur.Children.Add(wp);
        }

        TextBox Box(string text, double width)
        {
            return new TextBox { Text = text, Width = width, Foreground = Brushes.White, CaretBrush = Brushes.White, Background = Util.Solid(Util.White(0.08)),
                BorderBrush = Util.Solid(Util.White(0.22)), Padding = new Thickness(9, 6, 9, 6), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2) };
        }

        void SwatchRow(string label, string curHex, Action<string> set, int flags)
        {
            LabelAbove(label);
            var wp = new WrapPanel();
            foreach (string hx in Swatches)
            {
                string h2 = hx;
                var sw = new GlassSwatch(h2, string.Equals(h2, curHex, StringComparison.OrdinalIgnoreCase));
                sw.Picked += delegate(string v) { set(v); d.Refresh(flags); ShowTab(tab, false); };
                wp.Children.Add(sw);
            }
            var hb = Box(curHex, 92); hb.Margin = new Thickness(6, 0, 0, 0);
            hb.LostFocus += delegate { string v = hb.Text.Trim(); if (v.Length == 7 && v[0] == '#') { set(v); d.Refresh(flags); ShowTab(tab, false); } };
            wp.Children.Add(hb);
            cur.Children.Add(wp);
        }

        void TextRow(string label, string val, Action<string> set, int flags, double width)
        {
            LabelAbove(label);
            var tb = Box(val, width);
            tb.LostFocus += delegate { set(tb.Text.Trim()); d.Refresh(flags); };
            tb.KeyDown += delegate(object o, KeyEventArgs e) { if (e.Key == Key.Enter) { set(tb.Text.Trim()); d.Refresh(flags); } };
            cur.Children.Add(tb);
        }

        void FontRow(string label, string curName, Action<string> set)
        {
            LabelAbove(label);
            var btn = new GlassButton(curName);
            var lb = new ListBox { MaxHeight = 340, Width = 300, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White };
            lb.Resources[SystemColors.HighlightBrushKey] = Util.Solid(Util.White(0.22));
            lb.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = Util.Solid(Util.White(0.22));
            lb.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
            lb.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Brushes.White;
            foreach (string n in FontBook.Names) lb.Items.Add(n);
            lb.SelectedItem = curName;
            var box = new Border { Background = Util.Solid(Color.FromArgb(0xF2, 24, 26, 36)), BorderBrush = Util.Solid(Util.White(0.25)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(8), Child = lb };
            var pop = new Popup { PlacementTarget = btn, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = box };
            btn.Click += delegate { pop.IsOpen = true; if (lb.SelectedItem != null) lb.ScrollIntoView(lb.SelectedItem); };
            lb.SelectionChanged += delegate
            {
                if (lb.SelectedItem == null || !pop.IsOpen) return;
                string n = (string)lb.SelectedItem;
                set(n); btn.Label = n; btn.InvalidateMeasure(); btn.InvalidateVisual();
                d.Refresh(F.Layout); pop.IsOpen = false;
            };
            cur.Children.Add(btn);
        }

        void Buttons(params GlassButton[] bs)
        {
            var wp = new WrapPanel(); wp.Margin = new Thickness(0, 6, 0, 0);
            foreach (GlassButton b in bs) wp.Children.Add(b);
            cur.Children.Add(wp);
        }

        GlassButton Btn(string label, Action a, bool danger)
        {
            var b = new GlassButton(label); b.Danger = danger; b.Click += delegate { a(); }; return b;
        }

        // ------------------------------------------------------------------ tabs
        void Look()
        {
            Begin("Glass");
            Seg("Material", new string[] { "Regular  (frosted)", "Clear  (transparent)" }, new string[] { "regular", "clear" }, C.Variant,
                delegate(string v) { Presets.ApplyVariant(C, v); }, F.Vis, true);
            Note("Regular stays legible on any wallpaper. Clear lets the wallpaper through and dims it slightly so the glass stays readable.");
            string[] pn = new string[Presets.Names.Length];
            for (int i = 0; i < pn.Length; i++) pn[i] = Presets.Names[i].Substring(0, 1).ToUpper() + Presets.Names[i].Substring(1);
            Chips("Style", pn, Presets.Names, C.Theme, delegate(string v) { Presets.Apply(C, v); }, F.Vis, true);

            Begin("Tint");
            SwatchRow("Tint colour", C.TintColor, delegate(string v) { C.TintColor = v; C.Theme = "custom"; }, F.Vis);
            Slider("Tint amount", 0, 100, C.Tint, 0, "%", delegate(double v) { C.Tint = v; C.Theme = "custom"; }, F.Vis);
            SwatchRow("Light spill colour", C.Accent, delegate(string v) { C.Accent = v; C.Theme = "custom"; }, F.Vis);

            Begin("Material");
            Slider("Frost (lightness)", 0, 100, C.Veil, 45, "%", delegate(double v) { C.Veil = v; }, F.Vis);
            Slider("Dimming (Clear)", 0, 100, C.Dim, 0, "%", delegate(double v) { C.Dim = v; }, F.Vis);
            Slider("Lensing / edge depth", 0, 100, C.Lens, 70, "%", delegate(double v) { C.Lens = v; }, F.Vis);
            Slider("Edge highlights", 0, 100, C.Rim, 85, "%", delegate(double v) { C.Rim = v; }, F.Vis);
            Slider("Sheen streak", 0, 100, C.Sheen, 55, "%", delegate(double v) { C.Sheen = v; }, F.Vis);
            Slider("Light direction", 0, 360, C.Light, 135, "°", delegate(double v) { C.Light = v; }, F.Vis);
            Slider("Colour dispersion", 0, 100, C.Disp, 25, "%", delegate(double v) { C.Disp = v; }, F.Vis);
            Slider("Colour spill", 0, 100, C.Spill, 35, "%", delegate(double v) { C.Spill = v; }, F.Vis);
            Slider("Shadow", 0, 100, C.Shadow, 45, "%", delegate(double v) { C.Shadow = v; }, F.Vis);
            Slider("Overall opacity", 10, 100, C.Opacity, 100, "%", delegate(double v) { C.Opacity = v; }, F.Mode);
            Note("Double-click any slider to reset it. The small tick marks the default. Light direction 135° = light from the top-left.");
        }

        void ContentTab()
        {
            Begin("Big glass text");
            Seg("Show", new string[] { "Day", "Time", "Date" }, new string[] { "Day", "Time", "Date" }, C.Big, delegate(string v) { C.Big = v; }, F.Layout, false);
            string[] cs = { "upper", "normal", "lower" }, cn = { "UPPERCASE", "Normal", "lowercase" };
            Seg("Letter case", cn, cs, C.BigCase, delegate(string v) { C.BigCase = v; }, F.Layout, false);

            Begin("Underneath");
            Toggle("Day name", C.ShowDay, delegate(bool v) { C.ShowDay = v; }, F.Layout);
            Toggle("Date", C.ShowDate, delegate(bool v) { C.ShowDate = v; }, F.Layout);
            Toggle("Time", C.ShowTime, delegate(bool v) { C.ShowTime = v; }, F.Layout);
            Toggle("Dashes around the time   - 12:06 -", C.Dashes, delegate(bool v) { C.Dashes = v; }, F.Layout);
            Toggle("Thin glass divider", C.Divider, delegate(bool v) { C.Divider = v; }, F.Layout);
            Toggle("Make the small text glass too", C.SmallGlass, delegate(bool v) { C.SmallGlass = v; }, F.Layout);
            Seg("Small text case", cn, cs, C.SmallCase, delegate(string v) { C.SmallCase = v; }, F.Layout, false);

            Begin("Time");
            Toggle("24-hour clock", C.Clock24, delegate(bool v) { C.Clock24 = v; }, F.Layout);
            Toggle("Show AM / PM (12-hour)", C.AmPm, delegate(bool v) { C.AmPm = v; }, F.Layout);
            Toggle("Show seconds", C.Seconds, delegate(bool v) { C.Seconds = v; }, F.Layout);

            Begin("Date format");
            string[] shown = new string[DateFormats.Length];
            for (int i = 0; i < shown.Length; i++) shown[i] = DateTime.Now.ToString(DateFormats[i], CultureInfo.CurrentCulture);
            Chips("Presets", shown, DateFormats, C.DateFormat, delegate(string v) { C.DateFormat = v; }, F.Layout, true);
            TextRow("Custom format", C.DateFormat, delegate(string v) { if (v.Length > 0) C.DateFormat = v; }, F.Layout, 240);
            Note("Codes: dddd = weekday, MMMM = month, MMM = short month, dd / d = day, yyyy = year.");
        }

        void TextTab()
        {
            Begin("Fonts");
            FontRow("Big text font", C.Font, delegate(string v) { C.Font = v; });
            FontRow("Small text font", C.SmallFont, delegate(string v) { C.SmallFont = v; });
            Buttons(Btn("Import font file…", ImportFont, false));
            Note("Poppins (bundled) is the default - a wide geometric face whose thick strokes show the glass best. Any installed font works; imported fonts are remembered.");

            Begin("Big text");
            Slider("Size", 30, 400, C.BigSize, 110, " px", delegate(double v) { C.BigSize = v; }, F.Layout);
            Slider("Weight  (100 thin - 900 black)", 100, 900, C.BigWeight, 700, "", delegate(double v) { C.BigWeight = v; }, F.Layout);
            Slider("Letter spacing", 0, 100, C.BigSpace, 18, "%", delegate(double v) { C.BigSpace = v; }, F.Layout);

            Begin("Small text");
            Slider("Size", 8, 100, C.SmallSize, 22, " px", delegate(double v) { C.SmallSize = v; }, F.Layout);
            Slider("Weight", 100, 900, C.SmallWeight, 500, "", delegate(double v) { C.SmallWeight = v; }, F.Layout);
            Slider("Letter spacing", 0, 100, C.SmallSpace, 16, "%", delegate(double v) { C.SmallSpace = v; }, F.Layout);
            SwatchRow("Solid text colour", C.SmallColor, delegate(string v) { C.SmallColor = v; }, F.Vis);
            Slider("Solid text opacity", 20, 100, C.SmallOpacity, 92, "%", delegate(double v) { C.SmallOpacity = v; }, F.Vis);

            Begin("Spacing");
            Slider("Space between lines", 20, 300, C.Gap, 100, "%", delegate(double v) { C.Gap = v; }, F.Layout);
        }

        void ImportFont()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog(); dlg.Filter = "Fonts|*.ttf;*.otf"; dlg.Multiselect = true;
            if (dlg.ShowDialog() != true) return;
            var old = new List<string>(FontBook.Names);
            foreach (string f in dlg.FileNames)
            {
                try { File.Copy(f, Path.Combine(Paths.Fonts, Path.GetFileName(f)), true); } catch (Exception e) { Logger.Write("import font: " + e.Message); }
            }
            FontBook.Reload();
            foreach (string n in FontBook.Names) if (!old.Contains(n)) { C.Font = n; break; }
            d.Refresh(F.Layout);
            ShowTab(tab, false);
        }

        void Sys()
        {
            Begin("Window");
            Seg("Layer", new string[] { "Desktop", "Embedded", "Normal", "On top" }, new string[] { "desktop", "embedded", "normal", "top" }, C.Layer,
                delegate(string v) { C.Layer = v; }, F.Mode, false);
            Note("Desktop: just above the wallpaper (Razer Axon, Wallpaper Engine, Lively...) and below every app - recommended. Embedded: experimental, lives inside the desktop like a wallpaper layer (not draggable). Normal / On top: ordinary windows.");
            Toggle("Treat borderless full-screen background windows as wallpaper", C.GenericHost, delegate(bool v) { C.GenericHost = v; }, F.Mode);
            TextRow("Wallpaper app process names (separated by ;)", C.WallpaperApps, delegate(string v) { C.WallpaperApps = v; }, F.Mode, 340);
            Toggle("Lock position (no dragging)", C.Locked, delegate(bool v) { C.Locked = v; }, 0);
            Toggle("Click-through (mouse passes through the clock)", C.ClickThrough, delegate(bool v) { C.ClickThrough = v; }, F.Mode);
            Toggle("Start with Windows", Autostart.IsOn(), delegate(bool v) { Autostart.Set(v); }, 0);

            Begin("Position");
            double px, py; d.GetPosPercent(out px, out py);
            double[] pos = new double[] { px, py };
            Slider("Horizontal", 0, 100, px, 50, "%", delegate(double v) { pos[0] = v; d.SetPosPercent(pos[0], pos[1]); }, 0);
            Slider("Vertical", 0, 100, py, 30, "%", delegate(double v) { pos[1] = v; d.SetPosPercent(pos[0], pos[1]); }, 0);
            Note("You can also drag the clock, or use the mouse wheel over it to resize.");

            Begin("Motion");
            Toggle("Animations (materialize, minute morph, this menu)", C.Animations, delegate(bool v) { C.Animations = v; }, 0);
            Toggle("Interactive glass (clock squeezes slightly while you drag it)", C.Interactive, delegate(bool v) { C.Interactive = v; }, 0);

            Begin("Actions");
            Buttons(Btn("Re-centre", delegate { d.Recentre(); }, false),
                    Btn("Diagnostics…", delegate { d.ShowDiagnostics(); }, false),
                    Btn("Reset all", delegate { d.ResetAll(); ShowTab(tab, false); }, true),
                    Btn("Quit GlassClock", delegate { d.Quit(); }, true));
            Note("If the clock is ever invisible: Diagnostics shows the window stack - the clock should sit directly above the [HOST] windows. Try Layer = Normal to confirm it is running.");
        }
    }
}
