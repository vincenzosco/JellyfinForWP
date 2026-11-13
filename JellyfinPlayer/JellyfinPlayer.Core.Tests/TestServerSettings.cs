using System;
using System.Collections.Generic;
using System.IO;

namespace JellyfinPlayer.Core.Tests
{
    // Connection settings for the integration tests.
    // The password is never committed: it comes from the environment or from the
    // git-ignored test.settings.local (copied next to the test assembly).
    public class TestServerSettings
    {
        public string Url { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Password); }
        }

        public static TestServerSettings Load()
        {
            var s = new TestServerSettings();
            s.Url = Environment.GetEnvironmentVariable("JELLYFIN_TEST_URL");
            s.User = Environment.GetEnvironmentVariable("JELLYFIN_TEST_USER");
            s.Password = Environment.GetEnvironmentVariable("JELLYFIN_TEST_PASSWORD");

            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.settings.local");
            if (File.Exists(settingsPath))
            {
                var values = ParseSettings(File.ReadAllText(settingsPath));
                if (string.IsNullOrEmpty(s.Url)) s.Url = GetValue(values, "url");
                if (string.IsNullOrEmpty(s.User)) s.User = GetValue(values, "user");
                if (string.IsNullOrEmpty(s.Password)) s.Password = GetValue(values, "password");
            }

            if (string.IsNullOrEmpty(s.Url)) s.Url = "http://192.168.0.108:8096";
            if (string.IsNullOrEmpty(s.User)) s.User = "vincenzo";
            if (s.Password == null) s.Password = "";
            return s;
        }

        // key=value per line; '#' starts a comment; blank and malformed lines are ignored.
        public static Dictionary<string, string> ParseSettings(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var rawLine in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                string key = line.Substring(0, idx).Trim().ToLowerInvariant();
                string value = line.Substring(idx + 1).Trim();
                if (key.Length > 0) result[key] = value;
            }
            return result;
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            string v;
            if (values.TryGetValue(key, out v)) return v;
            return null;
        }
    }
}
