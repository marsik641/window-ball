using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace WindowBall
{
    [DataContract]
    internal sealed class Settings
    {
        [DataMember] internal int Diameter = 80;
        [DataMember] internal double Bounce = .80;
        [DataMember] internal bool Gravity = true;
        [DataMember] internal bool AboveTaskbar = true;
        [DataMember] internal bool Welcomed;
        [DataMember] internal BallSkin Skin;
        [DataMember] internal int BestTennis, BestBasketball, BestFootball, BestFootbag;
        internal static string FileName { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowBall.settings.json"); } }
        internal static Settings Load()
        {
            try
            {
                using (FileStream stream = File.OpenRead(FileName))
                {
                    Settings settings = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                    if (settings == null) return new Settings();
                    if (settings.Diameter != 56 && settings.Diameter != 80 && settings.Diameter != 112) settings.Diameter = 80;
                    settings.Bounce = BallArt.Clamp(settings.Bounce, .55, .96);
                    if (!Enum.IsDefined(typeof(BallSkin), settings.Skin)) settings.Skin = BallSkin.Tennis;
                    return settings;
                }
            }
            catch (IOException) { return new Settings(); }
            catch (UnauthorizedAccessException) { return new Settings(); }
            catch (SerializationException) { return new Settings(); }
        }
        internal int Best(BallSkin skin)
        {
            switch (skin) { case BallSkin.Basketball: return BestBasketball; case BallSkin.Football: return BestFootball; case BallSkin.Footbag: return BestFootbag; default: return BestTennis; }
        }
        internal bool Record(BallSkin skin, int score)
        {
            if (score <= Best(skin)) return false;
            switch (skin) { case BallSkin.Basketball: BestBasketball = score; break; case BallSkin.Football: BestFootball = score; break; case BallSkin.Footbag: BestFootbag = score; break; default: BestTennis = score; break; }
            return true;
        }
        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileName));
                using (FileStream stream = File.Create(FileName)) new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, this);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
