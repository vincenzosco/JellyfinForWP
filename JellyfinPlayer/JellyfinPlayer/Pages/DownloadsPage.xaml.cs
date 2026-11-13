using System.Windows;
using System.Windows.Navigation;
using System.Windows.Input;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class DownloadsPage : PhoneApplicationPage
    {
        public DownloadsPage()
        {
            InitializeComponent();
            TitleText.Text = Services.Localized.Text("DownloadsTitle");
            EmptyText.Text = Services.Localized.Text("DownloadEmpty");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Refresh();
        }

        private void Refresh()
        {
            var rows = Services.DownloadRow.Build(Services.DownloadStore.Current.Records());
            DownloadList.ItemsSource = rows;
            EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Row_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            var item = row == null ? null : row.DataContext as Services.DownloadRow;
            if (item != null) Services.NavigationService.Navigate(typeof(PlayerPage), item.Record);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            var item = button == null ? null : button.DataContext as Services.DownloadRow;
            if (item == null) return;
            Services.DownloadStore.Current.Remove(item.Record.ItemId);
            Refresh();
        }
    }
}
