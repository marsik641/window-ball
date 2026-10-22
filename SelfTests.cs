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
            SportsTests();
            int failed = results.FindAll(delegate(string line) { return line.StartsWith("FAIL"); }).Count;
            results.Add(string.Format("\r\n{0} tests, {1} failed", results.Count, failed));
            File.WriteAllLines(path, results);
            return failed == 0 ? 0 : 1;
        }

        private static SportsGame Game(BallSkin skin)
        {
            SportsGame game = new SportsGame(new Random(42)); game.Start(skin, new Rectangle(0, 0, 1920, 1080), 1); return game;
        }
        private static void SportsTests()
        {
            Test("All four bodies share gravity independent of mass", delegate {
                foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin)))
                {
                    BallPhysics b = Ball(); b.Profile = BallProfile.For(skin); b.Step(1.0 / 240);
                    Check(Math.Abs(b.VY - 9.81 * 180 / 240) < .01, "Mass changed gravitational acceleration");
                }
            });
            Test("Material restitution separates rubber from sand", delegate {
                double[] speeds = new double[4]; int index = 0;
                foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin)))
                {
                    BallPhysics b = Ball(); b.Profile = BallProfile.For(skin); b.Angle = 0;
                    b.Y = b.Arena.Bottom - b.HalfHeight; b.VY = 1000; b.Step(1.0 / 240); speeds[index++] = -b.VY;
                }
                Check(speeds[1] > speeds[0] && speeds[0] > speeds[2] && speeds[2] > speeds[3], "Bounce profiles overlap");
                Check(speeds[3] < 65, "Sand-filled bag bounced like rubber");
            });
            Test("Off-center football contact transfers motion into rotation", delegate {
                BallPhysics b = Ball(); b.Profile = BallProfile.Football; b.Gravity = false; b.Angle = 35;
                b.Y = b.Arena.Bottom - b.HalfHeight; b.VY = 1000;
                double energy = b.Energy; b.Step(1.0 / 240);
                Check(Math.Abs(b.Spin) > 100 && Math.Abs(b.VX) > 10, "Ellipsoid bounced as a sphere");
                Check(b.Energy < energy, "Contact created energy");
            });
            Test("Every material stays within bounds and comes to rest", delegate {
                foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin)))
                {
                    BallPhysics b = Ball(); b.Profile = BallProfile.For(skin); b.Angle = 34; b.VX = 2400; b.VY = -1800; b.Spin = 750;
                    for (int i = 0; i < 24000; i++)
                    {
                        b.Step(1.0 / 240);
                        Check(b.X >= b.Arena.Left + b.HalfWidth - .01 && b.X <= b.Arena.Right - b.HalfWidth + .01 &&
                            b.Y >= b.Arena.Top + b.HalfHeight - .01 && b.Y <= b.Arena.Bottom - b.HalfHeight + .01, skin + " escaped screen");
                    }
                    Check(b.Sleeping, skin + " never settled");
                }
            });
            Test("Footbag loses spin faster than an inflated ball", delegate {
                BallPhysics bag = Ball(), basket = Ball(); bag.Profile = BallProfile.Footbag; basket.Profile = BallProfile.Basketball;
                bag.Gravity = basket.Gravity = false; bag.Spin = basket.Spin = 300;
                for (int i = 0; i < 120; i++) { bag.Step(1.0 / 240); basket.Step(1.0 / 240); }
                Check(Math.Abs(bag.Spin) < Math.Abs(basket.Spin) * .3, "Internal damping missing");
            });
            Test("Tennis rejects direct hits and counts one ricochet per throw", delegate {
                SportsGame game = Game(BallSkin.Tennis); BallPhysics b = Ball(); b.X = 300; b.VX = 800; game.Release(b);
                b.X = game.Target.X + game.Target.Width / 2; b.Y = game.Target.Y + game.Target.Height / 2;
                game.Advance(.01, b.X, b.Y, b, true); Check(game.Score == 0, "Direct hit scored");
                b.Contacts = Contact.Wall; game.Advance(.01, b.X, b.Y, b, true);
                Check(game.Score == 1, "Ricochet didn't score");
                game.Advance(.01, b.X, b.Y, b, true); Check(game.Score == 1, "Single throw counted twice");
            });
            Test("Tennis detects swept hits between simulation positions", delegate {
                SportsGame game = Game(BallSkin.Tennis); BallPhysics b = Ball(); b.X = 300; b.VX = 800; game.Release(b);
                b.Contacts = Contact.Wall; b.Y = game.Target.Y + game.Target.Height / 2; b.X = game.Target.Right + 100;
                game.Advance(.01, game.Target.Left - 100, b.Y, b, true); Check(game.Score == 1, "Fast shot tunneled through target");
            });
            Test("Basketball requires a click between successive floor bounces", delegate {
                SportsGame game = Game(BallSkin.Basketball); BallPhysics b = Ball();
                Check(game.Tap(b) && b.VY > 0, "Click didn't push down");
                Check(!game.Tap(b), "Spam accepted before a bounce");
                b.Contacts = Contact.Floor; b.Impact = 1000; game.Advance(.5, 0, 0, b, true);
                Check(game.Score == 1 && game.Active, "First dribble failed");
                game.Advance(.5, 0, 0, b, true);
                Check(!game.Active && !game.Won, "Missed dribble didn't end series");
            });
            Test("Basketball wins after twenty controlled dribbles", delegate {
                SportsGame game = Game(BallSkin.Basketball); BallPhysics b = Ball();
                for (int i = 0; i < 20; i++) { Check(game.Tap(b), "Tap rejected"); b.Contacts = Contact.Floor; b.Impact = 1000; game.Advance(.5, 0, 0, b, true); }
                Check(game.Score == 20 && game.Won && !game.Active, "Dribble goal didn't finish");
            });
            Test("Football requires a direct left-to-right pass", delegate {
                SportsGame game = Game(BallSkin.Football); BallPhysics b = Ball(); b.X = 1500; b.VX = 1000;
                game.Release(b); Check(!game.ShotArmed, "Pass started in receiver area");
                b.X = 300; game.Release(b); b.Contacts = Contact.Wall;
                game.Advance(.01, b.X, b.Y, b, true); Check(!game.ShotArmed && game.Score == 0, "Bounced pass counted");
                b.Contacts = Contact.None; game.Release(b); b.Y = game.Target.Y + game.Target.Height / 2;
                double oldX = game.Target.Left - 30; b.X = game.Target.Right + 30;
                game.Advance(.01, oldX, b.Y, b, true); Check(game.Score == 1 && game.NeedsServe, "Swept pass wasn't caught");
            });
            Test("Football receiver moves while the clock is running", delegate {
                SportsGame game = Game(BallSkin.Football); BallPhysics b = Ball(); b.X = 300; b.VX = 1000; game.Release(b);
                float y = game.Target.Y; game.Advance(.5, 300, 100, b, false);
                Check(Math.Abs(game.Target.Y - y) > 10, "Receiver isn't moving");
            });
            Test("Footbag accepts descending taps, rejects ascending spam, loses on floor", delegate {
                SportsGame game = Game(BallSkin.Footbag); BallPhysics b = Ball();
                Check(game.Tap(b) && b.VY < 0 && game.Score == 1, "First juggle failed");
                game.Advance(.3, 0, 0, b, false); Check(!game.Tap(b), "Ascending spam counted");
                b.VY = 200; Check(game.Tap(b) && game.Score == 2, "Descending tap failed");
                b.Contacts = Contact.Floor; b.Impact = 500; game.Advance(.1, 0, 0, b, true);
                Check(!game.Active && !game.Won, "Floor didn't end juggling");
            });
            Test("Timers start on first throw and end a sixty-second round", delegate {
                SportsGame game = Game(BallSkin.Tennis); BallPhysics b = Ball();
                game.Advance(100, 0, 0, b, false); Check(game.Elapsed == 0 && game.Active, "Ready time consumed round");
                b.VX = 500; game.Release(b); game.Advance(61, 0, 0, b, false);
                Check(!game.Active && game.Remaining == 0 && !game.Won, "Timer did not finish round");
                game.Start(BallSkin.Footbag, b.Arena, 1); game.Stop(); Check(!game.Waiting && !game.Visible, "Stop left ambient physics frozen");
            });
            Test("Skin art matches its elliptical collision outline", delegate {
                foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin)))
                using (Bitmap image = BallArt.Render(100, 5, 0, skin))
                {
                    Check(image.GetPixel(55, 55).A == 255 && image.GetPixel(0, 0).A == 0, "Invalid sprite alpha");
                    if (skin == BallSkin.Football) Check(image.GetPixel(55, 10).A == 0 && image.GetPixel(10, 55).A > 0, "Football isn't elongated");
                }
            });
            Test("Personal records are independent for each skin", delegate {
                Settings settings = new Settings(); Check(settings.Record(BallSkin.Tennis, 4), "No first record");
                Check(!settings.Record(BallSkin.Tennis, 3) && settings.Best(BallSkin.Tennis) == 4 && settings.Best(BallSkin.Basketball) == 0, "Records mixed or decreased");
            });
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
                        else if (phase == 3)
                        {
                            SelfTests.Check(form.PresentedFrames > 20, "Animation stalled after resize");
                            log.Add("PASS animation continues after reset and sprite replacement");
                            foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin))) form.SetSkin(skin);
                            form.SetSkin(BallSkin.Football); form.StartGame();
                            int overlays = 0;
                            foreach (Form window in Application.OpenForms)
                                if (window is GameOverlay && window.Visible)
                                {
                                    SelfTests.Check((GetWindowLong(window.Handle, -20) & 0x080800A0) == 0x080800A0, "HUD intercepts input or activation"); overlays++;
                                }
                            SelfTests.Check(overlays == 2, "HUD and target were not both shown");
                            log.Add("PASS all four native skins; HUD and target are transparent, click-through and nonactivating");
                            previousY = form.Ball.Y;
                        }
                        else if (phase == 4)
                        {
                            SelfTests.Check(form.Game.Waiting && form.Game.Elapsed == 0 && form.Ball.Y == previousY, "Ready state did not wait for input");
                            Point origin = new Point((int)form.Ball.X, (int)form.Ball.Y);
                            form.BeginGrab(origin, 20); form.MoveGrab(new Point(origin.X + 30, origin.Y - 10), 20.03);
                            form.EndGrab(new Point(origin.X + 60, origin.Y - 20), 20.06);
                            SelfTests.Check(!form.Game.Waiting && form.Game.ShotArmed, "Live football throw wasn't armed");
                            log.Add("PASS live game waits for first input and accepts a football throw");
                        }
                        else if (phase == 5)
                        {
                            SelfTests.Check(form.Game.Elapsed > .1, "Game clock did not run");
                            form.TogglePause(); previousY = form.Game.Elapsed;
                        }
                        else if (phase == 6)
                        {
                            SelfTests.Check(form.Game.Elapsed == previousY, "Paused game clock advanced");
                            log.Add("PASS pause freezes game clock as well as physics");
                            form.StopGame();
                            foreach (Form window in Application.OpenForms) if (window is GameOverlay) SelfTests.Check(!window.Visible, "Ambient mode left a target on screen");
                            form.SetSkin(BallSkin.Basketball); form.StartGame();
                            form.Ball.Y = form.Ball.Arena.Bottom - form.Ball.HalfHeight - .1;
                            SelfTests.Check(form.Game.Tap(form.Ball), "Live basketball click rejected");
                        }
                        else if (phase == 7)
                        {
                            SelfTests.Check(form.Game.Score == 1 && form.Game.Active, "Live floor bounce did not score");
                            log.Add("PASS basketball tap and real collision create one dribble point");
                            form.SetSkin(BallSkin.Footbag); form.StartGame(); form.Game.Tap(form.Ball);
                        }
                        else if (phase == 8)
                        {
                            SelfTests.Check(form.Game.Score == 1 && form.Game.Active, "Live footbag tap failed");
                            form.Ball.Y = form.Ball.Arena.Bottom - form.Ball.HalfHeight - .1; form.Ball.VY = 600;
                        }
                        else if (phase == 9)
                        {
                            SelfTests.Check(!form.Game.Active && form.Game.Visible && !form.Game.Won, "Live footbag did not lose on floor");
                            log.Add("PASS footbag floor contact ends the round and leaves the result visible");
                            form.ResetBall();
                            SelfTests.Check(!form.Game.Visible && !form.Game.Waiting, "Reset didn't restore free play");
                        }
                        else
                        {
                            log.Add("PASS free play restored after games; overlay resources close with main form");
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
