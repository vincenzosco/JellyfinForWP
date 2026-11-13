using System;
using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    // The saved logins and which one is in use. Pure: no platform API, no network.
    public class AccountStore
    {
        private readonly IAccountStorage _storage;
        private readonly List<AccountDto> _accounts = new List<AccountDto>();
        private string _activeId;

        public AccountStore(IAccountStorage storage)
        {
            _storage = storage;
            Load();
        }

        public List<AccountDto> Accounts
        {
            get { return _accounts; }
        }

        public string ActiveId
        {
            get { return _activeId; }
        }

        public AccountDto Active
        {
            get { return ById(_activeId); }
        }

        // Logging in as the same user on the same server refreshes that entry
        // instead of adding a second one.
        public AccountDto AddOrUpdate(string baseUrl, string userId, string userName, string token, string serverId, string serverName,
                                      string serverVersion = null)
        {
            string normalized = Normalize(baseUrl);
            var account = Find(normalized, userId);
            if (account == null)
            {
                account = new AccountDto { Id = Guid.NewGuid().ToString("N") };
                _accounts.Add(account);
            }
            account.BaseUrl = normalized;
            account.UserId = userId;
            account.UserName = userName;
            account.AccessToken = token;
            account.ServerId = serverId;
            if (!string.IsNullOrEmpty(serverName)) account.ServerName = serverName;
            if (!string.IsNullOrEmpty(serverVersion)) account.ServerVersion = serverVersion;
            _activeId = account.Id;
            Save();
            return account;
        }

        public void SetActive(string id)
        {
            _activeId = id;
            Save();
        }

        // Leaving an account does not forget it: the token stays, so coming back
        // asks for no password.
        public void ClearActive()
        {
            _activeId = null;
            Save();
        }

        public void Remove(string id)
        {
            for (int i = _accounts.Count - 1; i >= 0; i--)
            {
                if (_accounts[i].Id == id) _accounts.RemoveAt(i);
            }
            if (_activeId == id) _activeId = null;
            Save();
        }

        // For a name the migration could not know - the token is left alone.
        public void SetUserName(string id, string userName)
        {
            var account = ById(id);
            if (account == null || string.IsNullOrEmpty(userName)) return;
            account.UserName = userName;
            Save();
        }

        public AccountDto Find(string baseUrl, string userId)
        {
            string normalized = Normalize(baseUrl);
            foreach (var account in _accounts)
            {
                if (string.Equals(account.BaseUrl, normalized, StringComparison.OrdinalIgnoreCase) && account.UserId == userId) return account;
            }
            return null;
        }

        public AccountDto ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var account in _accounts)
            {
                if (account.Id == id) return account;
            }
            return null;
        }

        // An app upgraded from the single-session build still has its four loose
        // keys: they become the first account, active, so the upgrade keeps the
        // session. Only ever runs when there are no accounts yet.
        public AccountDto MigrateFromLegacy(string baseUrl, string token, string userId, string serverId)
        {
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token)) return null;
            if (_accounts.Count > 0) return null;
            return AddOrUpdate(baseUrl, userId, null, token, serverId, null);
        }

        // ---- persistence ----

        public void Load()
        {
            _accounts.Clear();
            _activeId = null;
            string raw;
            try
            {
                raw = _storage.Load();
            }
            catch
            {
                // Unreadable storage: start empty and let the login screen show.
                return;
            }
            if (string.IsNullOrEmpty(raw)) return;
            AccountDocumentDto document;
            try
            {
                document = Json.Deserialize<AccountDocumentDto>(raw);
            }
            catch
            {
                // A corrupt document counts as no accounts; the next save
                // overwrites it.
                return;
            }
            if (document == null || document.Accounts == null) return;
            foreach (var account in document.Accounts)
            {
                if (account != null && !string.IsNullOrEmpty(account.Id)) _accounts.Add(account);
            }
            // A pointer to an account that is not there means nothing is active.
            if (ById(document.ActiveId) != null) _activeId = document.ActiveId;
        }

        public void Save()
        {
            var document = new AccountDocumentDto { Accounts = _accounts, ActiveId = _activeId };
            _storage.Save(Json.Serialize(document));
        }

        // Same normalization JellyfinUrls applies to the base address.
        public static string Normalize(string baseUrl)
        {
            return (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        }
    }
}
