namespace WindowBall
{
    internal enum BallSkin { Tennis, Basketball, Football, Footbag }

    internal sealed class BallProfile
    {
        internal readonly BallSkin Skin;
        internal readonly string Name, GameName, Rule;
        internal readonly double Mass, Bounce, Friction, Rolling, AirDrag, SpinDrag, Size, Aspect, InertiaFactor;
        private BallProfile(BallSkin skin, string name, string game, string rule, double mass, double bounce,
            double friction, double rolling, double cd, double area, double spinDrag, double size, double aspect, double inertia)
        {
            Skin = skin; Name = name; GameName = game; Rule = rule;
            Mass = mass; Bounce = bounce; Friction = friction; Rolling = rolling;
            AirDrag = .5 * 1.225 * cd * area / mass / 180;
            SpinDrag = spinDrag; Size = size; Aspect = aspect; InertiaFactor = inertia;
        }
        internal static readonly BallProfile Tennis = new BallProfile(BallSkin.Tennis, "Теннисный", "Рикошет", "Попади в 10 мишеней после отскока за 60 секунд.", .057, .74, .42, .018, .55, .0035, .35, 1, 1, .4);
        internal static readonly BallProfile Basketball = new BallProfile(BallSkin.Basketball, "Баскетбольный", "Дриблинг", "20 отскоков подряд: кликай по мячу, отбивая его об пол.", .60, .77, .65, .022, .47, .045, .26, 1.30, 1, 2.0 / 3);
        internal static readonly BallProfile Football = new BallProfile(BallSkin.Football, "Американский футбол", "Точный пас", "5 прямых пасов из левой половины в зону приёма за 60 секунд.", .415, .48, .58, .045, .32, .024, .20, 1.35, .60, .272);
        internal static readonly BallProfile Footbag = new BallProfile(BallSkin.Footbag, "Сокс с песком", "Не урони", "20 подбиваний подряд, не касаясь пола.", .060, .055, .90, .18, .65, .0028, 3.8, .82, .90, .4);

        internal static BallProfile For(BallSkin skin)
        {
            switch (skin) { case BallSkin.Basketball: return Basketball; case BallSkin.Football: return Football; case BallSkin.Footbag: return Footbag; default: return Tennis; }
        }
    }
}
