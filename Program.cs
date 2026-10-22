using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WindowBall
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Native.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args.Length > 1 ? args[1] : "test-results.txt");
            if (args.Length > 1 && args[0] == "--render-preview")
            {
                using (Bitmap image = BallArt.Render(320, 20, -22)) image.Save(args[1]);
                return 0;
            }
            if (args.Length > 1 && args[0] == "--make-icon")
            {
                using (Bitmap image = BallArt.Render(48, 2, -22))
                {
                    IntPtr handle = image.GetHicon();
                    try { using (Icon icon = Icon.FromHandle(handle)) using (FileStream stream = File.Create(args[1])) icon.Save(stream); }
                    finally { Native.DestroyIcon(handle); }
                }
                return 0;
            }
            if (args.Length > 1 && args[0] == "--smoke-test") return SmokeTests.Run(args[1]);
            bool created;
            using (Mutex single = new Mutex(true, "Local\\WindowBall.Desktop.1", out created))
            {
                if (!created) return 0;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { ReportError(e.Exception); Application.Exit(); };
                try { using (BallForm form = new BallForm(Settings.Load(), false)) Application.Run(form); }
                catch (Exception ex) { ReportError(ex); return 1; }
            }
            return 0;
        }

        private static void ReportError(Exception exception)
        {
            string path = Path.Combine(Path.GetDirectoryName(Settings.FileName), "error.log");
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.AppendAllText(path, DateTime.Now + "\r\n" + exception + "\r\n"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            MessageBox.Show("Не удалось продолжить работу мяча.\n" + exception.Message + "\n\nЖурнал: " + path, "Window Ball", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal sealed class BallForm : Form
    {
        internal readonly BallPhysics Ball = new BallPhysics();
        private readonly Settings settings;
        private readonly bool testing;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly ThrowTracker throws = new ThrowTracker();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly NotifyIcon tray = new NotifyIcon();
        private SpriteSet sprites;
        private Icon trayIcon;
        private bool dragging, paused, menuOpen, precisionTimer, disposed;
        private double offsetX, offsetY, lastTick, accumulator, lastPresentAngle = double.NaN;
        private double lastPresentX = double.NaN, lastPresentY = double.NaN, scale = 1;
        internal int PresentedFrames;
        internal bool Paused { get { return paused; } }
        internal bool Dragging { get { return dragging; } }

        internal BallForm(Settings preferences, bool isTesting)
        {
            settings = preferences; testing = isTesting;
            Text = "Window Ball";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Opaque, true);
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) scale = graphics.DpiX / 96.0;
            Ball.Scale = scale; Ball.Restitution = settings.Bounce; Ball.Gravity = settings.Gravity;
            SetDiameter(settings.Diameter);
            ResetBall();
            BuildMenu();
            menu.Opening += delegate { menuOpen = true; WakeTimer(); };
            menu.Closed += delegate { menuOpen = false; WakeTimer(); };
            using (Bitmap image = BallArt.Render(32, 2, -22))
            {
                IntPtr handle = image.GetHicon();
                try { trayIcon = (Icon)Icon.FromHandle(handle).Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
            Icon = trayIcon;
            tray.Icon = trayIcon;
            tray.Text = "Window Ball · потяни и брось";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ResetBall(); };
            tray.Visible = !testing;
            timer.Interval = 8;
            timer.Tick += Tick;
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.UserPreferenceChanged += PreferencesChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00080000 | 0x08000000 | 0x00000080; // Layered, no activation, tool window.
                return parameters;
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Present();
            WakeTimer(); timer.Start();
            if (!testing && !settings.Welcomed)
            {
                tray.ShowBalloonTip(4500, "Твой настольный мяч", "Зажми левую кнопку, потяни и брось.\nПравая кнопка — настройки и выход.\nДвойной щелчок — подбросить.", ToolTipIcon.Info);
                settings.Welcomed = true; settings.Save();
            }
        }

        private Rectangle ArenaAt(Point position)
        {
            Screen screen = Screen.FromPoint(position);
            return settings.AboveTaskbar ? screen.WorkingArea : screen.Bounds;
        }

        internal void ResetBall()
        {
            dragging = false; Capture = false; paused = false;
            Ball.Arena = ArenaAt(Cursor.Position);
            Ball.X = Ball.Arena.Right - Math.Min(Ball.Arena.Width * .25, 240 * scale);
            Ball.Y = Ball.Arena.Top + Math.Min(Ball.Arena.Height * .20, 160 * scale);
            Ball.VX = -180 * scale; Ball.VY = 0; Ball.Spin = -95; Ball.Sleeping = false;
            Ball.ClampToArena(); WakeTimer();
        }

        internal void SetDiameter(int diameter)
        {
            SpriteSet replacement = new SpriteSet(Math.Max(28, (int)Math.Round(diameter * scale)));
            if (sprites != null) sprites.Dispose();
            sprites = replacement;
            settings.Diameter = diameter;
            Ball.Radius = sprites.Diameter / 2.0;
            ClientSize = new Size(sprites.Extent, sprites.Extent);
            if (Ball.Arena.Width > 0) Ball.ClampToArena();
            lastPresentAngle = double.NaN;
            if (IsHandleCreated) Present();
            WakeTimer();
        }

        private void Tick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(.05, Math.Max(0, now - lastTick)); lastTick = now;
            if (dragging) MoveGrab(Cursor.Position, now);
            else if (!paused && !menuOpen)
            {
                accumulator += dt;
                const double step = 1.0 / 240;
                while (accumulator >= step) { Ball.Step(step); accumulator -= step; }
            }
            Present();
            SetTimerRate(dragging || (!paused && !menuOpen && !Ball.Sleeping));
        }

        private void Present()
        {
            if (!IsHandleCreated || sprites == null) return;
            if (Math.Round(Ball.X) == Math.Round(lastPresentX) && Math.Round(Ball.Y) == Math.Round(lastPresentY) &&
                !double.IsNaN(lastPresentAngle) && Math.Abs(Ball.Angle - lastPresentAngle) < 360.0 / SpriteSet.FrameCount) return;
            sprites.Present(Handle, Ball.X, Ball.Y, Ball.Angle);
            lastPresentX = Ball.X; lastPresentY = Ball.Y; lastPresentAngle = Ball.Angle;
            PresentedFrames++;
        }

        private void SetTimerRate(bool active)
        {
            timer.Interval = active ? 8 : 120;
            if (active && !precisionTimer) { precisionTimer = Native.timeBeginPeriod(1) == 0; }
            else if (!active && precisionTimer) { Native.timeEndPeriod(1); precisionTimer = false; }
        }

        private void WakeTimer()
        {
            lastTick = clock.Elapsed.TotalSeconds; accumulator = 0;
            SetTimerRate(true);
        }

        internal void BeginGrab(Point cursor, double time)
        {
            dragging = true; Capture = true;
            offsetX = cursor.X - Ball.X; offsetY = cursor.Y - Ball.Y;
            Ball.VX = Ball.VY = Ball.Spin = 0; Ball.Sleeping = false;
            throws.Clear(); throws.Add(time, cursor.X, cursor.Y);
            WakeTimer();
        }

        internal void MoveGrab(Point cursor, double time)
        {
            if (!dragging) return;
            double oldX = Ball.X;
            Ball.X = cursor.X - offsetX; Ball.Y = cursor.Y - offsetY;
            Ball.Angle = (Ball.Angle + (Ball.X - oldX) / Ball.Radius * 45) % 360;
            throws.Add(time, cursor.X, cursor.Y);
        }

        internal void EndGrab(Point cursor, double time)
        {
            if (!dragging) return;
            MoveGrab(cursor, time);
            PointF velocity = throws.Velocity(scale);
            dragging = false; Capture = false;
            Ball.Arena = ArenaAt(new Point((int)Ball.X, (int)Ball.Y));
            Ball.ClampToArena();
            Ball.VX = velocity.X; Ball.VY = velocity.Y;
            Ball.Spin = BallArt.Clamp(velocity.X / Ball.Radius * 70, -1200, 1200);
            Ball.Sleeping = false; WakeTimer();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) BeginGrab(Cursor.Position, clock.Elapsed.TotalSeconds);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) MoveGrab(Cursor.Position, clock.Elapsed.TotalSeconds);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) EndGrab(Cursor.Position, clock.Elapsed.TotalSeconds);
            if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (dragging && !Capture)
            {
                dragging = false; Ball.VX = Ball.VY = 0;
                Ball.Arena = ArenaAt(new Point((int)Ball.X, (int)Ball.Y));
                Ball.ClampToArena(); Ball.Sleeping = false; WakeTimer();
            }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = false; Capture = false; paused = false;
            Ball.VY = -900 * scale; Ball.VX = 170 * scale; Ball.Spin = 280; Ball.Sleeping = false;
            WakeTimer();
        }

        internal void TogglePause() { paused = !paused; WakeTimer(); }

        private void BuildMenu()
        {
            menu.Font = new Font("Segoe UI", 10);
            ToolStripMenuItem title = new ToolStripMenuItem("WINDOW BALL  /  настольный мяч") { Enabled = false };
            menu.Items.Add(title);
            menu.Items.Add("Подбросить", null, delegate { paused = false; Ball.VY = -1050 * scale; Ball.VX = 200 * scale; Ball.Spin = 320; Ball.Sleeping = false; WakeTimer(); });
            menu.Items.Add("Вернуть мяч на экран", null, delegate { ResetBall(); });
            ToolStripMenuItem pause = new ToolStripMenuItem("Пауза");
            pause.Click += delegate { TogglePause(); };
            menu.Items.Add(pause);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem sizes = new ToolStripMenuItem("Размер");
            AddSize(sizes, "Маленький", 56); AddSize(sizes, "Обычный", 80); AddSize(sizes, "Большой", 112);
            menu.Items.Add(sizes);
            ToolStripMenuItem bounce = new ToolStripMenuItem("Упругость");
            AddBounce(bounce, "Мягкий", .65); AddBounce(bounce, "Теннисный", .80); AddBounce(bounce, "Суперпрыгучий", .94);
            menu.Items.Add(bounce);
            ToolStripMenuItem gravity = new ToolStripMenuItem("Гравитация") { CheckOnClick = true, Checked = settings.Gravity };
            gravity.Click += delegate { settings.Gravity = Ball.Gravity = gravity.Checked; Ball.Sleeping = false; settings.Save(); WakeTimer(); };
            menu.Items.Add(gravity);
            ToolStripMenuItem taskbar = new ToolStripMenuItem("Пол над панелью задач") { CheckOnClick = true, Checked = settings.AboveTaskbar };
            taskbar.Click += delegate { settings.AboveTaskbar = taskbar.Checked; RefreshArena(); settings.Save(); };
            menu.Items.Add(taskbar);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Как играть", null, delegate { MessageBox.Show("Левая кнопка — схватить и бросить.\nЧем резче движение, тем сильнее бросок.\nДвойной щелчок — подбросить.\nПравая кнопка — это меню.\n\nПеретащи мяч на другой монитор, чтобы играть там.\nЗначок мяча в трее — настройки и возврат мяча.", "Window Ball", MessageBoxButtons.OK, MessageBoxIcon.Information); });
            menu.Items.Add("Выход", null, delegate { Close(); });
            menu.Opening += delegate { pause.Checked = paused; };
        }

        private void AddSize(ToolStripMenuItem parent, string label, int value)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Click += delegate { SetDiameter(value); settings.Save(); };
            parent.DropDownItems.Add(item);
            menu.Opening += delegate { item.Checked = settings.Diameter == value; };
        }
        private void AddBounce(ToolStripMenuItem parent, string label, double value)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Click += delegate { settings.Bounce = Ball.Restitution = value; settings.Save(); };
            parent.DropDownItems.Add(item);
            menu.Opening += delegate { item.Checked = Math.Abs(settings.Bounce - value) < .01; };
        }

        private void RefreshArena()
        {
            Ball.Arena = ArenaAt(new Point((int)Ball.X, (int)Ball.Y));
            Ball.ClampToArena(); Ball.Sleeping = false; WakeTimer();
        }
        private void DisplayChanged(object sender, EventArgs e) { ScheduleRefresh(); }
        private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) { ScheduleRefresh(); }
        private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) ScheduleRefresh(); }
        private void ScheduleRefresh()
        {
            if (!IsHandleCreated || disposed) return;
            try { BeginInvoke((Action)RefreshArena); }
            catch (InvalidOperationException) { }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0021) { message.Result = new IntPtr(3); return; } // MA_NOACTIVATE, still receive mouse input.
            if (message.Msg == 0x0084)
            {
                long packed = message.LParam.ToInt64();
                Point point = new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff)));
                if (Math.Pow(point.X - Ball.X, 2) + Math.Pow(point.Y - Ball.Y, 2) > Ball.Radius * Ball.Radius)
                { message.Result = new IntPtr(-1); return; }
            }
            if (message.Msg == 0x02E0)
            {
                // Keep physical cursor coordinates; resize the sprite for the destination monitor's DPI.
                scale = (message.WParam.ToInt64() & 0xffff) / 96.0;
                Ball.Scale = scale;
                SetDiameter(settings.Diameter);
                message.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                SystemEvents.DisplaySettingsChanged -= DisplayChanged;
                SystemEvents.UserPreferenceChanged -= PreferencesChanged;
                SystemEvents.PowerModeChanged -= PowerChanged;
                timer.Stop(); timer.Dispose();
                if (precisionTimer) { Native.timeEndPeriod(1); precisionTimer = false; }
                tray.Visible = false; tray.Dispose(); menu.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
                if (sprites != null) sprites.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
