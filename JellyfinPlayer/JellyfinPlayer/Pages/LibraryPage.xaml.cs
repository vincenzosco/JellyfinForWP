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
    public partial class LibraryPage : PhoneApplicationPage
    {
        private const int PageSize = 50;
        private const string IncludeTypes = "Movie,Series,Episode,Video,BoxSet";
        private const string SortByName = "SortName";
        private const string SortByDateAdded = "DateCreated";

        private BaseItemDto _view;
        private int _startIndex = 0;
        private List<Services.PosterItem> _items = new List<Services.PosterItem>();
        private string _sortBy = SortByName;
        private GenreChoice _genre;
        private List<GenreChoice> _genreChoices;
        private bool _suppressGenre;

        public LibraryPage()
        {
            InitializeComponent();
            LoadMoreButton.Content = Services.Localized.Text("LoadMoreButton.Content");
            GenrePanelTitle.Text = Services.Localized.Text("GenrePanelTitle");
            CloseGenreButton.Content = Services.Localized.Text("ActionClose");
            UpdateSortLabel();
            UpdateGenreLabel();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _view = Services.NavigationService.ConsumePending() as BaseItemDto;
            // The frame caches pages, so this instance can be reused for another
            // library, or navigated back into. Start from the first page again.
            _startIndex = 0;
            _items.Clear();
            _sortBy = SortByName;
            _genre = null;
            _genreChoices = null;
            GenrePanel.Visibility = Visibility.Collapsed;
            UpdateSortLabel();
            UpdateGenreLabel();
            ErrorText.Text = "";
            TitleText.Text = _view != null ? _view.Name : Services.Localized.Text("LibraryTitle");
            if (_view == null) return;
            await LoadGenresAsync();
            await LoadNextPageAsync();
        }

        // A genre filter is a convenience: a failure leaves the list with "all" and
        // the library still opens.
        private async Task LoadGenresAsync()
        {
            try
            {
                var session = Services.AppSession.Current;
                var genres = await session.CreateClient().GetGenresAsync(session.UserId, _view.Id);
                _genreChoices = LibraryFilters.GenreChoices(genres, Services.Localized.Text("AllGenres"));
            }
            catch
            {
                _genreChoices = LibraryFilters.GenreChoices(null, Services.Localized.Text("AllGenres"));
            }
            _suppressGenre = true;
            GenreList.ItemsSource = _genreChoices;
            GenreList.SelectedIndex = 0;
            _suppressGenre = false;
        }

        private async Task LoadNextPageAsync()
        {
            var session = Services.AppSession.Current;
            Busy.Visibility = Visibility.Visible;
            try
            {
                string genreIds = _genre != null && !_genre.IsAll ? _genre.Id : null;
                var q = await session.CreateClient().GetItemsAsync(session.UserId, _view.Id, IncludeTypes, _startIndex, PageSize, _sortBy, "Ascending", genreIds);
                foreach (var poster in Services.PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken))
                {
                    _items.Add(poster);
                }
                _startIndex += PageSize;
                // Reset the source so the list re-renders the accumulated items.
                ItemsList.ItemsSource = null;
                ItemsList.ItemsSource = _items;
                LoadMoreButton.Visibility = _items.Count < q.TotalRecordCount ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
            }
            finally
            {
                Busy.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            await LoadNextPageAsync();
        }

        private void UpdateSortLabel()
        {
            SortButton.Content = Services.Localized.Text(_sortBy == SortByName ? "SortByName" : "SortByDateAdded");
        }

        private void UpdateGenreLabel()
        {
            // The button states the filter it is applying: the genre's own name, or
            // "Genre" while every genre is shown.
            string name = _genre != null && !_genre.IsAll ? _genre.Name : Services.Localized.Text("GenreFilter");
            GenreButton.Content = name;
        }

        private void Genre_Click(object sender, RoutedEventArgs e)
        {
            GenrePanel.Visibility = GenrePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CloseGenre_Click(object sender, RoutedEventArgs e)
        {
            GenrePanel.Visibility = Visibility.Collapsed;
        }

        private async void Genre_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressGenre) return;
            var chosen = GenreList.SelectedItem as GenreChoice;
            if (chosen == null) return;
            GenrePanel.Visibility = Visibility.Collapsed;
            if (_genre != null && _genre.Id == chosen.Id) return;
            _genre = chosen;
            UpdateGenreLabel();
            // A different filter means a different first page.
            _startIndex = 0;
            _items.Clear();
            ItemsList.ItemsSource = null;
            LoadMoreButton.Visibility = Visibility.Collapsed;
            if (_view != null) await LoadNextPageAsync();
        }

        private async void Sort_Click(object sender, RoutedEventArgs e)
        {
            _sortBy = _sortBy == SortByName ? SortByDateAdded : SortByName;
            UpdateSortLabel();
            // A different order means a different first page, not a second one.
            _startIndex = 0;
            _items.Clear();
            ItemsList.ItemsSource = null;
            LoadMoreButton.Visibility = Visibility.Collapsed;
            if (_view != null) await LoadNextPageAsync();
        }

        private void Item_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item != null) Services.ItemNavigator.Open(item.Raw);
        }
    }
}
