using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowBall
{
    internal sealed partial class BallForm
    {
        internal readonly SportsGame Game = new SportsGame();
        private GameOverlay hud, targetOverlay;
        private string lastHudKey;
        private RectangleF lastTarget;
        private double lastGameUi = -1;

        internal void SetSkin(BallSkin skin)
        {
            StopGame();
            dragging = false; Capture = false;
            settings.Skin = skin; Ball.Profile = BallProfile.For(skin);
            SetDiameter(settings.Diameter);
            Ball.Wake();
            using (Bitmap image = BallArt.Render(32, 2, -22, skin))
            {
                IntPtr handle = image.GetHicon();
                Icon replacement;
                try { replacement = (Icon)Icon.FromHandle(handle).Clone(); }
                finally { Native.DestroyIcon(handle); }
                Icon previous = trayIcon;
                Icon = replacement; tray.Icon = replacement; trayIcon = replacement;
                if (previous != null) previous.Dispose();
            }
            tray.Text = "Window Ball · " + Ball.Profile.Name;
            if (!testing) settings.Save();
            WakeTimer();
        }
        internal void StartGame()
        {
            StopGame();
            dragging = false; Capture = false; paused = false;
            Ball.Arena = ArenaAt(new Point((int)Ball.X, (int)Ball.Y));
            Ball.Gravity = true; Ball.Restitution = .80;
            Game.Start(settings.Skin, Ball.Arena, scale);
            PlaceServe();
            lastHudKey = null; lastTarget = RectangleF.Empty; lastGameUi = -1;
            UpdateGameOverlays(clock.Elapsed.TotalSeconds);
            Present(); WakeTimer();
        }
        internal void StopGame()
        {
            Game.Stop();
            Ball.Gravity = settings.Gravity; Ball.Restitution = settings.Bounce;
            if (hud != null) hud.Hide();
            if (targetOverlay != null) targetOverlay.Hide();
            Ball.Wake();
        }
        private void PlaceServe()
        {
            Ball.X = Ball.Arena.Left + Ball.Arena.Width * (Game.TapGame ? .5 : .22);
            Ball.Y = Ball.Arena.Top + Ball.Arena.Height * (Game.TapGame ? .40 : .65);
            Ball.Angle = settings.Skin == BallSkin.Football ? -20 : 0;
            Ball.VX = Ball.VY = Ball.Spin = 0;
            Ball.ClampToArena(); Ball.Sleeping = true;
        }
        private void AddGameMenu()
        {
            ToolStripMenuItem skins = new ToolStripMenuItem("Скин мяча");
            foreach (BallSkin skin in Enum.GetValues(typeof(BallSkin)))
            {
                BallSkin selected = skin;
                ToolStripMenuItem item = new ToolStripMenuItem(BallProfile.For(skin).Name);
                item.Click += delegate { SetSkin(selected); };
                menu.Opening += delegate { item.Checked = settings.Skin == selected; };
                skins.DropDownItems.Add(item);
            }
            menu.Items.Add(skins);
            ToolStripMenuItem play = new ToolStripMenuItem();
            play.Click += delegate { StartGame(); };
            menu.Opening += delegate { play.Text = (Game.Visible ? "Новая игра: " : "Начать игру: ") + Ball.Profile.GameName; };
            menu.Items.Add(play);
            ToolStripMenuItem ambient = new ToolStripMenuItem("Обычный режим");
            ambient.Click += delegate { StopGame(); WakeTimer(); };
            menu.Opening += delegate { ambient.Enabled = Game.Visible; };
            menu.Items.Add(ambient);
            menu.Items.Add(new ToolStripSeparator());
        }
        private void UpdateGameOverlays(double now)
        {
            if (!Game.Visible || now - lastGameUi < .04) return;
            lastGameUi = now;
            if (settings.Record(Game.Skin, Game.Score) && !testing) settings.Save();
            bool stopped = paused || menuOpen;
            string key = Game.Score + "/" + Math.Ceiling(Game.Remaining) + "/" + Game.Active + "/" + Game.Waiting + "/" + Game.Message + "/" + stopped;
            if (key != lastHudKey)
            {
                if (hud == null) hud = new GameOverlay();
                using (Bitmap bitmap = GameArt.Hud(Game, settings.Best(Game.Skin), stopped))
                    hud.Upload(bitmap, Game.Arena.Left + (int)(16 * scale), Game.Arena.Top + (int)(16 * scale));
                lastHudKey = key;
            }
            if (!Game.TapGame && Game.Active)
            {
                if (targetOverlay == null) targetOverlay = new GameOverlay();
                if (!targetOverlay.Visible || Math.Abs(lastTarget.Y - Game.Target.Y) >= 1 || lastTarget.X != Game.Target.X)
                {
                    using (Bitmap bitmap = GameArt.Target(Game)) targetOverlay.Upload(bitmap, (int)Game.Target.X - 6, (int)Game.Target.Y - 6);
                    lastTarget = Game.Target;
                }
            }
            else if (targetOverlay != null) targetOverlay.Hide();
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013); // Above our HUD, without activation or movement.
        }
    }
}
