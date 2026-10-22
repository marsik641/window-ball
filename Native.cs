using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowBall
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width, Height; public Size(int w, int h) { Width = w; Height = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { public byte Operation, Flags, Alpha, Format; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref Point position, ref Size size, IntPtr src, ref Point origin, int key, ref Blend blend, int flags);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, int flags);
        [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);

        internal static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }
    }

    // Cache native bitmaps: each animation frame only selects a bitmap and uploads it.
    internal sealed class SpriteSet : IDisposable
    {
        internal const int FrameCount = 72;
        private readonly IntPtr[] frames = new IntPtr[FrameCount];
        private IntPtr dc;
        internal readonly int Diameter, Padding, Extent;

        internal SpriteSet(int diameter)
        {
            Diameter = diameter;
            Padding = Math.Max(3, diameter / 14);
            Extent = diameter + Padding * 2;
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            try { dc = Native.CreateCompatibleDC(screen); }
            finally { Native.ReleaseDC(IntPtr.Zero, screen); }
            if (dc == IntPtr.Zero) throw new Win32Exception();
            try
            {
                for (int i = 0; i < frames.Length; i++)
                    using (Bitmap image = BallArt.Render(diameter, Padding, i * 360.0 / FrameCount))
                        frames[i] = image.GetHbitmap(Color.FromArgb(0));
            }
            catch { Dispose(); throw; }
        }

        internal void Present(IntPtr hwnd, double x, double y, double angle)
        {
            int frame = ((int)Math.Round(angle / 360 * FrameCount) % FrameCount + FrameCount) % FrameCount;
            IntPtr old = Native.SelectObject(dc, frames[frame]);
            try
            {
                Native.Point position = new Native.Point((int)Math.Round(x - Diameter / 2.0) - Padding, (int)Math.Round(y - Diameter / 2.0) - Padding);
                Native.Point origin = new Native.Point(0, 0);
                Native.Size size = new Native.Size(Extent, Extent);
                Native.Blend blend = new Native.Blend { Alpha = 255, Format = 1 };
                if (!Native.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref position, ref size, dc, ref origin, 0, ref blend, 2))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Native.SelectObject(dc, old); }
        }

        public void Dispose()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != IntPtr.Zero) Native.DeleteObject(frames[i]);
                frames[i] = IntPtr.Zero;
            }
            if (dc != IntPtr.Zero) Native.DeleteDC(dc);
            dc = IntPtr.Zero;
        }
    }
}
