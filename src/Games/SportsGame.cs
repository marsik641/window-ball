using System;
using System.Drawing;

namespace WindowBall
{
    internal sealed class SportsGame
    {
        private readonly Random random;
        internal BallSkin Skin;
        internal Rectangle Arena;
        internal double Scale, Elapsed;
        internal int Score, Goal;
        internal bool Visible, Active, Waiting, Won, ShotArmed, Bounced, NeedsServe;
        internal string Message = "";
        internal RectangleF Target;
        private double lastTap = -10, messageUntil;
        internal bool TapGame { get { return Skin == BallSkin.Basketball || Skin == BallSkin.Footbag; } }
        internal double Remaining { get { return Math.Max(0, 60 - Elapsed); } }
        internal SportsGame() : this(new Random()) { }
        internal SportsGame(Random source) { random = source; }

        internal void Start(BallSkin skin, Rectangle arena, double scale)
        {
            Skin = skin; Arena = arena; Scale = scale;
            Score = 0; Goal = skin == BallSkin.Tennis ? 10 : skin == BallSkin.Football ? 5 : 20;
            Elapsed = 0; lastTap = -10; Visible = Active = Waiting = true;
            Won = ShotArmed = Bounced = NeedsServe = false;
            Message = skin == BallSkin.Basketball ? "Кликни по мячу — начни дриблинг" : skin == BallSkin.Footbag ? "Кликни по соксу — подбрось его" : "Возьми мяч и сделай первый бросок";
            messageUntil = 0;
            if (skin == BallSkin.Tennis) NewTarget();
            else if (skin == BallSkin.Football) UpdateReceiver();
        }
        internal void Stop() { Active = Visible = Waiting = false; ShotArmed = false; }
        internal void CancelShot() { ShotArmed = Bounced = false; }
        internal void Release(BallPhysics ball)
        {
            if (!Active || TapGame) return;
            if (Math.Sqrt(ball.VX * ball.VX + ball.VY * ball.VY) < 140 * Scale) return;
            if (Skin == BallSkin.Football && ball.X > Arena.Left + Arena.Width * .45)
            {
                Feedback("Начни пас в левой половине экрана"); return;
            }
            Waiting = false; ShotArmed = true; Bounced = false;
            Feedback(Skin == BallSkin.Tennis ? "Сначала отскок, потом мишень" : "Прямой пас в движущуюся зону");
        }
        internal bool Tap(BallPhysics ball)
        {
            if (!Active || !TapGame || Elapsed - lastTap < .22) return false;
            if (Skin == BallSkin.Basketball && ShotArmed) return false;
            if (Skin == BallSkin.Footbag && !Waiting && ball.VY < -80 * Scale) return false;
            Waiting = false; lastTap = Elapsed; ball.Wake();
            double spread = random.NextDouble() * 2 - 1;
            if (Skin == BallSkin.Basketball)
            {
                ball.VY = 1080 * Scale;
                ball.VX = BallArt.Clamp(ball.VX * .4 + spread * 135 * Scale, -230 * Scale, 230 * Scale);
                ShotArmed = true;
                Feedback("Лови следующий отскок кликом");
            }
            else
            {
                ball.VY = -750 * Scale;
                ball.VX = BallArt.Clamp(ball.VX * .35 + spread * 110 * Scale, -170 * Scale, 170 * Scale);
                ball.Spin += spread * 100;
                AddPoint("Подбивание!");
            }
            return true;
        }

