using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowBall
{
    // Small, click-through windows: no full-screen input surface or keyboard hooks.
    internal sealed class GameOverlay : Form
    {
        internal GameOverlay()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams p = base.CreateParams; p.ExStyle |= 0x080800A0; return p; }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
        internal void Upload(Bitmap bitmap, int x, int y)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero), dc = IntPtr.Zero, handle = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                dc = Native.CreateCompatibleDC(screen); handle = bitmap.GetHbitmap(Color.FromArgb(0)); old = Native.SelectObject(dc, handle);
                Native.Point position = new Native.Point(x, y), origin = new Native.Point(0, 0);
                Native.Size size = new Native.Size(bitmap.Width, bitmap.Height);
                Native.Blend blend = new Native.Blend { Alpha = 255, Format = 1 };
                if (!Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref position, ref size, dc, ref origin, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                if (old != IntPtr.Zero) Native.SelectObject(dc, old);
                if (handle != IntPtr.Zero) Native.DeleteObject(handle);
                if (dc != IntPtr.Zero) Native.DeleteDC(dc);
                Native.ReleaseDC(IntPtr.Zero, screen);
            }
            if (!Visible) Show();
        }
    }

    internal static class GameArt
    {
        internal static Bitmap Gallery()
        {
            Bitmap bitmap = new Bitmap(1280, 510);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Font title = new Font("Segoe UI", 22, FontStyle.Bold))
            using (Font label = new Font("Segoe UI", 13, FontStyle.Bold))
            using (Font body = new Font("Segoe UI", 11))
            using (Brush white = new SolidBrush(Color.FromArgb(241, 244, 237)))
            using (Brush muted = new SolidBrush(Color.FromArgb(171, 184, 197)))
            {
                g.Clear(Color.FromArgb(21, 26, 32)); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawString("WINDOW BALL", title, white, 35, 23);
                g.DrawString("Четыре мяча. Четыре характера. Четыре игры.", body, muted, 38, 73);
                int index = 0;
                foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin)))
                {
                    BallProfile profile = BallProfile.For(skin);
                    int x = 30 + index * 312;
                    using (Brush card = new SolidBrush(Color.FromArgb(31, 38, 46)))
                    using (GraphicsPath path = Rounded(new RectangleF(x, 120, 292, 354), 20)) g.FillPath(card, path);
                    using (Bitmap ball = BallArt.Render(180, 10, skin == BallSkin.Football ? -24 : -16, skin)) g.DrawImage(ball, x + 46, 139);
                    using (Brush accent = new SolidBrush(Accent(skin))) g.DrawString(profile.Name, label, accent, x + 17, 350);
                    g.DrawString(profile.GameName, body, white, x + 18, 387);
                    string description = skin == BallSkin.Tennis ? "10 мишеней с рикошетом" : skin == BallSkin.Basketball ? "20 отскоков об пол" : skin == BallSkin.Football ? "5 точных пасов" : "20 подбиваний в воздухе";
                    g.DrawString(description, body, muted, x + 18, 419);
                    index++;
                }
            }
            return bitmap;
        }
        internal static Color Accent(BallSkin skin)
        {
            switch (skin) { case BallSkin.Basketball: return Color.FromArgb(255, 168, 85); case BallSkin.Football: return Color.FromArgb(121, 206, 255); case BallSkin.Footbag: return Color.FromArgb(255, 158, 186); default: return Color.FromArgb(203, 237, 114); }
        }
        internal static Bitmap Hud(SportsGame game, int best, bool paused)
        {
            double scale = Math.Min(game.Scale, Math.Max(.5, (game.Arena.Width - 32.0) / 510));
            Bitmap bitmap = new Bitmap((int)(510 * scale), (int)(146 * scale), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform((float)scale, (float)scale);
                Color accent = Accent(game.Skin);
                using (GraphicsPath path = Rounded(new RectangleF(1, 1, 508, 144), 18))
                using (Brush background = new SolidBrush(Color.FromArgb(235, 24, 29, 35))) g.FillPath(background, path);
                using (Font title = new Font("Segoe UI", 10, FontStyle.Bold))
                using (Font score = new Font("Segoe UI", 24, FontStyle.Bold))
                using (Font body = new Font("Segoe UI", 9))
                using (Brush colored = new SolidBrush(accent))
                using (Brush white = new SolidBrush(Color.FromArgb(243, 244, 239)))
                using (Brush muted = new SolidBrush(Color.FromArgb(173, 182, 191)))
                {
                    string name = BallProfile.For(game.Skin).GameName.ToUpperInvariant();
                    g.DrawString("WINDOW BALL  /  " + name, title, colored, 18, 12);
                    g.DrawString(game.Score + " / " + game.Goal, score, white, 15, 32);
                    string state = paused ? "ПАУЗА" : game.Waiting ? "ГОТОВ?" : !game.Active ? (game.Won ? "ПОБЕДА" : "РАУНД ЗАВЕРШЁН") : game.TapGame ? "СЕРИЯ" : Math.Ceiling(game.Remaining) + " сек";
                    g.DrawString(state + "   ·   рекорд " + best, title, colored, 175, 48);
                    string instruction = game.Skin == BallSkin.Tennis ? "Бросок → отскок от края → мишень" : game.Skin == BallSkin.Basketball ? "Клик → пол → отскок → следующий клик" : game.Skin == BallSkin.Football ? "Бросок слева → движущаяся зона справа" : "Кликай на падении, не дай соксу коснуться пола";
                    string message = string.IsNullOrEmpty(game.Message) ? instruction : game.Message;
                    using (StringFormat format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(message, body, white, new RectangleF(18, 88, 478, 22), format);
                    g.DrawString("ПКМ по мячу — новая игра, обычный режим и выход", body, muted, 18, 116);
                }
            }
            return bitmap;
        }
        internal static Bitmap Target(SportsGame game)
        {
            int w = Math.Max(8, (int)Math.Ceiling(game.Target.Width) + 12), h = Math.Max(8, (int)Math.Ceiling(game.Target.Height) + 12);
            Bitmap bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(Accent(game.Skin), (float)(3 * game.Scale)))
            using (Brush wash = new SolidBrush(Color.FromArgb(42, Accent(game.Skin))))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF rect = new RectangleF(6, 6, w - 13, h - 13);
                if (game.Skin == BallSkin.Tennis)
                {
                    g.FillEllipse(wash, rect); g.DrawEllipse(pen, rect);
                    rect.Inflate(-rect.Width * .22f, -rect.Height * .22f); g.DrawEllipse(pen, rect);
                    g.DrawLine(pen, w / 2f - 8, h / 2f, w / 2f + 8, h / 2f);
                    g.DrawLine(pen, w / 2f, h / 2f - 8, w / 2f, h / 2f + 8);
                }
                else
                {
                    using (GraphicsPath path = Rounded(rect, (float)(14 * game.Scale))) { g.FillPath(wash, path); g.DrawPath(pen, path); }
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawLine(pen, w * .25f, 14, w * .25f, h - 14);
                    using (Font font = new Font("Segoe UI", (float)(10 * game.Scale), FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Accent(game.Skin)))
                    using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("ПАС", font, brush, rect, format);
                }
            }
            return bitmap;
        }
        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }
}
