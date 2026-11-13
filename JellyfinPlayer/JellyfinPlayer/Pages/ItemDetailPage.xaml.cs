using System;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Media.Imaging;
using JellyfinPlayer.Shared;
using Microsoft.Phone;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class ItemDetailPage : PhoneApplicationPage
    {
        private BaseItemDto _item;
        private bool _suppressToggle;
        private DownloadRecordDto _download;
        private bool _downloading;

        public ItemDetailPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _item = Services.NavigationService.ConsumePending() as BaseItemDto;
            if (_item == null) return;

            var session = Services.AppSession.Current;
            try
            {
                var full = await session.CreateClient().GetItemAsync(session.UserId, _item.Id);
                if (full != null) _item = full;
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }

            ApplyItem();
            _download = Services.DownloadStore.Current.Find(_item.Id);
            UpdateDownloadButton();
        }

        private void UpdateDownloadButton()
        {
            DownloadButton.IsEnabled = !_downloading;
            DownloadButton.Content = Services.Localized.Text(_download != null ? "RemoveDownload" : "DownloadAction");
        }

        // The button is a toggle: download, or remove the copy that is already there.
        // The tap runs the copy; the download finishes even if the page scrolls, but
        // leaving the app stops it (there is no background agent on this platform).
        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_downloading || _item == null) return;
            if (_download != null)
            {
                Services.DownloadStore.Current.Remove(_item.Id);
                _download = null;
                DownloadProgress.Visibility = Visibility.Collapsed;
                UpdateDownloadButton();
                return;
            }

            _downloading = true;
            ErrorText.Text = "";
            DownloadProgress.Value = 0;
            DownloadProgress.Visibility = Visibility.Visible;
            UpdateDownloadButton();
            // The frame caches this page: a download finishes against the item it was
            // started for, even if the user has opened another one meanwhile.
            string downloadingItemId = _item.Id;
            try
            {
                var record = await Services.DownloadService.DownloadAsync(_item, percent =>
                {
                    Deployment.Current.Dispatcher.BeginInvoke(() => ShowPercent(percent));
                });
                if (_item != null && _item.Id == downloadingItemId) _download = record;
            }
            catch (Exception ex)
            {
                Services.AppLog.Error("Download of " + downloadingItemId, ex);
                if (_item != null && _item.Id == downloadingItemId) ErrorText.Text = Services.Localized.DescribeError(ex);
            }
            finally
            {
                _downloading = false;
                DownloadProgress.Visibility = Visibility.Collapsed;
                UpdateDownloadButton();
            }
        }

        private void ShowPercent(int percent)
        {
            if (!_downloading) return;
            DownloadProgress.Value = percent;
            DownloadButton.Content = Services.Localized.Format("Downloading", percent);
        }

        private void ApplyItem()
        {
            TitleText.Text = _item.Name;
            OverviewText.Text = _item.Overview;

            string meta = "";
            if (_item.SeriesName != null) meta += _item.SeriesName;
            if (_item.ParentIndexNumber.HasValue) meta += "  S" + _item.ParentIndexNumber.Value.ToString("00");
            if (_item.IndexNumber.HasValue) meta += "E" + _item.IndexNumber.Value.ToString("00");
            if (_item.RunTimeTicks.HasValue) meta += "  " + Ticks.FormatDuration(_item.RunTimeTicks);
            MetaText.Text = meta.Trim();

            if (Services.ImageHelper.HasBackdropImage(_item))
            {
                BackdropImage.Source = new BitmapImage(Services.ImageHelper.BackdropUrl(Services.AppSession.Current.BaseUrl, Services.AppSession.Current.AccessToken, _item.Id, 720));
            }
            else if (Services.ImageHelper.HasPrimaryImage(_item))
            {
                // No backdrop: fall back to the primary image so the banner is not empty.
                BackdropImage.Source = new BitmapImage(Services.ImageHelper.ImageUrl(Services.AppSession.Current.BaseUrl, Services.AppSession.Current.AccessToken, _item.Id, 720));
            }

            bool resuming = _item.UserData != null && _item.UserData.PlaybackPositionTicks.HasValue && _item.UserData.PlaybackPositionTicks.Value > 0;
            PlayButton.Content = Services.Localized.Text(resuming ? "ResumeAction" : "PlayAction");

            _suppressToggle = true;
            FavoriteToggle.IsChecked = _item.UserData != null && _item.UserData.IsFavorite;
            PlayedToggle.IsChecked = _item.UserData != null && _item.UserData.Played;
            _suppressToggle = false;

            FavoriteToggle.Content = Services.Localized.Text("FavoriteToggle.Content");
            PlayedToggle.Content = Services.Localized.Text("PlayedToggle.Content");
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(PlayerPage), _item);
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle) return;
            var session = Services.AppSession.Current;
            bool value = FavoriteToggle.IsChecked.HasValue && FavoriteToggle.IsChecked.Value;
            try
            {
                await session.CreateClient().SetFavoriteAsync(session.UserId, _item.Id, value);
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }
        }

        private async void Played_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle) return;
            var session = Services.AppSession.Current;
            bool value = PlayedToggle.IsChecked.HasValue && PlayedToggle.IsChecked.Value;
            try
            {
                await session.CreateClient().SetPlayedAsync(session.UserId, _item.Id, value);
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }
        }
    }
}
