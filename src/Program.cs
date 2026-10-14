using System;
using System.Windows;
using System.Windows.Threading;

namespace GlassClock
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            var mutex = new System.Threading.Mutex(true, "GlassClock_SingleInstance_v3", out created);
            if (!created) return;

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // never crash (a crashed desktop widget just "disappears"): log and carry on
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                Logger.Write("UI error: " + e.Exception); e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Logger.Write("Fatal: " + e.ExceptionObject);
            };

            FontBook.ExtractBundled();
            FontBook.Reload();
            var cfg = new Cfg();
            cfg.Load(Paths.ConfigFile);

            app.Run(new ClockWindow(cfg));
            GC.KeepAlive(mutex);
        }
    }
}
