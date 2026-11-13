using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // What the pages see: the active account plus the device-wide settings. The
    // accounts themselves live in the AccountStore, so a page that only ever
    // calls CreateClient() does not know how many logins are saved. Silverlight
    // keeps the values in IsolatedStorageSettings; every read/write is serialised
    // because that collection is not thread-safe.
    public class AppSession
    {
        private static AppSession _current;
        private static readonly object Sync = new object();

        public static AppSession Current
        {
            get
            {
                if (_current == null) _current = new AppSession();
                return _current;
            }
        }

        private const string KDeviceId = "JellyfinDeviceId";
        private const string KBitrate = "JellyfinBitrate";
        // The single-session keys of the previous build: read once by Load() to
        // migrate them, then removed.
        private const string KLegacyBaseUrl = "JellyfinBaseUrl";
        private const string KLegacyAccessToken = "JellyfinAccessToken";
        private const string KLegacyUserId = "JellyfinUserId";
        private const string KLegacyServerId = "JellyfinServerId";

        private readonly AccountStore _store;
        private string _deviceId;

        public AppSession()
        {
            _store = new AccountStore(new LocalAccountStorage());
        }

        public AccountStore Store
        {
            get { return _store; }
        }

        public List<AccountDto> Accounts
        {
            get { return _store.Accounts; }
        }

        public AccountDto ActiveAccount
        {
            get { return _store.Active; }
        }

        public string DeviceId
        {
            get { return _deviceId; }
            set { _deviceId = value; }
        }

        public int PreferredBitrate { get; set; }

        public string BaseUrl
        {
            get { return ActiveAccount == null ? null : ActiveAccount.BaseUrl; }
        }

        public string AccessToken
        {
            get { return ActiveAccount == null ? null : ActiveAccount.AccessToken; }
        }

        public string UserId
        {
            get { return ActiveAccount == null ? null : ActiveAccount.UserId; }
        }

        public string ServerId
        {
            get { return ActiveAccount == null ? null : ActiveAccount.ServerId; }
        }

        public bool IsLoggedIn
        {
            get { return !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(BaseUrl); }
        }

        public void Load()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            lock (Sync)
            {
                DeviceId = ReadString(s, KDeviceId);
                if (string.IsNullOrEmpty(DeviceId))
                {
                    DeviceId = Guid.NewGuid().ToString("N");
                    s[KDeviceId] = DeviceId;
                }

                object raw;
                if (s.TryGetValue(KBitrate, out raw) && raw != null)
                {
                    PreferredBitrate = (int)raw;
                }
                else
                {
                    PreferredBitrate = DeviceProfileBuilder.DefaultBitrate;
                }

                // An upgrade arrives with four loose keys and no document.
                _store.MigrateFromLegacy(ReadString(s, KLegacyBaseUrl), ReadString(s, KLegacyAccessToken),
                                         ReadString(s, KLegacyUserId), ReadString(s, KLegacyServerId));
                foreach (var key in new[] { KLegacyBaseUrl, KLegacyAccessToken, KLegacyUserId, KLegacyServerId })
                {
                    if (s.Contains(key)) s.Remove(key);
                }
                s.Save();
            }
        }

        public void Save()
        {
            var s = IsolatedStorageSettings.ApplicationSettings;
            lock (Sync)
            {
                s[KDeviceId] = DeviceId;
                s[KBitrate] = PreferredBitrate;
                s.Save();
            }
            _store.Save();
        }

        // A successful login: the same server and user refreshes that entry.
        public AccountDto SignIn(string baseUrl, string userId, string userName, string token, string serverId, string serverName)
        {
            var account = _store.AddOrUpdate(baseUrl, userId, userName, token, serverId, serverName);
            Save();
            return account;
        }

        public void SwitchTo(string id)
        {
            _store.SetActive(id);
            Save();
        }

        // Leaving an account keeps it (and its token) saved.
        public void SignOut()
        {
            _store.ClearActive();
            Save();
        }

        public void RemoveAccount(string id)
        {
            _store.Remove(id);
            Save();
        }

        public void SetUserName(string id, string userName)
        {
            _store.SetUserName(id, userName);
            Save();
        }

        public JellyfinApiClient CreateClient()
        {
            var account = ActiveAccount;
            var urls = new JellyfinUrls(account == null ? null : account.BaseUrl,
                                        account == null ? null : account.AccessToken);
            var client = new JellyfinApiClient(new HttpTransport(), urls, DeviceId);
            if (account != null)
            {
                client.AccessToken = account.AccessToken;
                client.UserId = account.UserId;
                client.ServerId = account.ServerId;
            }
            return client;
        }

        private static string ReadString(IDictionary<string, object> s, string key)
        {
            object v;
            if (s.TryGetValue(key, out v) && v != null) return v.ToString();
            return null;
        }
    }
}
