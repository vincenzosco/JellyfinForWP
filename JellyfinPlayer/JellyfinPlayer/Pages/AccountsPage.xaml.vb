Imports System
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Input

''' <summary>
''' The account picker: enter one of the saved logins, add another, forget one.
''' </summary>
Partial Public Class AccountsPage
    Inherits PhoneApplicationPage

    Public Sub New()
        InitializeComponent()
        AccountsHeader.Text = Localized.Text("AccountsHeader.Text")
        AddButton.Content = Localized.Text("AddAccountButton.Content")
        RemoveButton.Content = Localized.Text("RemoveAccountButton.Content")
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        Refresh()
    End Sub

    Private Sub Refresh()
        Dim session = AppSession.Current
        AccountsList.ItemsSource = AccountItem.Build(session.Accounts, session.Store.ActiveId)
        RemoveButton.IsEnabled = False
    End Sub

    Private Sub Account_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        RemoveButton.IsEnabled = AccountsList.SelectedItem IsNot Nothing
    End Sub

    ' A single tap opens the account; selecting a row only enables Remove, so the
    ' two gestures do not fight each other.
    Private Sub Account_Tap(sender As Object, e As GestureEventArgs)
        Dim row = TryCast(sender, FrameworkElement)
        EnterAccount(TryCast(row.DataContext, AccountItem))
    End Sub

    Private Sub EnterAccount(item As AccountItem)
        If item Is Nothing Then Return
        AppSession.Current.SwitchTo(item.Account.Id)
        Services.NavigationService.ClearBackStack()
        Services.NavigationService.Navigate(GetType(HomePage), Nothing)
    End Sub

    Private Sub Add_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(MainPage), Nothing)
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(AccountsList.SelectedItem, AccountItem)
        If item Is Nothing Then Return
        AppSession.Current.RemoveAccount(item.Account.Id)
        Refresh()
    End Sub
End Class
