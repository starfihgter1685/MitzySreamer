using System.IO;
using Newtonsoft.Json;

namespace MitzyStreamer
{
    public class UserSettings
    {
        public string Server { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string EncodedPassword { get; set; }

        private static string settingsPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "MitzyStreamer",
            "settings.json"
        );

        public static void Save(UserSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath, JsonConvert.SerializeObject(settings));
        }

        public static UserSettings Load()
        {
            if (File.Exists(settingsPath))
            {
                return JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(settingsPath));
            }
            return null;
        }
    }
}
