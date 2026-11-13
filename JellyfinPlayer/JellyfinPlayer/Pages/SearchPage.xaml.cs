using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class SearchPage : PhoneApplicationPage
    {
        public SearchPage()
        {
            InitializeComponent();
        }

        private async void QueryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            await RunSearchAsync();
        }

        private async Task RunSearchAsync()
        {
            var term = QueryBox.Text.Trim();
            if (string.IsNullOrEmpty(term)) return;

            var session = Services.AppSession.Current;
            ErrorText.Text = "";
            EmptyText.Text = Services.Localized.Text("Searching");
            EmptyText.Visibility = Visibility.Visible;

            try
            {
                var q = await session.CreateClient().SearchAsync(session.UserId, term);
                ResultsList.ItemsSource = Services.PosterItem.Build(q.Items, session.BaseUrl, session.AccessToken);
                if (q.Items.Count == 0)
                {
                    EmptyText.Text = Services.Localized.Text("NoResults");
                }
                else
                {
                    EmptyText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = Services.Localized.DescribeError(ex);
                EmptyText.Visibility = Visibility.Collapsed;
            }
        }

        private void Result_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.PosterItem;
            if (item != null) Services.ItemNavigator.Open(item.Raw);
        }
    }
}
