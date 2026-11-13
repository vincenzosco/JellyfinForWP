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
        // Bumped by every reset: a page that arrives after one belongs to a list that
        // no longer exists and is thrown away.
        private int _loadGeneration;
        private bool _loadingMore;

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
            var requested = Services.NavigationService.ConsumePending() as BaseItemDto;
            // Back from an item re-shows this instance without a parameter: the list,
            // the order and the genre are what the user left, so they are kept.
            if (requested == null)
            {
                if (_view == null) TitleText.Text = Services.Localized.Text("LibraryTitle");
                return;
            }

            _view = requested;
            _sortBy = SortByName;
            _genre = null;
            _genreChoices = null;
            GenrePanel.Visibility = Visibility.Collapsed;
            UpdateSortLabel();
            UpdateGenreLabel();
            ErrorText.Text = "";
            TitleText.Text = _view.Name;
            ResetList();
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
            // A page reached before a library was chosen has nothing to ask for.
            if (_view == null) return;
            var session = Services.AppSession.Current;
            int generation = _loadGeneration;
            int start = _startIndex;
            string genreIds = _genre != null && !_genre.IsAll ? _genre.Id : null;
            Busy.Visibility = Visibility.Visible;
            try
            {
                var q = await session.CreateClient().GetItemsAsync(session.UserId, _view.Id, IncludeTypes, start, PageSize, _sortBy, "Ascending", genreIds);
                // Another filter or order replaced the list while this page was on its
                // way: its entries would be appended to a list they do not belong to.
                if (generation != _loadGeneration) return;
                foreach (var poster in Services.PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken))
                {
                    _items.Add(poster);
                }
                // The server may answer with fewer entries than were asked for; the
                // next page starts after what actually arrived.
                _startIndex = start + q.Items.Count;
                // Reset the source so the list re-renders the accumulated items.
                ItemsList.ItemsSource = null;
                ItemsList.ItemsSource = _items;
                LoadMoreButton.Visibility = _items.Count < q.TotalRecordCount ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (generation == _loadGeneration) ErrorText.Text = Services.Localized.DescribeError(ex);
            }
            finally
            {
                if (generation == _loadGeneration) Busy.Visibility = Visibility.Collapsed;
            }
        }

        // Two taps before the first page arrives would ask for the same page twice
        // and append its entries twice.
        private async void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            if (_loadingMore) return;
            _loadingMore = true;
            try
            {
                await LoadNextPageAsync();
            }
            finally
            {
                _loadingMore = false;
            }
        }

        // A different filter or order is a different first page: the entries go, and
        // a page still on its way is told to discard what it brings.
        private void ResetList()
        {
            _loadGeneration++;
            _startIndex = 0;
            _items.Clear();
            ItemsList.ItemsSource = null;
            LoadMoreButton.Visibility = Visibility.Collapsed;
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
            ResetList();
            if (_view != null) await LoadNextPageAsync();
        }

        private async void Sort_Click(object sender, RoutedEventArgs e)
        {
            _sortBy = _sortBy == SortByName ? SortByDateAdded : SortByName;
            UpdateSortLabel();
            ResetList();
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
