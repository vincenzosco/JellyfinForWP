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
            _series = Services.NavigationService.ConsumePending() as BaseItemDto;
            if (_series == null) return;
            TitleText.Text = _series.Name;

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
