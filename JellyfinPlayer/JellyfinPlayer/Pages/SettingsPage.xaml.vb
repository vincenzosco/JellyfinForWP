Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class SettingsPage
    Inherits Page

    Private ReadOnly _bitrates As Integer() = {2000000, 4000000, 8000000, 20000000}
    Private _suppress As Boolean

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        Dim session = AppSession.Current
        ServerText.Text = session.BaseUrl
        AccountText.Text = If(String.IsNullOrEmpty(session.UserId), "(not signed in)", session.UserId)

        _suppress = True
        Dim labels As New List(Of String)()
        For Each b In _bitrates
            labels.Add((b \ 1000000).ToString() & " Mbps")
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
        StatusText.Text = "Saved."
    End Sub

    Private Sub SignOut_Click(sender As Object, e As RoutedEventArgs)
        AppSession.Current.Clear()
        NavigationService.Navigate(GetType(MainPage), Nothing)
    End Sub
End Class