        internal void Advance(double dt, double oldX, double oldY, BallPhysics ball, bool moving)
        {
            if (!Active || Waiting) return;
            Elapsed += dt;
            RectangleF oldTarget = Target;
            if (Skin == BallSkin.Football) UpdateReceiver();
            if (!TapGame && Remaining <= 0) { Finish(false, "Время вышло"); return; }
            if (messageUntil > 0 && Elapsed > messageUntil) Message = "";
            if (!moving) return;
            bool floor = (ball.Contacts & Contact.Floor) != 0 && ball.Impact > 100 * Scale;
            if (Skin == BallSkin.Basketball && floor)
            {
                if (ShotArmed) { ShotArmed = false; AddPoint("Отскок! Кликни снова"); }
                else Finish(false, "Пропущен отскок — серия завершена");
                return;
            }
            if (Skin == BallSkin.Footbag && floor) { Finish(false, "Сокс упал — серия завершена"); return; }
            if (!ShotArmed || TapGame) return;
            if (ball.Contacts != Contact.None)
            {
                Bounced = true;
                if (Skin == BallSkin.Football) { ShotArmed = false; Feedback("Пас должен быть без отскока. Попробуй снова"); return; }
            }
            if (Skin == BallSkin.Tennis && Bounced && SegmentDistanceSquared(oldX, oldY, ball.X, ball.Y, Target.X + Target.Width / 2, Target.Y + Target.Height / 2) <= Math.Pow(Target.Width / 2, 2))
            {
                ShotArmed = false; AddPoint("В цель! Новый бросок — новая мишень");
                if (Active) NewTarget();
            }
            if (Skin == BallSkin.Football && ball.VX > 0 && CrossesRectangle(oldX - oldTarget.X, oldY - oldTarget.Y,
                ball.X - Target.X, ball.Y - Target.Y, new RectangleF(0, 0, Target.Width, Target.Height)))
            {
                ShotArmed = false; AddPoint("Пас принят!"); NeedsServe = Active;
            }
        }
        private void AddPoint(string text)
        {
            Score++; Feedback(text);
            if (Score >= Goal) Finish(true, "Цель выполнена!");
        }
        private void Finish(bool won, string text) { Active = Waiting = ShotArmed = false; Won = won; Message = text; }
        private void Feedback(string text) { Message = text; messageUntil = Elapsed + 2.2; }
        private void NewTarget()
        {
            float size = (float)Math.Min(126 * Scale, Math.Min(Arena.Width * .18, Arena.Height * .22));
            float x = (float)(Arena.Left + Arena.Width * (.55 + random.NextDouble() * .25) - size / 2);
            float y = (float)(Arena.Top + Arena.Height * (.34 + random.NextDouble() * .38) - size / 2);
            Target = new RectangleF(x, y, size, size);
        }
        private void UpdateReceiver()
        {
            float w = (float)Math.Min(110 * Scale, Arena.Width * .13), h = (float)Math.Min(160 * Scale, Arena.Height * .30);
            Target = new RectangleF((float)(Arena.Right - Arena.Width * .16 - w / 2),
                (float)(Arena.Top + Arena.Height * (.52 + .20 * Math.Sin(Elapsed * 1.65)) - h / 2), w, h);
        }
        private static double SegmentDistanceSquared(double ax, double ay, double bx, double by, double x, double y)
        {
            double dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
            double t = length < .000001 ? 0 : BallArt.Clamp(((x - ax) * dx + (y - ay) * dy) / length, 0, 1);
            return Math.Pow(ax + t * dx - x, 2) + Math.Pow(ay + t * dy - y, 2);
        }
        private static bool CrossesRectangle(double ax, double ay, double bx, double by, RectangleF rectangle)
        {
            double min = 0, max = 1;
            return Clip(ax, bx - ax, rectangle.Left, rectangle.Right, ref min, ref max) && Clip(ay, by - ay, rectangle.Top, rectangle.Bottom, ref min, ref max);
        }
        private static bool Clip(double origin, double delta, double low, double high, ref double min, ref double max)
        {
            if (Math.Abs(delta) < .000001) return origin >= low && origin <= high;
            double a = (low - origin) / delta, b = (high - origin) / delta;
            min = Math.Max(min, Math.Min(a, b)); max = Math.Min(max, Math.Max(a, b)); return min <= max;
        }
    }
}
