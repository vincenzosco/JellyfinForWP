Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows

Partial Public Class SettingsPage
    Inherits PhoneApplicationPage

    Private ReadOnly _bitrates As Integer() = {2000000, 4000000, 8000000, 20000000}
    Private _suppress As Boolean

    Public Sub New()
        InitializeComponent()
        SettingsHeader.Text = Localized.Text("SettingsHeader.Text")
        ServerLabel.Text = Localized.Text("ServerLabel.Text")
        AccountLabel.Text = Localized.Text("AccountLabel.Text")
        BitrateLabel.Text = Localized.Text("BitrateLabel.Text")
        SwitchAccountButton.Content = Localized.Text("SwitchAccountButton.Content")
        SignOutButton.Content = Localized.Text("SignOutButton.Content")
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        Dim session = AppSession.Current
        ServerText.Text = session.BaseUrl
        ' The picker knows the person's name; the raw id is the fallback for an
        ' account the migration could not name yet.
        Dim name = AccountItem.DisplayName(session.ActiveAccount)
        AccountText.Text = If(String.IsNullOrEmpty(name), Localized.Text("NotSignedIn"), name)

        _suppress = True
        Dim labels As New List(Of String)()
        For Each b In _bitrates
            labels.Add((b \ 1000000).ToString() & " " & Localized.Text("MbpsSuffix"))
        Next
        BitrateBox.ItemsSource = labels
        Dim index = Array.IndexOf(_bitrates, session.PreferredBitrate)
        BitrateBox.SelectedIndex = If(index >= 0, index, 2)
        _suppress = False
    End Sub

    Private Sub Bitrate_Changed(sender As Object, e As SelectionChangedEventArgs)
        If _suppress Then Return
        If BitrateBox.SelectedIndex < 0 Then Return
        AppSession.Current.PreferredBitrate = _bitrates(BitrateBox.SelectedIndex)
        AppSession.Current.Save()
        StatusText.Text = Localized.Text("Saved")
    End Sub

    Private Sub Switch_Click(sender As Object, e As RoutedEventArgs)
        Services.NavigationService.Navigate(GetType(AccountsPage), Nothing)
    End Sub

    Private Sub SignOut_Click(sender As Object, e As RoutedEventArgs)
        ' Leaves the account without forgetting it: its token stays, so coming
        ' back asks for no password.
        AppSession.Current.SignOut()
        Services.NavigationService.ClearBackStack()
        Services.NavigationService.Navigate(GetType(AccountsPage), Nothing)
    End Sub
End Class
