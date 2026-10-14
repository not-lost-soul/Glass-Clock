using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GlassClock
{
    /// <summary>
    /// The desktop clock. The glass is rendered ONCE into a bitmap whenever the text or a setting changes (about once a minute),
    /// so the window is idle the rest of the time: no capture, no per-frame drawing, no lag.
    /// Animations (materialize, minute cross-fade, press) only move that cached bitmap.
    /// </summary>
    class ClockWindow : Window
    {
        public Cfg C;
        public BitmapSource LastBitmap;
        public event Action<BitmapSource> Rendered;

        Grid root; Image imgCur, imgPrev;
        ScaleTransform stCur = new ScaleTransform(1, 1);
        IntPtr hwnd = IntPtr.Zero;
        bool shown, dragging;
        string lastKey = "";
        DispatcherTimer tick, place, saveT, rebuildT;
        SettingsWindow settings;
        Tray tray;
        MenuItem lockMi;
        Native.POINT dragStart; int dragX, dragY;

        public ClockWindow(Cfg c)
        {
            C = c;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = null;
            ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; ShowActivated = false; Topmost = false;
            WindowStartupLocation = WindowStartupLocation.Manual; Title = "GlassClock"; SnapsToDevicePixels = true;

            root = new Grid(); root.Background = Brushes.Transparent;
            imgPrev = MkImage(); imgCur = MkImage();
            imgCur.RenderTransformOrigin = new Point(0.5, 0.5); imgCur.RenderTransform = stCur;
            root.Children.Add(imgPrev); root.Children.Add(imgCur);
            Content = root;

            Rebuild(false);
            PlaceInitial();
            BuildMenu();

            root.MouseLeftButtonDown += OnDown;
            root.MouseMove += OnMove;
            root.MouseLeftButtonUp += OnUp;
            root.MouseWheel += OnWheel;

            tick = new DispatcherTimer(); tick.Interval = TimeSpan.FromSeconds(1);
            tick.Tick += delegate { if (GlassText.Key(C) != lastKey) Rebuild(true); };
            tick.Start();

            place = new DispatcherTimer(); place.Interval = TimeSpan.FromSeconds(2);
            place.Tick += delegate
            {
                if (C.Layer == "desktop" && hwnd != IntPtr.Zero && !dragging) Placement.PlaceDesktop(hwnd, C.WallpaperApps, C.GenericHost);
            };
            place.Start();

            saveT = new DispatcherTimer(); saveT.Interval = TimeSpan.FromMilliseconds(600);
            saveT.Tick += delegate { saveT.Stop(); C.Save(Paths.ConfigFile); };

            rebuildT = new DispatcherTimer(); rebuildT.Interval = TimeSpan.FromMilliseconds(35);
            rebuildT.Tick += delegate { rebuildT.Stop(); Rebuild(false); };

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate { ApplyMode(); }));
            };

            tray = new Tray(this);
        }

        static Image MkImage()
        {
            var i = new Image(); i.Stretch = Stretch.None; i.HorizontalAlignment = HorizontalAlignment.Left;
            i.VerticalAlignment = VerticalAlignment.Top; i.IsHitTestVisible = false;
            RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.HighQuality);
            return i;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new WindowInteropHelper(this).Handle;
            ApplyMode();
            shown = true;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ApplyMode();                      // placement needs a visible window
            Materialize();
        }

        // ------------------------------------------------------------------ rendering
        void Rebuild(bool animate)
        {
            try
            {
                GlassLayout L = GlassText.Build(C);
                GlassStyle S = GlassStyle.From(C);
                double sc = Util.Scale();
                int pw = Math.Max(1, (int)Math.Ceiling(L.W * sc)), ph = Math.Max(1, (int)Math.Ceiling(L.H * sc));
                var dv = new DrawingVisual();
                using (DrawingContext dc = dv.RenderOpen()) GlassRenderer.Draw(dc, L, S);
                var rtb = new RenderTargetBitmap(pw, ph, 96 * sc, 96 * sc, PixelFormats.Pbgra32);
                rtb.Render(dv); rtb.Freeze();

                ApplySize(pw / sc, ph / sc);
                SwapImage(rtb, animate && C.Animations && shown);
                lastKey = GlassText.Key(C);
                LastBitmap = rtb;
                if (Rendered != null) Rendered(rtb);
            }
            catch (Exception e) { Logger.Write("Rebuild: " + e); }
        }

        void ApplySize(double w, double h)
        {
            if (double.IsNaN(Width) || double.IsNaN(Height)) { Width = w; Height = h; return; }
            if (Math.Abs(w - Width) < 0.5 && Math.Abs(h - Height) < 0.5) return;
            double nx = C.X + (Width - w) / 2, ny = C.Y + (Height - h) / 2;      // keep the centre fixed when the size changes
            Width = w; Height = h;
            if (C.X > -50000) MoveToDip(nx, ny);
            if (hwnd != IntPtr.Zero && Placement.IsEmbedded(hwnd)) ReapplyEmbeddedGeometry();
        }

        void SwapImage(BitmapSource bmp, bool animate)
        {
            if (!animate || imgCur.Source == null)
            {
                imgCur.BeginAnimation(UIElement.OpacityProperty, null); imgPrev.BeginAnimation(UIElement.OpacityProperty, null);
                imgCur.Source = bmp; imgPrev.Source = null; imgCur.Opacity = 1; imgPrev.Opacity = 0;
                return;
            }
            // liquid cross-fade: the new frame materialises while the old one dissolves
            imgPrev.Source = imgCur.Source; imgPrev.Opacity = 1;
            imgCur.Source = bmp;
            var ease = new CubicEase(); ease.EasingMode = EasingMode.EaseOut;
            var dur = TimeSpan.FromMilliseconds(460);
            var a1 = new DoubleAnimation(1, 0, dur); a1.EasingFunction = ease;
            var a2 = new DoubleAnimation(0, 1, dur); a2.EasingFunction = ease;
            imgPrev.BeginAnimation(UIElement.OpacityProperty, a1);
            imgCur.BeginAnimation(UIElement.OpacityProperty, a2);
            var s1 = new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(560)); s1.EasingFunction = ease;
            var s2 = new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(560)); s2.EasingFunction = ease;
            stCur.BeginAnimation(ScaleTransform.ScaleXProperty, s1);
            stCur.BeginAnimation(ScaleTransform.ScaleYProperty, s2);
        }

        void Materialize()
        {
            if (!C.Animations) return;
            var ease = new CubicEase(); ease.EasingMode = EasingMode.EaseOut;
            var o = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800)); o.EasingFunction = ease;
            var sx = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(900)); sx.EasingFunction = ease;
            var sy = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(900)); sy.EasingFunction = ease;
            imgCur.BeginAnimation(UIElement.OpacityProperty, o);
            stCur.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            stCur.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        }

        void Press(bool down)
        {
            if (down)
            {
                var ease = new CubicEase(); ease.EasingMode = EasingMode.EaseOut;
                var a = new DoubleAnimation(1.02, TimeSpan.FromMilliseconds(140)); a.EasingFunction = ease;
                var b = new DoubleAnimation(1.02, TimeSpan.FromMilliseconds(140)); b.EasingFunction = ease;
                stCur.BeginAnimation(ScaleTransform.ScaleXProperty, a); stCur.BeginAnimation(ScaleTransform.ScaleYProperty, b);
            }
            else
            {
                var el = new ElasticEase(); el.EasingMode = EasingMode.EaseOut; el.Oscillations = 2; el.Springiness = 6;
                var a = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(560)); a.EasingFunction = el;
                var b = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(560)); b.EasingFunction = el;
                stCur.BeginAnimation(ScaleTransform.ScaleXProperty, a); stCur.BeginAnimation(ScaleTransform.ScaleYProperty, b);
            }
        }

        void SaveSoon()
        {
            if (saveT == null) return;
            saveT.Stop(); saveT.Start();
        }

        // ------------------------------------------------------------------ position
        public void MoveToDip(double x, double y)
        {
            C.X = x; C.Y = y;
            if (hwnd == IntPtr.Zero) { Left = x; Top = y; return; }
            double sc = Util.Scale();
            Placement.MoveToScreenPx(hwnd, (int)Math.Round(x * sc), (int)Math.Round(y * sc));
        }

        void ReapplyEmbeddedGeometry()
        {
            double sc = Util.Scale();
            Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, (int)Math.Round(Width * sc), (int)Math.Round(Height * sc),
                Native.SWP_NOMOVE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            MoveToDip(C.X, C.Y);
        }

        void PlaceInitial()
        {
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            bool ok = C.X > -50000 && C.X > vl - Width + 40 && C.X < vl + vw - 40 && C.Y > vt - 10 && C.Y < vt + vh - 40;
            if (ok) { Left = C.X; Top = C.Y; } else Recentre();
        }

        public void Recentre()
        {
            MoveToDip((SystemParameters.PrimaryScreenWidth - Width) / 2, SystemParameters.PrimaryScreenHeight * 0.30 - Height / 2);
            SaveSoon();
        }

        public void SetPosPercent(double px, double py)
        {
            double sw = SystemParameters.PrimaryScreenWidth - Width, sh = SystemParameters.PrimaryScreenHeight - Height;
            MoveToDip(sw * px / 100.0, sh * py / 100.0);
            SaveSoon();
        }

        public void GetPosPercent(out double px, out double py)
        {
            double sw = SystemParameters.PrimaryScreenWidth - Width, sh = SystemParameters.PrimaryScreenHeight - Height;
            px = sw > 1 ? Util.Clamp(C.X / sw * 100.0, 0, 100) : 50;
            py = sh > 1 ? Util.Clamp(C.Y / sh * 100.0, 0, 100) : 30;
        }

        // ------------------------------------------------------------------ window mode (layer, click-through, opacity)
        public void ApplyMode()
        {
            Opacity = Math.Max(0.1, C.Opacity / 100.0);
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
                ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                if (C.ClickThrough) ex |= Native.WS_EX_TRANSPARENT; else ex &= ~Native.WS_EX_TRANSPARENT;
                Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);

                string layer = C.Layer;
                if (layer == "embedded")
                {
                    if (!Placement.IsEmbedded(hwnd))
                    {
                        Topmost = false;
                        if (Placement.Embed(hwnd)) ReapplyEmbeddedGeometry();
                        else { Logger.Write("embed failed - falling back to desktop layer"); layer = "desktop"; C.Layer = "desktop"; }
                    }
                    if (layer == "embedded") return;
                }
                if (Placement.IsEmbedded(hwnd)) { Placement.Detach(hwnd); MoveToDip(C.X, C.Y); }

                if (layer == "top") Topmost = true;
                else
                {
                    Topmost = false;
                    if (layer == "normal")
                        Native.SetWindowPos(hwnd, Native.HWND_NOTOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                    else Placement.PlaceDesktop(hwnd, C.WallpaperApps, C.GenericHost);
                }
            }
            catch (Exception e) { Logger.Write("ApplyMode: " + e.Message); }
        }

        public void Refresh(int f)
        {
            SaveSoon();
            if ((f & F.Mode) != 0) ApplyMode();
            if ((f & (F.Vis | F.Layout)) != 0) { rebuildT.Stop(); rebuildT.Start(); }
            if (lockMi != null) lockMi.IsChecked = C.Locked;
        }

        public void ApplyPreset(string n) { Presets.Apply(C, n); Refresh(F.Vis); }
        public void ApplyVariant(string v) { Presets.ApplyVariant(C, v); Refresh(F.Vis); }

        public void ResetAll()
        {
            C.ResetKeepingPosition();
            Refresh(F.Vis | F.Layout | F.Mode);
        }

        // ------------------------------------------------------------------ mouse
        void OnDown(object s, MouseButtonEventArgs e)
        {
            if (C.Locked || Placement.IsEmbedded(hwnd)) return;
            dragging = true; Native.GetCursorPos(out dragStart);
            Native.RECT r; Native.GetWindowRect(hwnd, out r); dragX = r.L; dragY = r.T;
            root.CaptureMouse();
            if (C.Animations && C.Interactive) Press(true);
        }

        void OnMove(object s, MouseEventArgs e)
        {
            if (!dragging) return;
            Native.POINT p; Native.GetCursorPos(out p);
            Placement.MoveToScreenPx(hwnd, dragX + p.X - dragStart.X, dragY + p.Y - dragStart.Y);
        }

        void OnUp(object s, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false; root.ReleaseMouseCapture();
            Native.RECT r; Native.GetWindowRect(hwnd, out r);
            double sc = Util.Scale();
            C.X = r.L / sc; C.Y = r.T / sc;
            SaveSoon();
            if (C.Animations && C.Interactive) Press(false);
            if (C.Layer == "desktop") Placement.PlaceDesktop(hwnd, C.WallpaperApps, C.GenericHost);
        }

        void OnWheel(object s, MouseWheelEventArgs e)
        {
            double f = e.Delta > 0 ? 1.05 : 1 / 1.05;
            C.BigSize = Util.Clamp(C.BigSize * f, 30, 400);
            C.SmallSize = Util.Clamp(C.SmallSize * f, 8, 100);
            Refresh(F.Layout);
            if (settings != null) settings.Reload();
        }

        // ------------------------------------------------------------------ menu / settings / diagnostics
        MenuItem Mi(string h, Action a)
        {
            var m = new MenuItem(); m.Header = h; m.Click += delegate { a(); }; return m;
        }

        void BuildMenu()
        {
            var cm = new ContextMenu();
            cm.Items.Add(Mi("Settings…", delegate { OpenSettings(); }));
            lockMi = new MenuItem(); lockMi.Header = "Lock position"; lockMi.IsCheckable = true; lockMi.IsChecked = C.Locked;
            lockMi.Click += delegate { C.Locked = lockMi.IsChecked; Refresh(0); };
            cm.Items.Add(lockMi);
            cm.Items.Add(Mi("Re-centre", delegate { Recentre(); }));
            cm.Items.Add(Mi("Diagnostics…", delegate { ShowDiagnostics(); }));
            cm.Items.Add(new Separator());
            cm.Items.Add(Mi("Quit GlassClock", delegate { Quit(); }));
            root.ContextMenu = cm;
        }

        public void OpenSettings()
        {
            if (settings != null) { settings.Activate(); return; }
            settings = new SettingsWindow(this);
            settings.Closed += delegate { settings = null; C.Save(Paths.ConfigFile); };
            settings.Show();
        }

        public void ShowDiagnostics()
        {
            try
            {
                File.WriteAllText(Paths.DiagFile, Placement.Dump(hwnd, C.WallpaperApps, C.GenericHost));
                Process.Start("notepad.exe", Paths.DiagFile);
            }
            catch (Exception e) { Logger.Write("diag: " + e.Message); }
        }

        public void Quit()
        {
            C.Save(Paths.ConfigFile);
            try { if (tray != null) tray.Dispose(); } catch { }
            Application.Current.Shutdown();
        }
    }
}
