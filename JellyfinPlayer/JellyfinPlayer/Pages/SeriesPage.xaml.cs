using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Input;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class SeriesPage : PhoneApplicationPage
    {
        private BaseItemDto _series;
        private List<BaseItemDto> _seasons;

        public SeriesPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var requested = Services.NavigationService.ConsumePending() as BaseItemDto;
            // Back from an episode re-shows this instance without a parameter: the
            // season and its episodes stay, and nothing is reloaded (which would jump
            // the picker back to the first season).
            if (requested == null)
            {
                return;
            }
            _series = requested;
            ErrorText.Text = "";
            TitleText.Text = _series.Name;
            SeasonBox.ItemsSource = null;
            SeasonBox.SelectedIndex = -1;
            EpisodeList.ItemsSource = null;

            var session = Services.AppSession.Current;
            var client = session.CreateClient();

            try
            {
                var full = await client.GetItemAsync(session.UserId, _series.Id);
                if (full != null) Overview.Text = full.Overview;
            }
            catch
            {
                // Overview is optional; ignore.
            }

            try
            {
                _seasons = await client.GetSeasonsAsync(_series.Id, session.UserId);
                SeasonBox.ItemsSource = _seasons;
                if (_seasons != null && _seasons.Count > 0) SeasonBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }
        }

        private async void Season_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var season = SeasonBox.SelectedItem as BaseItemDto;
            if (season == null || _series == null) return;
            var session = Services.AppSession.Current;
            try
            {
                var eps = await session.CreateClient().GetEpisodesAsync(_series.Id, session.UserId, season.Id);
                EpisodeList.ItemsSource = Services.PosterItem.Build(eps, session.BaseUrl, session.AccessToken);
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }
        }

        private void Episode_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item != null) Services.NavigationService.Navigate(typeof(ItemDetailPage), item.Raw);
        }
    }
}
