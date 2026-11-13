using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class HomePage : PhoneApplicationPage
    {
        public HomePage()
        {
            InitializeComponent();

            // The three actions live in the application bar: they cost no layout
            // space and cannot collide with the brand, which is what three full
            // buttons in the header did.
            SearchMenuItem.Text = Services.Localized.Text("SearchButton.Content");
            AccountsMenuItem.Text = Services.Localized.Text("SwitchAccountButton.Content");
            SettingsMenuItem.Text = Services.Localized.Text("SettingsButton.Content");
            ManageDownloadsButton.Content = Services.Localized.Text("ManageDownloads");

            // One pivot tab per section; the labels are the section titles.
            ResumeSection.Header = Services.Localized.Text("ResumeHeader.Text");
            NextUpSection.Header = Services.Localized.Text("NextUpHeader.Text");
            LibrariesSection.Header = Services.Localized.Text("LibrariesHeader.Text");
            PlaylistsSection.Header = Services.Localized.Text("PlaylistsHeader");
            DownloadsSection.Header = Services.Localized.Text("DownloadsHeader");

            string empty = Services.Localized.Text("NothingToShow");
            ResumeEmpty.Text = empty;
            NextUpEmpty.Text = empty;
            LibrariesEmpty.Text = empty;
            PlaylistsEmpty.Text = empty;
            DownloadsEmpty.Text = empty;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // The frame caches this page: the previous account's failure must not
            // stay on screen for the next visit.
            ErrorText.Text = "";
            ErrorText.Visibility = Visibility.Collapsed;
            if (!Services.AppSession.Current.IsLoggedIn)
            {
                Services.NavigationService.Navigate(typeof(MainPage), null);
                return;
            }
            await LoadAsync();
            AnimateIn(CurrentPanel());
        }

        private async Task LoadAsync()
        {
            var session = Services.AppSession.Current;
            var client = session.CreateClient();

            try
            {
                var views = await client.GetViewsAsync(session.UserId);
                ViewsList.ItemsSource = Services.PosterItem.Build(views, session.BaseUrl, session.AccessToken);
                ShowEmpty(LibrariesEmpty, views.Count == 0);
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                Services.AppLog.Error("Home views", ex);
                ShowError(ex);
            }

            try
            {
                var resumeItems = await client.GetResumeAsync(session.UserId, 12);
                ResumeList.ItemsSource = Services.PosterItem.Build(resumeItems, session.BaseUrl, session.AccessToken);
                ShowEmpty(ResumeEmpty, resumeItems.Count == 0);
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                Services.AppLog.Error("Home resume", ex);
                ShowError(ex);
            }

            try
            {
                var nextUp = await client.GetNextUpAsync(session.UserId, 12);
                NextUpList.ItemsSource = Services.PosterItem.Build(nextUp, session.BaseUrl, session.AccessToken);
                ShowEmpty(NextUpEmpty, nextUp.Count == 0);
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                Services.AppLog.Error("Home next up", ex);
                ShowError(ex);
            }

            try
            {
                var playlists = await client.GetPlaylistsAsync(session.UserId);
                PlaylistsList.ItemsSource = Services.PosterItem.Build(playlists, session.BaseUrl, session.AccessToken);
                ShowEmpty(PlaylistsEmpty, playlists.Count == 0);
            }
            catch (Exception ex)
            {
                if (FailureText.IsUnauthorized(ex))
                {
                    Services.NavigationService.Navigate(typeof(MainPage), session.ActiveAccount);
                    return;
                }
                Services.AppLog.Error("Home playlists", ex);
                ShowError(ex);
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

        private void ShowError(Exception ex)
        {
            ErrorText.Text = Services.Localized.DescribeError(ex);
            ErrorText.Visibility = Visibility.Visible;
        }

        // An empty section says so instead of showing a blank tab.
        private static void ShowEmpty(UIElement placeholder, bool empty)
        {
            placeholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }

        private FrameworkElement CurrentPanel()
        {
            // SelectionChanged can fire while the pivot is still being built, before
            // the panels exist.
            if (ResumePanel == null) return null;
            if (Sections.SelectedItem == NextUpSection) return NextUpPanel;
            if (Sections.SelectedItem == LibrariesSection) return LibrariesPanel;
            if (Sections.SelectedItem == PlaylistsSection) return PlaylistsPanel;
            if (Sections.SelectedItem == DownloadsSection) return DownloadsPanel;
            return ResumePanel;
        }

        // A plain Silverlight storyboard: the section fades in and slides up a
        // little each time it becomes the visible tab.
        private static void AnimateIn(UIElement element)
        {
            if (element == null) return;
            var transform = new CompositeTransform();
            element.RenderTransform = transform;

            var storyboard = new Storyboard();

            var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(240) };
            Storyboard.SetTarget(fade, element);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            storyboard.Children.Add(fade);

            var slide = new DoubleAnimation { From = 22, To = 0, Duration = TimeSpan.FromMilliseconds(240) };
            Storyboard.SetTarget(slide, transform);
            Storyboard.SetTargetProperty(slide, new PropertyPath("TranslateY"));
            storyboard.Children.Add(slide);

            storyboard.Begin();
        }

        private void Sections_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AnimateIn(CurrentPanel());
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
            ShowEmpty(DownloadsEmpty, !any);
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

        private void Search_Click(object sender, EventArgs e)
        {
            Services.NavigationService.Navigate(typeof(SearchPage), null);
        }

        private void Settings_Click(object sender, EventArgs e)
        {
            Services.NavigationService.Navigate(typeof(SettingsPage), null);
        }

        private void SwitchAccount_Click(object sender, EventArgs e)
        {
            Services.NavigationService.Navigate(typeof(AccountsPage), null);
        }
    }
}
