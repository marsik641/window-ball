using System;
using System.Collections.Generic;
using System.Drawing;

namespace WindowBall
{
    [Flags] internal enum Contact { None = 0, Wall = 1, Ceiling = 2, Floor = 4 }

    internal sealed class BallPhysics
    {
        internal double X, Y, VX, VY, Angle, Spin;
        internal double Radius = 40, Scale = 1, Restitution = .80;
        internal bool Gravity = true;
        internal Rectangle Arena;
        internal bool Sleeping;
        internal BallProfile Profile = BallProfile.Tennis;
        internal Contact Contacts;
        internal double Impact;
        private double restTime;
        internal double AxisY { get { return Radius * Profile.Aspect; } }
        internal double HalfWidth { get { double t = Angle * Math.PI / 180; return Math.Sqrt(Radius * Radius * Math.Cos(t) * Math.Cos(t) + AxisY * AxisY * Math.Sin(t) * Math.Sin(t)); } }
        internal double HalfHeight { get { double t = Angle * Math.PI / 180; return Math.Sqrt(Radius * Radius * Math.Sin(t) * Math.Sin(t) + AxisY * AxisY * Math.Cos(t) * Math.Cos(t)); } }
        internal double Energy { get { return .5 * Profile.Mass * (VX * VX + VY * VY + Profile.InertiaFactor * Radius * Radius * Math.Pow(Spin * Math.PI / 180, 2)); } }

        internal void Wake() { Sleeping = false; restTime = 0; }
        internal void ClampToArena()
        {
            double rx = Math.Min(HalfWidth, Arena.Width / 2.0), ry = Math.Min(HalfHeight, Arena.Height / 2.0);
            X = BallArt.Clamp(X, Arena.Left + rx, Arena.Right - rx);
            Y = BallArt.Clamp(Y, Arena.Top + ry, Arena.Bottom - ry);
        }
        internal bool Contains(Point point)
        {
            double t = Angle * Math.PI / 180, dx = point.X - X, dy = point.Y - Y;
            double u = dx * Math.Cos(t) + dy * Math.Sin(t), v = -dx * Math.Sin(t) + dy * Math.Cos(t);
            return u * u / (Radius * Radius) + v * v / (AxisY * AxisY) <= 1;
        }
        internal void Step(double dt)
        {
            Contacts = Contact.None; Impact = 0;
            if (Sleeping) return;
            if (Gravity) VY += 9.81 * 180 * Scale * dt;
            double speed = Math.Sqrt(VX * VX + VY * VY);
            double drag = 1 / (1 + Profile.AirDrag / Scale * speed * dt);
            VX *= drag; VY *= drag;
            Spin *= Math.Exp(-Profile.SpinDrag * dt);
            X += VX * dt; Y += VY * dt;
            Angle = (Angle + Spin * dt) % 360;
            double rx = Math.Min(HalfWidth, Arena.Width / 2.0), ry = Math.Min(HalfHeight, Arena.Height / 2.0);
            if (X <= Arena.Left + rx) { X = Arena.Left + rx; Resolve(1, 0, Contact.Wall); }
            else if (X >= Arena.Right - rx) { X = Arena.Right - rx; Resolve(-1, 0, Contact.Wall); }
            if (Y <= Arena.Top + ry) { Y = Arena.Top + ry; Resolve(0, 1, Contact.Ceiling); }
            else if (Y >= Arena.Bottom - ry) { Y = Arena.Bottom - ry; Resolve(0, -1, Contact.Floor); }
            bool grounded = Gravity && Y >= Arena.Bottom - HalfHeight - .1;
            if (grounded)
            {
                double rollingLoss = Profile.Rolling * 9.81 * 180 * Scale * dt;
                VX = Math.Sign(VX) * Math.Max(0, Math.Abs(VX) - rollingLoss);
                if (Profile.Skin == BallSkin.Footbag) { VX *= Math.Exp(-7 * dt); Spin *= Math.Exp(-12 * dt); }
            }
            if ((grounded || !Gravity) && Math.Abs(VX) + Math.Abs(VY) < 4 * Scale && Math.Abs(Spin) < 6) restTime += dt;
            else restTime = 0;
            if (restTime > .4) { VX = VY = Spin = 0; Sleeping = true; }
        }
        private void Resolve(double nx, double ny, Contact contact)
        {
            double t = Angle * Math.PI / 180, c = Math.Cos(t), s = Math.Sin(t);
            double aa = Radius * Radius, bb = AxisY * AxisY;
            double qxx = aa * c * c + bb * s * s, qyy = aa * s * s + bb * c * c, qxy = (aa - bb) * s * c;
            double support = Math.Sqrt(nx * nx * qxx + 2 * nx * ny * qxy + ny * ny * qyy);
            double rx = -(qxx * nx + qxy * ny) / support, ry = -(qxy * nx + qyy * ny) / support;
            double omega = Spin * Math.PI / 180;
            double vn = (VX - omega * ry) * nx + (VY + omega * rx) * ny;
            if (vn >= 0) return;
            Contacts |= contact; Impact = Math.Max(Impact, -vn);
            double mass = Profile.Mass, inertia = mass * Profile.InertiaFactor * Radius * Radius;
            double cross = rx * ny - ry * nx;
            double restitution = -vn < 80 * Scale ? 0 : BallArt.Clamp(Profile.Bounce * Restitution / .80, 0, .97);
            double impulse = -(1 + restitution) * vn / (1 / mass + cross * cross / inertia);
            VX += impulse * nx / mass; VY += impulse * ny / mass;
            omega += cross * impulse / inertia;
            double tx = -ny, ty = nx, tangentArm = rx * ty - ry * tx;
            double vt = (VX - omega * ry) * tx + (VY + omega * rx) * ty;
            double friction = -vt / (1 / mass + tangentArm * tangentArm / inertia);
            friction = BallArt.Clamp(friction, -Profile.Friction * impulse, Profile.Friction * impulse);
            VX += friction * tx / mass; VY += friction * ty / mass;
            omega += tangentArm * friction / inertia;
            Spin = omega * 180 / Math.PI;
        }
    }

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
            foreach (Sample sample in samples) { t += sample.Time; x += sample.X; y += sample.Y; }
            t /= samples.Count; x /= samples.Count; y /= samples.Count;
            double denominator = 0, vx = 0, vy = 0;
            foreach (Sample sample in samples)
            {
                double delta = sample.Time - t;
                denominator += delta * delta; vx += delta * (sample.X - x); vy += delta * (sample.Y - y);
            }
            if (denominator < .000025) return PointF.Empty;
            vx /= denominator; vy /= denominator;
            double speed = Math.Sqrt(vx * vx + vy * vy), limit = 5000 * scale;
            if (speed > limit) { vx *= limit / speed; vy *= limit / speed; }
            return new PointF((float)vx, (float)vy);
        }
    }
}
