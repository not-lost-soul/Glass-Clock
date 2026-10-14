using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace GlassClock
{
    /// <summary>Critically-tunable spring (Apple-style fluid motion: a little overshoot, then settles).</summary>
    class Spring
    {
        public double X, V, T;
        public Spring(double v) { X = v; T = v; }

        public void Step(double dt, double k, double d)
        {
            int n = (int)Math.Ceiling(dt / 0.008); if (n < 1) n = 1;
            double h = dt / n;
            for (int i = 0; i < n; i++) { V += (k * (T - X) - d * V) * h; X += V * h; }
        }

        public bool Rest { get { return Math.Abs(X - T) < 0.0008 && Math.Abs(V) < 0.02; } }
    }

    /// <summary>One shared frame loop. It only runs while some control is still moving, so the UI is idle (0% CPU) otherwise.</summary>
    static class Motion
    {
        static readonly List<Func<double, bool>> steps = new List<Func<double, bool>>();
        static bool on;
        static long last;

        public static void Run(Func<double, bool> step)
        {
            steps.Add(step);
            if (on) return;
            on = true; last = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += Frame;
        }

        static void Frame(object s, EventArgs e)
        {
            long now = Stopwatch.GetTimestamp();
            double dt = (now - last) / (double)Stopwatch.Frequency;
            if (dt < 0.002) return;
            last = now; if (dt > 0.05) dt = 0.05;
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                bool keep;
                try { keep = steps[i](dt); } catch { keep = false; }
                if (!keep) steps.RemoveAt(i);
            }
            if (steps.Count == 0) { CompositionTarget.Rendering -= Frame; on = false; }
        }
    }

    abstract class MotionControl : FrameworkElement
    {
        bool running;

        protected MotionControl() { SnapsToDevicePixels = true; }

        protected void Wake()
        {
            if (running) return;
            running = true; Motion.Run(Tick);
        }

        bool Tick(double dt)
        {
            bool more;
            try { more = Step(dt); } catch { more = false; }
            InvalidateVisual();
            if (!more) running = false;
            return more;
        }

        protected abstract bool Step(double dt);
    }
}
