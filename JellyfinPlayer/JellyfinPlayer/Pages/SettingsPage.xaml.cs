using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Navigation;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        private readonly int[] _bitrates = { 2000000, 4000000, 8000000, 20000000 };
        private bool _suppress;

        public SettingsPage()
        {
            InitializeComponent();
            SettingsHeader.Text = Services.Localized.Text("SettingsHeader.Text");
            ServerLabel.Text = Services.Localized.Text("ServerLabel.Text");
            AccountLabel.Text = Services.Localized.Text("AccountLabel.Text");
            BitrateLabel.Text = Services.Localized.Text("BitrateLabel.Text");
            SwitchAccountButton.Content = Services.Localized.Text("SwitchAccountButton.Content");
            SignOutButton.Content = Services.Localized.Text("SignOutButton.Content");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var session = Services.AppSession.Current;
            ServerText.Text = session.BaseUrl;
            // The picker knows the person's name; the raw id is the fallback for an
            // account the migration could not name yet.
            string name = Services.AccountItem.DisplayName(session.ActiveAccount);
            AccountText.Text = string.IsNullOrEmpty(name) ? Services.Localized.Text("NotSignedIn") : name;

            _suppress = true;
            var labels = new List<string>();
            foreach (int b in _bitrates)
            {
                labels.Add((b / 1000000).ToString() + " " + Services.Localized.Text("MbpsSuffix"));
            }
            BitrateBox.ItemsSource = labels;
            int index = Array.IndexOf(_bitrates, session.PreferredBitrate);
            BitrateBox.SelectedIndex = index >= 0 ? index : 2;
            _suppress = false;
        }

        private void Bitrate_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppress) return;
            if (BitrateBox.SelectedIndex < 0) return;
            Services.AppSession.Current.PreferredBitrate = _bitrates[BitrateBox.SelectedIndex];
            Services.AppSession.Current.Save();
            StatusText.Text = Services.Localized.Text("Saved");
        }

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(AccountsPage), null);
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            // Leaves the account without forgetting it: its token stays, so coming
            // back asks for no password.
            Services.AppSession.Current.SignOut();
            Services.NavigationService.ClearBackStack();
            Services.NavigationService.Navigate(typeof(AccountsPage), null);
        }
    }
}
