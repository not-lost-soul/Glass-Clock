using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GlassClock
{
    /// <summary>System-tray icon (bottom-right) with the quick menu.</summary>
    class Tray : IDisposable
    {
        System.Windows.Forms.NotifyIcon ni;
        ClockWindow w;

        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(System.Drawing.Color.Transparent);
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), System.Drawing.Color.FromArgb(190, 215, 255), System.Drawing.Color.FromArgb(60, 110, 230), 90f))
                    g.FillEllipse(br, 1, 1, 30, 30);
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2.5f))
                {
                    g.DrawEllipse(pen, 1, 1, 30, 30); g.DrawLine(pen, 16, 16, 16, 7); g.DrawLine(pen, 16, 16, 23, 19);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public Tray(ClockWindow window)
        {
            w = window;
            try
            {
                ni = new System.Windows.Forms.NotifyIcon();
                ni.Icon = MakeIcon(); ni.Text = "GlassClock"; ni.Visible = true;
                var cms = new System.Windows.Forms.ContextMenuStrip();
                cms.Items.Add("Settings…", null, delegate { w.OpenSettings(); });

                var lockItem = new System.Windows.Forms.ToolStripMenuItem("Lock position"); lockItem.CheckOnClick = true;
                lockItem.Click += delegate { w.C.Locked = lockItem.Checked; w.Refresh(0); };
                cms.Items.Add(lockItem);

                var layer = new System.Windows.Forms.ToolStripMenuItem("Layer");
                string[] ids = { "desktop", "embedded", "normal", "top" };
                string[] names = { "Desktop (above wallpaper, below apps)", "Embedded in desktop (experimental)", "Normal window", "Always on top" };
                var items = new System.Windows.Forms.ToolStripMenuItem[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    var it = new System.Windows.Forms.ToolStripMenuItem(names[i]);
                    it.Click += delegate { w.C.Layer = id; w.Refresh(F.Mode); };
                    items[i] = it; layer.DropDownItems.Add(it);
                }
                cms.Items.Add(layer);
                cms.Items.Add("Re-centre", null, delegate { w.Recentre(); });
                cms.Items.Add("Diagnostics…", null, delegate { w.ShowDiagnostics(); });
                cms.Items.Add("Quit", null, delegate { w.Quit(); });
                cms.Opening += delegate
                {
                    lockItem.Checked = w.C.Locked;
                    for (int i = 0; i < ids.Length; i++) items[i].Checked = w.C.Layer == ids[i];
                };
                ni.ContextMenuStrip = cms;
                ni.DoubleClick += delegate { w.OpenSettings(); };
            }
            catch (Exception e) { Logger.Write("tray: " + e.Message); }
        }

        public void Dispose()
        {
            if (ni != null) { ni.Visible = false; ni.Dispose(); ni = null; }
        }
    }
}
