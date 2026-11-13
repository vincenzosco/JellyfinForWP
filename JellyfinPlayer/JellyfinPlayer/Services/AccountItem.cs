using System.Collections.Generic;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // One row of the picker; keeps XAML binding simple (no converters), like PosterItem.
    public class AccountItem
    {
        public AccountDto Account { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public bool IsActive { get; set; }
        public string Marker { get; set; }

        public static List<AccountItem> Build(IList<AccountDto> accounts, string activeId)
        {
            var result = new List<AccountItem>();
            if (accounts == null) return result;
            foreach (var saved in accounts)
            {
                result.Add(One(saved, activeId));
            }
            return result;
        }

        public static AccountItem One(AccountDto saved, string activeId)
        {
            var item = new AccountItem();
            item.Account = saved;
            item.Title = DisplayName(saved);
            item.Subtitle = SubtitleFor(saved);
            item.IsActive = saved.Id == activeId;
            item.Marker = item.IsActive ? Localized.Text("CurrentAccountMarker") : "";
            return item;
        }

        // A migrated account has no name until Home fetches it once.
        public static string DisplayName(AccountDto saved)
        {
            if (saved == null) return "";
            if (!string.IsNullOrEmpty(saved.UserName)) return saved.UserName;
            return saved.UserId ?? "";
        }

        private static string SubtitleFor(AccountDto saved)
        {
            if (saved == null) return "";
            if (string.IsNullOrEmpty(saved.ServerName)) return saved.BaseUrl;
            return saved.ServerName + " - " + saved.BaseUrl;
        }
    }
}
