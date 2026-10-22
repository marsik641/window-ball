using System;
using System.Collections.Generic;
using System.Drawing;

namespace WindowBall
{
    internal sealed class BallPhysics
    {
        internal double X, Y, VX, VY, Angle, Spin;
        internal double Radius = 36, Scale = 1, Restitution = .80;
        internal bool Gravity = true;
        internal Rectangle Arena;
        internal bool Sleeping;

        internal void ClampToArena()
        {
            double r = EffectiveRadius;
            X = BallArt.Clamp(X, Arena.Left + r, Arena.Right - r);
            Y = BallArt.Clamp(Y, Arena.Top + r, Arena.Bottom - r);
        }

        private double EffectiveRadius { get { return Math.Min(Radius, Math.Min(Arena.Width, Arena.Height) / 2.0); } }

        internal void Step(double dt)
        {
            if (Sleeping) return;
            double r = EffectiveRadius;
            bool grounded = Gravity && Y >= Arena.Bottom - r - .01 && Math.Abs(VY) < .01;
            if (Gravity && !grounded) VY += 1750 * Scale * dt;
            double drag = Math.Exp(-.12 * dt);
            VX *= drag;
            if (!Gravity) VY *= drag;
            if (grounded)
            {
                VX = ApproachZero(VX, 290 * Scale * dt);
                Spin = VX / Radius * 180 / Math.PI;
            }
            X += VX * dt;
            Y += VY * dt;
            if (X < Arena.Left + r)
            {
                X = Arena.Left + r;
                if (VX < 0) { VX = -VX * Restitution; Spin += VY / Radius * 8; }
            }
            else if (X > Arena.Right - r)
            {
                X = Arena.Right - r;
                if (VX > 0) { VX = -VX * Restitution; Spin -= VY / Radius * 8; }
            }
            if (Y < Arena.Top + r)
            {
                Y = Arena.Top + r;
                if (VY < 0) { VY = -VY * Restitution; Spin *= .85; }
            }
            else if (Y >= Arena.Bottom - r)
            {
                Y = Arena.Bottom - r;
                if (VY > 0)
                {
                    VY = VY < 110 * Scale ? 0 : -VY * Restitution;
                    VX *= .97;
                    Spin = Spin * .45 + VX / Radius * 180 / Math.PI * .55;
                }
            }
            Spin = BallArt.Clamp(Spin * Math.Exp(-.6 * dt), -1440, 1440);
            Angle = (Angle + Spin * dt) % 360;
            if (grounded && Math.Abs(VX) < 1) { VX = VY = Spin = 0; Sleeping = true; }
            if (!Gravity && Math.Abs(VX) + Math.Abs(VY) + Math.Abs(Spin) < 1) { VX = VY = Spin = 0; Sleeping = true; }
        }

        private static double ApproachZero(double value, double step)
        {
            return Math.Sign(value) * Math.Max(0, Math.Abs(value) - step);
        }
    }

    // Least-squares fit over the recent gesture; stationary samples cancel old throws.
    internal sealed class ThrowTracker
    {
        private struct Sample { internal double Time, X, Y; }
        private readonly List<Sample> samples = new List<Sample>();
        internal void Clear() { samples.Clear(); }
        internal void Add(double time, double x, double y)
        {
            if (samples.Count > 0 && time <= samples[samples.Count - 1].Time) return;
            samples.Add(new Sample { Time = time, X = x, Y = y });
            while (samples.Count > 0 && time - samples[0].Time > .100) samples.RemoveAt(0);
        }
        internal PointF Velocity(double scale)
        {
            if (samples.Count < 2) return PointF.Empty;
            double t = 0, x = 0, y = 0;
            foreach (Sample s in samples) { t += s.Time; x += s.X; y += s.Y; }
            t /= samples.Count; x /= samples.Count; y /= samples.Count;
            double denominator = 0, vx = 0, vy = 0;
            foreach (Sample s in samples)
            {
                double delta = s.Time - t;
                denominator += delta * delta; vx += delta * (s.X - x); vy += delta * (s.Y - y);
            }
            if (denominator < .000025) return PointF.Empty;
            vx /= denominator; vy /= denominator;
            double speed = Math.Sqrt(vx * vx + vy * vy), limit = 5000 * scale;
            if (speed > limit) { vx *= limit / speed; vy *= limit / speed; }
            return new PointF((float)vx, (float)vy);
        }
    }
}
