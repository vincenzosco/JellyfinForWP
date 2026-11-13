using System.Collections.Generic;
using System.IO.IsolatedStorage;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // The one and only place the account document meets app storage. One value
    // holds the whole document, so a save is a single atomic write. Silverlight
    // stores it in IsolatedStorageSettings, and every access is serialised because
    // the settings collection is not thread-safe.
    public class LocalAccountStorage : IAccountStorage
    {
        public const string Key = "JellyfinAccounts";
        private static readonly object Sync = new object();

        public string Load()
        {
            lock (Sync)
            {
                var values = IsolatedStorageSettings.ApplicationSettings;
                object raw;
                if (values.TryGetValue(Key, out raw) && raw != null) return raw.ToString();
                return null;
            }
        }

        public void Save(string json)
        {
            lock (Sync)
            {
                var values = IsolatedStorageSettings.ApplicationSettings;
                values[Key] = json;
                values.Save();
            }
        }
    }
}
