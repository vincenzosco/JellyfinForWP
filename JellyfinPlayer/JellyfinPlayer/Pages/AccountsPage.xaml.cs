using System;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Input;
using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    /// <summary>
    /// The account picker: enter one of the saved logins, add another, forget one.
    /// </summary>
    public partial class AccountsPage : PhoneApplicationPage
    {
        public AccountsPage()
        {
            InitializeComponent();
            AccountsHeader.Text = Services.Localized.Text("AccountsHeader.Text");
            AddButton.Content = Services.Localized.Text("AddAccountButton.Content");
            RemoveButton.Content = Services.Localized.Text("RemoveAccountButton.Content");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Refresh();
        }

        private void Refresh()
        {
            var session = Services.AppSession.Current;
            AccountsList.ItemsSource = Services.AccountItem.Build(session.Accounts, session.Store.ActiveId);
            RemoveButton.IsEnabled = false;
        }

        private void Account_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RemoveButton.IsEnabled = AccountsList.SelectedItem != null;
        }

        // A single tap opens the account; selecting a row only enables Remove, so the
        // two gestures do not fight each other.
        private void Account_Tap(object sender, GestureEventArgs e)
        {
            var row = sender as FrameworkElement;
            EnterAccount(row == null ? null : row.DataContext as Services.AccountItem);
        }

        private void EnterAccount(Services.AccountItem item)
        {
            if (item == null) return;
            Services.AppSession.Current.SwitchTo(item.Account.Id);
            Services.NavigationService.ClearBackStack();
            Services.NavigationService.Navigate(typeof(HomePage), null);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            Services.NavigationService.Navigate(typeof(MainPage), null);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var item = AccountsList.SelectedItem as Services.AccountItem;
            if (item == null) return;
            Services.AppSession.Current.RemoveAccount(item.Account.Id);
            Refresh();
        }
    }
}
