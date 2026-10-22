using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowBall
{
    internal static class BallArt
    {
        internal static Bitmap Render(int diameter, int padding, double angle)
        {
            return Render(diameter, padding, angle, BallSkin.Tennis);
        }

        internal static Bitmap Render(int diameter, int padding, double angle, BallSkin skin)
        {
            int extent = diameter + 2 * padding;
            Bitmap bitmap = new Bitmap(extent, extent, PixelFormat.Format32bppPArgb);
            byte[] pixels = new byte[extent * extent * 4];
            double radius = diameter / 2.0, center = extent / 2.0;
            double radians = angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            for (int y = 0; y < extent; y++)
            for (int x = 0; x < extent; x++)
            {
                double nx = (x + .5 - center) / radius, ny = (y + .5 - center) / radius;
                double u = nx * cos + ny * sin, v = -nx * sin + ny * cos;
                double aspect = BallProfile.For(skin).Aspect;
                double localY = v / aspect;
                double distance = Math.Sqrt(u * u + localY * localY);
                double alpha = Clamp((1 - distance) * radius + .5, 0, 1);
                if (alpha <= 0) continue;
                double z = Math.Sqrt(Math.Max(0, 1 - Math.Min(1, distance * distance)));
                double seamDistance = Math.Abs(Math.Abs(u) - (.30 + .48 * v * v)) / Math.Sqrt(1 + .96 * .96 * v * v);
                double seam = 1 - Clamp((seamDistance - .029) * radius, 0, 1);
                double groove = 1 - Clamp((seamDistance - .052) * radius, 0, 1);
                double light = Math.Max(0, -.38 * nx - .48 * ny + .79 * z);
                double shade = .42 + .58 * light;
                double noise = Noise((int)Math.Floor(u * radius * 1.4), (int)Math.Floor(v * radius * 1.4));
                double felt = (noise - .5) * 17;
                double glint = Math.Pow(Math.Max(0, -.30 * nx - .42 * ny + .855 * z), 22) * 10;
                double red = 204 * shade + felt + glint, green = 229 * shade + felt + glint, blue = 40 * shade + felt * .65;
                double recess = groove * (1 - seam) * .20;
                red *= 1 - recess; green *= 1 - recess; blue *= 1 - recess;
                red = red * (1 - seam) + (246 * shade + felt * .25) * seam;
                green = green * (1 - seam) + (243 * shade + felt * .25) * seam;
                blue = blue * (1 - seam) + (211 * shade + felt * .25) * seam;
                if (skin == BallSkin.Basketball)
                {
                    double line = Math.Min(Math.Min(Math.Abs(u), Math.Abs(v)), Math.Abs(Math.Abs(u) - (.62 - .32 * v * v)) / 1.2);
                    double black = 1 - Clamp((line - .021) * radius, 0, 1);
                    double pebbles = (Math.Sin(u * radius * 3.4) * Math.Sin(v * radius * 3.4) > .15 ? 8 : -4) + (noise - .5) * 12;
                    red = (221 + pebbles) * shade; green = (112 + pebbles * .6) * shade; blue = (38 + pebbles * .3) * shade;
                    red = red * (1 - black) + 32 * shade * black;
                    green = green * (1 - black) + 27 * shade * black;
                    blue = blue * (1 - black) + 21 * shade * black;
                }
                else if (skin == BallSkin.Football)
                {
                    double leather = (noise - .5) * 21;
                    red = (142 + leather) * shade; green = (69 + leather * .65) * shade; blue = (34 + leather * .4) * shade;
                    double stripe = 1 - Clamp((Math.Abs(Math.Abs(u) - .70) - .055) * radius, 0, 1);
                    bool stitch = Math.Abs(u) < .39 && Math.Abs(localY + .20) < .21 && Math.Abs(Math.Sin((u + .015) * 36)) > .83;
                    bool spine = Math.Abs(u) < .44 && Math.Abs(localY + .20) < .035;
                    double white = Math.Max(stripe, stitch || spine ? 1 : 0);
                    red = red * (1 - white) + 244 * shade * white; green = green * (1 - white) + 228 * shade * white; blue = blue * (1 - white) + 199 * shade * white;
                }
                else if (skin == BallSkin.Footbag)
                {
                    double panelAngle = Math.Atan2(localY, u) + Math.PI;
                    int panel = (int)(panelAngle / (Math.PI / 3));
                    bool centerPanel = distance < .40;
                    double rr = centerPanel ? 240 : (panel % 3 == 0 ? 52 : panel % 3 == 1 ? 235 : 224);
                    double gg = centerPanel ? 191 : (panel % 3 == 0 ? 149 : panel % 3 == 1 ? 107 : 191);
                    double bl = centerPanel ? 72 : (panel % 3 == 0 ? 147 : panel % 3 == 1 ? 80 : 65);
                    double thread = Math.Sin(u * radius * 3.1) * Math.Sin(localY * radius * 3.1) * 8 + felt * .55;
                    double edge = Math.Min(Math.Abs(distance - .40), distance > .40 ? Math.Abs(Math.Sin(panelAngle * 3)) * distance / 3 : 1);
                    double stitching = 1 - Clamp((edge - .014) * radius, 0, 1);
                    double fold = 1 - stitching * .32;
                    red = (rr + thread) * shade * fold; green = (gg + thread) * shade * fold; blue = (bl + thread) * shade * fold;
                }
                int offset = (y * extent + x) * 4;
                pixels[offset] = (byte)(Clamp(blue, 0, 255) * alpha);
                pixels[offset + 1] = (byte)(Clamp(green, 0, 255) * alpha);
                pixels[offset + 2] = (byte)(Clamp(red, 0, 255) * alpha);
                pixels[offset + 3] = (byte)(255 * alpha);
            }
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, extent, extent), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }

        private static double Noise(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + 1274126177);
                n = (n ^ (n >> 13)) * 1274126177;
                return (n ^ (n >> 16)) / (double)uint.MaxValue;
            }
        }
        internal static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
