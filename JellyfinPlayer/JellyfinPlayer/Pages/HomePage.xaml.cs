using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Input;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class HomePage : PhoneApplicationPage
    {
        public HomePage()
        {
            InitializeComponent();
            SwitchAccountButton.Content = Services.Localized.Text("SwitchAccountButton.Content");
            SearchButton.Content = Services.Localized.Text("SearchButton.Content");
            SettingsButton.Content = Services.Localized.Text("SettingsButton.Content");
            ResumeHeader.Text = Services.Localized.Text("ResumeHeader.Text");
            NextUpHeader.Text = Services.Localized.Text("NextUpHeader.Text");
            PlaylistsHeader.Text = Services.Localized.Text("PlaylistsHeader");
            DownloadsHeader.Text = Services.Localized.Text("DownloadsHeader");
            ManageDownloadsButton.Content = Services.Localized.Text("ManageDownloads");
            LibrariesHeader.Text = Services.Localized.Text("LibrariesHeader.Text");
            // Both sections appear only when they have something to show.
            PlaylistsHeader.Visibility = Visibility.Collapsed;
            DownloadsHeader.Visibility = Visibility.Collapsed;
            ManageDownloadsButton.Visibility = Visibility.Collapsed;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (!Services.AppSession.Current.IsLoggedIn)
            {
                Services.NavigationService.Navigate(typeof(MainPage), null);
                return;
            }
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var session = Services.AppSession.Current;
            var client = session.CreateClient();

            try
            {
                var views = await client.GetViewsAsync(session.UserId);
                ViewsList.ItemsSource = Services.PosterItem.Build(views, session.BaseUrl, session.AccessToken);
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }

            try
            {
                var resumeItems = await client.GetResumeAsync(session.UserId, 12);
                ResumeList.ItemsSource = Services.PosterItem.Build(resumeItems, session.BaseUrl, session.AccessToken);
                ResumeHeader.Visibility = resumeItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }

            try
            {
                var nextUp = await client.GetNextUpAsync(session.UserId, 12);
                NextUpList.ItemsSource = Services.PosterItem.Build(nextUp, session.BaseUrl, session.AccessToken);
                NextUpHeader.Visibility = nextUp.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }

            try
            {
                var playlists = await client.GetPlaylistsAsync(session.UserId);
                PlaylistsList.ItemsSource = Services.PosterItem.Build(playlists, session.BaseUrl, session.AccessToken);
                PlaylistsHeader.Visibility = playlists.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }

            ShowDownloads();

            // A migrated account has no name in the store; fetch it once so the picker
            // shows a person instead of an id.
            if (string.IsNullOrEmpty(session.ActiveAccount.UserName))
            {
                try
                {
                    var user = await client.GetUserAsync(session.UserId);
                    if (user != null) session.SetUserName(session.ActiveAccount.Id, user.Name);
                }
                catch
                {
                    // The name is cosmetic: a failure here must not disturb Home.
                }
            }
        }

        private void View_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item != null) Services.NavigationService.Navigate(typeof(LibraryPage), item.Raw);
        }

        private void Item_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item != null) Services.ItemNavigator.Open(item.Raw);
        }

        // Downloads are local: they show even with no server in reach.
        private void ShowDownloads()
        {
            var rows = Services.DownloadRow.Build(Services.DownloadStore.Current.Records());
            DownloadsList.ItemsSource = rows;
            bool any = rows.Count > 0;
            DownloadsHeader.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            ManageDownloadsButton.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Playlist_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item != null) Services.NavigationService.Navigate(typeof(PlaylistPage), item.Raw);
        }

        private void Download_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.DownloadRow;
            if (item != null) Services.NavigationService.Navigate(typeof(PlayerPage), item.Record);
        }

        private void ManageDownloads_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(DownloadsPage), null);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(SearchPage), null);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(SettingsPage), null);
        }

        private void SwitchAccount_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(AccountsPage), null);
        }
    }
}
