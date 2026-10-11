Imports System
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

''' <summary>
''' The account picker: enter one of the saved logins, add another, forget one.
''' </summary>
Public NotInheritable Class AccountsPage
    Inherits Page

    Public Sub New()
        InitializeComponent()
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
    Private Sub Account_Click(sender As Object, e As ItemClickEventArgs)
        EnterAccount(TryCast(e.ClickedItem, AccountItem))
    End Sub

    Private Sub EnterAccount(item As AccountItem)
        If item Is Nothing Then Return
        AppSession.Current.SwitchTo(item.Account.Id)
        NavigationService.ClearBackStack()
        NavigationService.Navigate(GetType(HomePage), Nothing)
    End Sub

    Private Sub Add_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(MainPage), Nothing)
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(AccountsList.SelectedItem, AccountItem)
        If item Is Nothing Then Return
        AppSession.Current.RemoveAccount(item.Account.Id)
        Refresh()
    End Sub
End Class
