using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowBall
{
    internal static class SelfTests
    {
        private static readonly List<string> results = new List<string>();
        internal static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static BallPhysics Ball()
        {
            return new BallPhysics { Arena = new Rectangle(-1200, -200, 1920, 1080), Radius = 40, X = 0, Y = 100 };
        }
        private static void Test(string name, Action action)
        {
            try { action(); results.Add("PASS " + name); }
            catch (Exception exception) { results.Add("FAIL " + name + ": " + exception.Message); }
        }
        internal static int Run(string path)
        {
            results.Clear();
            Test("Gravity accelerates a released ball", delegate {
                BallPhysics b = Ball(); b.Step(1.0 / 240);
                Check(b.VY > 7 && b.Y > 100, "Ball did not fall");
            });
            Test("Left and right walls reflect horizontal velocity", delegate {
                BallPhysics b = Ball(); b.Gravity = false; b.X = b.Arena.Left + b.Radius; b.VX = -2000; b.Step(1.0 / 240);
                Check(b.VX > 0 && b.X >= b.Arena.Left + b.Radius, "Left wall failed");
                b.X = b.Arena.Right - b.Radius; b.VX = 2000; b.Step(1.0 / 240);
                Check(b.VX < 0 && b.X <= b.Arena.Right - b.Radius, "Right wall failed");
            });
            Test("Ceiling and floor reflect vertical velocity", delegate {
                BallPhysics b = Ball(); b.Y = b.Arena.Top + b.Radius; b.VY = -1200; b.Step(1.0 / 240);
                Check(b.VY > 0 && b.Y >= b.Arena.Top + b.Radius, "Ceiling failed");
                b.Y = b.Arena.Bottom - b.Radius; b.VY = 1200; b.Step(1.0 / 240);
                Check(b.VY < 0 && b.Y <= b.Arena.Bottom - b.Radius, "Floor failed");
            });
            Test("Fast throws remain bounded across 60 seconds", delegate {
                BallPhysics b = Ball(); b.VX = 5000; b.VY = -5000; b.Spin = 900;
                for (int i = 0; i < 14400; i++)
                {
                    b.Step(1.0 / 240);
                    Check(b.X >= b.Arena.Left + b.Radius && b.X <= b.Arena.Right - b.Radius &&
                        b.Y >= b.Arena.Top + b.Radius && b.Y <= b.Arena.Bottom - b.Radius, "Ball escaped screen at step " + i);
                }
                Check(b.Sleeping && b.VX == 0 && b.VY == 0, "Ball never settled");
            });
            Test("Higher elasticity retains more bounce energy", delegate {
                BallPhysics soft = Ball(), hard = Ball(); soft.Restitution = .65; hard.Restitution = .94;
                soft.Y = hard.Y = soft.Arena.Bottom - soft.Radius;
                soft.VY = hard.VY = 1000; soft.Step(1.0 / 240); hard.Step(1.0 / 240);
                Check(Math.Abs(hard.VY) > Math.Abs(soft.VY) && Math.Abs(hard.VY) < 1010, "Elasticity is wrong");
            });
            Test("Zero gravity preserves direction and settles with drag", delegate {
                BallPhysics b = Ball(); b.Gravity = false; b.Step(.01);
                Check(b.VY == 0 && b.Y == 100, "Zero gravity accelerated");
                b.Sleeping = false; b.VX = 100;
                for (int i = 0; i < 30000; i++) b.Step(1.0 / 240);
                Check(b.Sleeping, "Zero gravity never idles");
            });
            Test("Monitor change recovers offscreen positions", delegate {
                BallPhysics b = Ball(); b.X = -4000; b.Y = 3000;
                b.Arena = new Rectangle(0, 0, 1280, 720); b.ClampToArena();
                Check(b.X == 40 && b.Y == 680, "Offscreen recovery failed");
            });
            Test("Throw regression measures direction and speed", delegate {
                ThrowTracker tracker = new ThrowTracker();
                for (int i = 0; i <= 10; i++) tracker.Add(i * .008, i * 8, -i * 4);
                PointF velocity = tracker.Velocity(1);
                Check(Math.Abs(velocity.X - 1000) < 1 && Math.Abs(velocity.Y + 500) < 1, "Wrong throw velocity");
            });
            Test("Holding still cancels a stale throw", delegate {
                ThrowTracker tracker = new ThrowTracker();
                tracker.Add(0, 0, 0); tracker.Add(.02, 200, 100);
                for (int i = 1; i <= 20; i++) tracker.Add(.02 + i * .01, 200, 100);
                Check(tracker.Velocity(1) == PointF.Empty, "Old gesture was reused");
            });
            Test("Single click has no accidental throw", delegate {
                ThrowTracker tracker = new ThrowTracker(); tracker.Add(0, 20, 20); tracker.Add(.001, 20, 20);
                Check(tracker.Velocity(1) == PointF.Empty, "Click acquired velocity");
            });
            Test("Extreme gestures are capped", delegate {
                ThrowTracker tracker = new ThrowTracker(); tracker.Add(0, 0, 0); tracker.Add(.01, 10000, 10000);
                PointF v = tracker.Velocity(1);
                Check(Math.Sqrt(v.X * v.X + v.Y * v.Y) <= 5001, "Velocity cap failed");
            });
            Test("Sprites have transparent corners and opaque textured centers", delegate {
                using (Bitmap bitmap = BallArt.Render(80, 5, 0))
                {
                    Check(bitmap.GetPixel(0, 0).A == 0, "Corner isn't transparent");
                    Check(bitmap.GetPixel(45, 45).A == 255, "Center isn't opaque");
                    Check(bitmap.GetPixel(45, 25).G > bitmap.GetPixel(45, 75).G, "Lighting is flat");
                }
            });
            Test("Rotation changes seam position", delegate {
                using (Bitmap a = BallArt.Render(80, 5, 0))
                using (Bitmap b = BallArt.Render(80, 5, 90))
                    Check(a.GetPixel(57, 45).B != b.GetPixel(57, 45).B, "Seam didn't rotate");
            });
            int failed = results.FindAll(delegate(string line) { return line.StartsWith("FAIL"); }).Count;
            results.Add(string.Format("\r\n{0} tests, {1} failed", results.Count, failed));
            File.WriteAllLines(path, results);
            return failed == 0 ? 0 : 1;
        }
    }

    internal static class SmokeTests
    {
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint kind);

        internal static int Run(string path)
        {
            List<string> log = new List<string>();
            int result = 0, phase = 0;
            IntPtr foreground = GetForegroundWindow();
            using (BallForm form = new BallForm(new Settings(), true))
            using (System.Windows.Forms.Timer driver = new System.Windows.Forms.Timer())
            {
                double previousY = 0;
                driver.Interval = 450;
                driver.Tick += delegate {
                    try
                    {
                        if (phase == 0)
                        {
                            SelfTests.Check(form.Visible && form.TopMost && !form.ShowInTaskbar, "Overlay flags invalid");
                            SelfTests.Check((GetWindowLong(form.Handle, -20) & 0x08080080) == 0x08080080, "Native extended styles missing");
                            SelfTests.Check(GetForegroundWindow() == foreground, "Overlay stole focus on startup");
                            SelfTests.Check(form.PresentedFrames > 5, "Layered window didn't animate");
                            log.Add("PASS native overlay, topmost, transparency, no taskbar, no focus theft, animated uploads");
                            previousY = form.Ball.Y;
                        }
                        else if (phase == 1)
                        {
                            SelfTests.Check(Math.Abs(form.Ball.Y - previousY) > 1, "Live physics did not move");
                            Point origin = new Point((int)form.Ball.X, (int)form.Ball.Y);
                            form.BeginGrab(origin, 10);
                            form.MoveGrab(new Point(origin.X - 20, origin.Y - 10), 10.02);
                            form.EndGrab(new Point(origin.X - 60, origin.Y - 30), 10.06);
                            SelfTests.Check(!form.Dragging && !form.Capture && form.Ball.VX < -900 && form.Ball.VY < -400, "Grab/release failed");
                            log.Add("PASS grab, movement tracking, release, capture cleanup, throw velocity");
                            form.TogglePause(); previousY = form.Ball.Y;
                        }
                        else if (phase == 2)
                        {
                            SelfTests.Check(form.Paused && form.Ball.Y == previousY, "Pause did not freeze physics");
                            log.Add("PASS pause freezes animation");
                            uint before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                            form.SetDiameter(112); form.SetDiameter(56); form.SetDiameter(80);
                            uint after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                            SelfTests.Check(after <= before + 5, "Resize leaked GDI objects");
                            log.Add("PASS all sizes render; repeated resize releases native bitmaps");
                            form.ResetBall();
                            SelfTests.Check(!form.Paused && form.Ball.Arena.Contains((int)form.Ball.X, (int)form.Ball.Y), "Reset failed");
                            log.Add("PASS reset recovers ball and resumes physics");
                        }
                        else
                        {
                            SelfTests.Check(form.PresentedFrames > 20, "Animation stalled after resize");
                            log.Add("PASS animation continues after reset and sprite replacement");
                            driver.Stop(); form.Close();
                        }
                        phase++;
                    }
                    catch (Exception exception)
                    {
                        log.Add("FAIL " + exception); result = 1; driver.Stop(); form.Close();
                    }
                };
                form.Shown += delegate { driver.Start(); };
                Application.Run(form);
            }
            File.WriteAllLines(path, log);
            return result;
        }
    }
}
