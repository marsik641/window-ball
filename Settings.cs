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
                    return settings;
                }
            }
            catch (IOException) { return new Settings(); }
            catch (UnauthorizedAccessException) { return new Settings(); }
            catch (SerializationException) { return new Settings(); }
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
