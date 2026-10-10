Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Controls.Primitives
Imports Windows.UI.Xaml.Media
Imports Windows.UI.Xaml.Navigation

Public NotInheritable Class PlayerPage
    Inherits Page

    Private _item As BaseItemDto
    Private _plan As PlaybackPlan
    Private _client As JellyfinApiClient
    Private _timer As DispatcherTimer
    Private _updatingSeek As Boolean

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _item = TryCast(e.Parameter, BaseItemDto)
        If _item Is Nothing Then Return
        TitleText.Text = _item.Name
        _client = AppSession.Current.CreateClient()

        Dim startTicks As Long = 0
        If _item.UserData IsNot Nothing AndAlso _item.UserData.PlaybackPositionTicks.HasValue Then
            startTicks = _item.UserData.PlaybackPositionTicks.Value
        End If
        Await StartAsync(startTicks, False)
    End Sub

    Private Async Function StartAsync(startTicks As Long, forceDirect As Boolean) As Task
        Busy.IsActive = True
        ErrorText.Text = ""
        DirectButton.Visibility = Visibility.Collapsed
        Try
            _plan = Await PlaybackService.PrepareAsync(_item, startTicks)
            If forceDirect Then
                If _plan.DirectFallbackUri Is Nothing Then Throw New JellyfinException(Localized.Text("PlaybackNoDirectStream"), 0)
                _plan.IsTranscode = False
                _plan.Uri = _plan.DirectFallbackUri
            End If

            ' WP8.1 MediaElement plays both a direct stream and the progressive
            ' HTTP MP4 that Jellyfin produces for a transcode.
            MediaEl.Source = _plan.Uri
            MediaEl.Play()
            StartTimer()
            Await ReportStartAsync()
        Catch ex As Exception
            ErrorText.Text = Localized.DescribeError(ex)
            DirectButton.Visibility = If(_plan IsNot Nothing AndAlso _plan.DirectFallbackUri IsNot Nothing, Visibility.Visible, Visibility.Collapsed)
        Finally
            Busy.IsActive = False
        End Try
    End Function

    Private Sub Media_Opened(sender As Object, e As RoutedEventArgs)
        If _plan IsNot Nothing Then MediaEl.Position = _plan.StartPosition
        SeekSlider.Maximum = Math.Max(1, MediaEl.NaturalDuration.TimeSpan.TotalSeconds)
    End Sub

    Private Async Sub Media_Failed(sender As Object, e As ExceptionRoutedEventArgs)
        ErrorText.Text = Localized.Format("PlaybackFailed", If(e.ErrorMessage, Localized.Text("UnknownError")))
        DirectButton.Visibility = If(_plan IsNot Nothing AndAlso _plan.DirectFallbackUri IsNot Nothing, Visibility.Visible, Visibility.Collapsed)
        Await SafeReportStoppedAsync()
    End Sub

    Private Async Sub Media_Ended(sender As Object, e As RoutedEventArgs)
        SeekSlider.Value = SeekSlider.Maximum
        Await ReportProgressAsync()
        Await SafeReportStoppedAsync()
    End Sub

    Private Sub PlayPause_Click(sender As Object, e As RoutedEventArgs)
        If MediaEl.CurrentState = MediaElementState.Playing Then
            MediaEl.Pause()
            PlayPauseButton.Content = Localized.Text("PlayAction")
        Else
            MediaEl.Play()
            PlayPauseButton.Content = Localized.Text("PauseAction")
        End If
    End Sub

    Private Async Sub Direct_Click(sender As Object, e As RoutedEventArgs)
        Await StartAsync(Ticks.FromTimeSpan(MediaEl.Position), True)
    End Sub

    Private Async Sub Stop_Click(sender As Object, e As RoutedEventArgs)
        Await SafeReportStoppedAsync()
        MediaEl.Stop()
        NavigationService.GoBack()
    End Sub

    Private Sub Seek_Changed(sender As Object, e As RangeBaseValueChangedEventArgs)
        If _updatingSeek Then Return
        If MediaEl.NaturalDuration.HasTimeSpan AndAlso MediaEl.NaturalDuration.TimeSpan.TotalSeconds > 0 Then
            MediaEl.Position = TimeSpan.FromSeconds(SeekSlider.Value)
        End If
    End Sub

    Private Sub StartTimer()
        If _timer Is Nothing Then
            _timer = New DispatcherTimer()
            _timer.Interval = TimeSpan.FromSeconds(10)
            AddHandler _timer.Tick, AddressOf Timer_Tick
        End If
        _timer.Start()
    End Sub

    Private Async Sub Timer_Tick(sender As Object, e As Object)
        _updatingSeek = True
        SeekSlider.Value = MediaEl.Position.TotalSeconds
        _updatingSeek = False
        Await ReportProgressAsync()
    End Sub

    Private Function BuildProgress() As PlaybackProgressDto
        Return New PlaybackProgressDto With {
            .ItemId = _item.Id,
            .MediaSourceId = If(_plan IsNot Nothing, _plan.MediaSourceId, Nothing),
            .PlaySessionId = If(_plan IsNot Nothing, _plan.PlaySessionId, Nothing),
            .PositionTicks = Ticks.FromTimeSpan(MediaEl.Position),
            .CanSeek = True,
            .IsPaused = MediaEl.CurrentState = MediaElementState.Paused,
            .PlayMethod = If(_plan IsNot Nothing AndAlso _plan.IsTranscode, "Transcode", "DirectPlay")
        }
    End Function

    Private Async Function ReportStartAsync() As Task
        Try
            Await _client.ReportPlaybackStartAsync(BuildProgress())
        Catch
        End Try
    End Function

    Private Async Function ReportProgressAsync() As Task
        Try
            Await _client.ReportPlaybackProgressAsync(BuildProgress())
        Catch
        End Try
    End Function

    Private Async Function SafeReportStoppedAsync() As Task
        Try
            Await _client.ReportPlaybackStoppedAsync(BuildProgress())
        Catch
        End Try
    End Function

    Protected Overrides Async Sub OnNavigatedFrom(e As NavigationEventArgs)
        MyBase.OnNavigatedFrom(e)
        If _timer IsNot Nothing Then _timer.Stop()
        If MediaEl.CurrentState <> MediaElementState.Stopped Then
            Await SafeReportStoppedAsync()
            MediaEl.Stop()
        End If
    End Sub
End Class
