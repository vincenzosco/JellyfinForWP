using System;
using System.Collections.Generic;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Input;

namespace JellyfinPlayer
{
    public partial class PlaylistPage : PhoneApplicationPage
    {
        private BaseItemDto _playlist;
        private List<BaseItemDto> _entries;

        public PlaylistPage()
        {
            InitializeComponent();
            PlayAllButton.Content = Services.Localized.Text("PlayAll");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _playlist = Services.NavigationService.ConsumePending() as BaseItemDto;
            if (_playlist == null) return;
            TitleText.Text = _playlist.Name;
            ErrorText.Text = "";
            // This instance is reused for another playlist: no entries from the old
            // one, and the button back on for a playlist that has them.
            _entries = null;
            EntryList.ItemsSource = null;
            PlayAllButton.Visibility = Visibility.Visible;

            var session = Services.AppSession.Current;
            try
            {
                _entries = await session.CreateClient().GetPlaylistItemsAsync(_playlist.Id, session.UserId);
                if (_entries == null || _entries.Count == 0)
                {
                    ErrorText.Text = Services.Localized.Text("PlaylistEmpty");
                    PlayAllButton.Visibility = Visibility.Collapsed;
                    return;
                }
                EntryList.ItemsSource = Services.PosterItem.Build(_entries, session.BaseUrl, session.AccessToken);
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
                PlayAllButton.Visibility = Visibility.Collapsed;
            }
        }

        private void PlayAll_Click(object sender, RoutedEventArgs e)
        {
            PlayAt(0);
        }

        private void Entry_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item == null || _entries == null) return;
            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Id == item.Id)
                {
                    PlayAt(index);
                    return;
                }
            }
        }

        // The whole playlist travels with the player, so the entries after this one
        // continue on their own instead of stopping at the end of the item.
        private void PlayAt(int index)
        {
            var item = PlayQueue.At(_entries, index);
            if (item == null) return;
            Services.NavigationService.Navigate(typeof(PlayerPage), new PlayRequest
            {
                Item = item,
                Queue = _entries,
                Index = index
            });
        }
    }
}
