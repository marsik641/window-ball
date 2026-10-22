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
            int extent = diameter + 2 * padding;
            Bitmap bitmap = new Bitmap(extent, extent, PixelFormat.Format32bppPArgb);
            byte[] pixels = new byte[extent * extent * 4];
            double radius = diameter / 2.0, center = extent / 2.0;
            double radians = angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            for (int y = 0; y < extent; y++)
            for (int x = 0; x < extent; x++)
            {
                double nx = (x + .5 - center) / radius, ny = (y + .5 - center) / radius;
                double distance = Math.Sqrt(nx * nx + ny * ny);
                double alpha = Clamp((1 - distance) * radius + .5, 0, 1);
                if (alpha <= 0) continue;
                double z = Math.Sqrt(Math.Max(0, 1 - Math.Min(1, distance * distance)));
                double u = nx * cos + ny * sin, v = -nx * sin + ny * cos;
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
