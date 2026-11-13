Imports System
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Microsoft.Phone.Controls
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Threading

Partial Public Class PlayerPage
    Inherits PhoneApplicationPage

    ' The controls may hide themselves only while the video is playing, and only
    ' after this long without a tap. Paused or buffering, they stay put.
    Private Const ControlsSeconds As Integer = 4
    ' The ui timer beats once a second; the server hears about the position every
    ' tenth beat, which keeps the 10 s reporting cadence.
    Private Const BeatsBetweenReports As Integer = 10
    Private Const SeekGraceSeconds As Integer = 2

    Private _item As BaseItemDto
    Private _plan As PlaybackPlan
    ' The URL that is playing right now, and whether the automatic fallback to the
    ' transcode has already been spent for this attempt. The rule itself lives in
    ' Shared (StreamFallback), so it is covered by the desktop tests.
    Private _playingUri As Uri
    Private _retriedTranscode As Boolean
    Private _client As JellyfinApiClient
    Private _uiTimer As DispatcherTimer
    Private _beats As Integer
    Private _controlsRemaining As Integer
    Private _seekGraceUntil As DateTime
    Private _updatingSeek As Boolean
    Private _surface As PlayerSurfaceController
    ' The tracks this playback was started with; Nothing means the default audio
    ' and no subtitle. Changing either restarts the item at the same position.
    Private _audioIndex As Integer?
    Private _subtitleIndex As Integer?
    Private _suppressTracks As Boolean

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _surface = New PlayerSurfaceController(New SilverlightPlayerShell(Me))
        _item = TryCast(Services.NavigationService.ConsumePending(), BaseItemDto)
        If _item Is Nothing Then Return
        ' The frame caches this page: a track chosen for the previous item must not
        ' leak into the next one.
        _audioIndex = Nothing
        _subtitleIndex = Nothing
        TitleText.Text = _item.Name
        _client = AppSession.Current.CreateClient()
        BufferingText.Text = Localized.Text("BufferingText.Text")
        DirectButton.Content = Localized.Text("DirectButton.Content")
        TracksTitle.Text = Localized.Text("TracksTitle")
        AudioTracksLabel.Text = Localized.Text("AudioTracksLabel")
        SubtitleTracksLabel.Text = Localized.Text("SubtitleTracksLabel")
        CloseTracksButton.Content = Localized.Text("ActionClose")
        NameActions()
        ShowControls()

        ' The hardware back button gives up fullscreen before it gives up the page.
        Services.NavigationService.BackInterceptor = AddressOf OnBackPressed

        Dim startTicks As Long = 0
        If _item.UserData IsNot Nothing AndAlso _item.UserData.PlaybackPositionTicks.HasValue Then
            startTicks = _item.UserData.PlaybackPositionTicks.Value
        End If
        Await StartAsync(startTicks, False)
    End Sub

    ' True means the page consumed the back press.
    Private Function OnBackPressed() As Boolean
        If Not _surface.IsFullScreen Then Return False
        LeaveFullScreen()
        Return True
    End Function

    Private Async Function StartAsync(startTicks As Long, forceDirect As Boolean,
                                      Optional audioIndex As Integer? = Nothing,
                                      Optional subtitleIndex As Integer? = Nothing) As Task
        SetBuffering(True)
        ErrorText.Text = ""
        DirectButton.Visibility = Visibility.Collapsed
        ' A forced direct retry cannot carry a chosen track: the original file is
        ' the file, with its own default audio and no burned-in subtitle.
        _audioIndex = If(forceDirect, Nothing, audioIndex)
        _subtitleIndex = If(forceDirect, Nothing, subtitleIndex)
        Try
            _plan = Await PlaybackService.PrepareAsync(_item, startTicks, _audioIndex, _subtitleIndex)
            If forceDirect Then
                If _plan.DirectFallbackUri Is Nothing Then Throw New JellyfinException(Localized.Text("PlaybackNoDirectStream"), 0)
                _plan.IsTranscode = False
                _plan.Uri = _plan.DirectFallbackUri
            End If

            ' Silverlight MediaElement plays both a direct stream and the progressive
            ' HTTP MP4 that Jellyfin produces for a transcode.
            _retriedTranscode = False
            MediaEl.Source = _plan.Uri
            _playingUri = _plan.Uri
            MediaEl.Play()
            StartUiTimer()
            BuildTracks()
            Await ReportStartAsync()
        Catch ex As Exception
            SetBuffering(False)
            ErrorText.Text = Localized.DescribeError(ex)
            DirectButton.Visibility = If(_plan IsNot Nothing AndAlso _plan.DirectFallbackUri IsNot Nothing, Visibility.Visible, Visibility.Collapsed)
        End Try
    End Function

    ' The duration is unknown until the media opens, and it stays unknown while a
    ' transcode is still starting - reading NaturalDuration then returns false.
    Private Function Duration() As TimeSpan
        If MediaEl.NaturalDuration.HasTimeSpan Then Return MediaEl.NaturalDuration.TimeSpan
        Return TimeSpan.Zero
    End Function

    Private Sub Media_Opened(sender As Object, e As RoutedEventArgs)
        If _plan IsNot Nothing Then MediaEl.Position = _plan.StartPosition
        _seekGraceUntil = DateTime.UtcNow.AddSeconds(SeekGraceSeconds)
        ShowDuration()
        ShowPlayingState()
        ShowControls()
    End Sub

    Private Sub Media_StateChanged(sender As Object, e As RoutedEventArgs)
        ShowPlayingState()
        _surface.SetPlaying(MediaEl.CurrentState = MediaElementState.Playing)
        SetBuffering(MediaEl.CurrentState = MediaElementState.Opening OrElse
                     MediaEl.CurrentState = MediaElementState.Buffering OrElse
                     (MediaEl.CurrentState = MediaElementState.Playing AndAlso MediaEl.BufferingProgress < 1))
    End Sub

    Private Sub Media_BufferingChanged(sender As Object, e As RoutedEventArgs)
        If MediaEl.CurrentState <> MediaElementState.Playing Then Return
        SetBuffering(MediaEl.BufferingProgress < 1)
    End Sub

    Private Async Sub Media_Failed(sender As Object, e As ExceptionRoutedEventArgs)
        SetBuffering(False)
        ShowPlayingState()
        ' The controls stay on screen: the way out must never be hidden by a failure.
        ShowControls()

        ' A direct stream that failed is worth retrying through the transcode, on
        ' its own: the file may simply be one this phone cannot open. Exactly once,
        ' and never on the URL that just failed, so a second failure still reaches
        ' the user as a sentence instead of bouncing between two broken URLs.
        Dim alternative = StreamFallback.Alternative(_playingUri,
                                                    If(_plan IsNot Nothing, _plan.TranscodeFallbackUri, Nothing),
                                                    _retriedTranscode)
        If alternative IsNot Nothing Then
            _retriedTranscode = True
            _plan.IsTranscode = True
            ErrorText.Text = ""
            SetBuffering(True)
            MediaEl.Source = alternative
            _playingUri = alternative
            MediaEl.Play()
            ' The same PlaySessionId, so this updates the session the server already
            ' knows about with the play method that is now actually running.
            Await ReportStartAsync()
            Return
        End If

        ErrorText.Text = Localized.Format("PlaybackFailed", If(e.ErrorException IsNot Nothing AndAlso e.ErrorException.Message IsNot Nothing,
                                                               e.ErrorException.Message, Localized.Text("UnknownError")))
        DirectButton.Visibility = If(_plan IsNot Nothing AndAlso _plan.DirectFallbackUri IsNot Nothing, Visibility.Visible, Visibility.Collapsed)
        Await SafeReportStoppedAsync()
    End Sub

    Private Async Sub Media_Ended(sender As Object, e As RoutedEventArgs)
        SetBuffering(False)
        If Duration() > TimeSpan.Zero Then
            _updatingSeek = True
            SeekSlider.Value = Duration().TotalSeconds
            _updatingSeek = False
        End If
        ShowPlayingState()
        ShowControls()
        Await ReportProgressAsync()
        Await SafeReportStoppedAsync()
        Await PlayNextEpisodeAsync()
    End Sub

    Private Sub PlayPause_Click(sender As Object, e As RoutedEventArgs)
        If MediaEl.CurrentState = MediaElementState.Playing Then
            MediaEl.Pause()
        Else
            MediaEl.Play()
        End If
        ShowPlayingState()
        ShowControls()
    End Sub

    Private Sub SkipBack_Click(sender As Object, e As RoutedEventArgs)
        SeekTo(SeekTarget.Relative(MediaEl.Position, TimeSpan.FromSeconds(-10), Duration()))
    End Sub

    Private Sub SkipForward_Click(sender As Object, e As RoutedEventArgs)
        SeekTo(SeekTarget.Relative(MediaEl.Position, TimeSpan.FromSeconds(30), Duration()))
    End Sub

    Private Sub Mute_Click(sender As Object, e As RoutedEventArgs)
        MediaEl.IsMuted = Not MediaEl.IsMuted
        ShowMuteState()
    End Sub

    Private Sub Fit_Click(sender As Object, e As RoutedEventArgs)
        MediaEl.Stretch = If(MediaEl.Stretch = Stretch.Uniform, Stretch.UniformToFill, Stretch.Uniform)
        ShowStretchState()
    End Sub

    Private Sub FullScreen_Click(sender As Object, e As RoutedEventArgs)
        If _surface.IsFullScreen Then
            LeaveFullScreen()
        Else
            _surface.EnterFullScreen()
            ShowFullScreenState()
            If MediaEl.CurrentState = MediaElementState.Playing Then HideControls()
        End If
    End Sub

    Private Sub LeaveFullScreen()
        _surface.ExitFullScreen()
        ShowFullScreenState()
        ShowControls()
    End Sub

    Private Async Sub Direct_Click(sender As Object, e As RoutedEventArgs)
        Await StartAsync(Ticks.FromTimeSpan(MediaEl.Position), True)
    End Sub

    ' ---- tracks ---------------------------------------------------------

    ' The panel is rebuilt after every start, because a transcode requested for a
    ' subtitle can come back with a different set of streams than direct play.
    Private Sub BuildTracks()
        If _plan Is Nothing Then Return
        Dim streams = _plan.Tracks
        Dim audio = TrackOptions.Audio(streams)
        Dim subtitles = TrackOptions.Subtitles(streams, Localized.Text("SubtitlesOff"))

        _suppressTracks = True
        AudioList.ItemsSource = audio
        SubtitleList.ItemsSource = subtitles
        AudioList.SelectedIndex = IndexOfOption(audio, If(_audioIndex, TrackOptions.DefaultAudioIndex(streams)))
        SubtitleList.SelectedIndex = IndexOfOption(subtitles, _subtitleIndex)
        _suppressTracks = False

        ' One audio track and no subtitles is nothing to choose from.
        TracksButton.Visibility = If(audio.Count > 1 OrElse subtitles.Count > 1, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Shared Function IndexOfOption(options As List(Of TrackOption), index As Integer?) As Integer
        If options Is Nothing Then Return -1
        For i = 0 To options.Count - 1
            If Nullable.Equals(options(i).Index, index) Then Return i
        Next
        Return -1
    End Function

    Private Sub Tracks_Click(sender As Object, e As RoutedEventArgs)
        TracksPanel.Visibility = If(TracksPanel.Visibility = Visibility.Visible, Visibility.Collapsed, Visibility.Visible)
        ShowControls()
    End Sub

    Private Sub CloseTracks_Click(sender As Object, e As RoutedEventArgs)
        TracksPanel.Visibility = Visibility.Collapsed
    End Sub

    Private Async Sub Audio_Changed(sender As Object, e As SelectionChangedEventArgs)
        If _suppressTracks Then Return
        Dim chosen = TryCast(AudioList.SelectedItem, TrackOption)
        If chosen Is Nothing OrElse _plan Is Nothing Then Return
        ' The default audio is sent as Nothing, so it still direct-plays.
        Await RestartForTracksAsync(TrackOptions.AudioSelection(_plan.Tracks, chosen.Index), _subtitleIndex)
    End Sub

    Private Async Sub Subtitle_Changed(sender As Object, e As SelectionChangedEventArgs)
        If _suppressTracks Then Return
        Dim chosen = TryCast(SubtitleList.SelectedItem, TrackOption)
        If chosen Is Nothing OrElse _plan Is Nothing Then Return
        Await RestartForTracksAsync(_audioIndex, chosen.Index)
    End Sub

    Private Async Function RestartForTracksAsync(audioIndex As Integer?, subtitleIndex As Integer?) As Task
        Dim position = Ticks.FromTimeSpan(MediaEl.Position)
        TracksPanel.Visibility = Visibility.Collapsed
        Await SafeReportStoppedAsync()
        MediaEl.Stop()
        Await StartAsync(position, False, audioIndex, subtitleIndex)
    End Function

    ' An episode that ends moves on to the next one in place, so Back still leaves
    ' the player instead of walking into a finished episode.
    Private Async Function PlayNextEpisodeAsync() As Task
        If _item Is Nothing OrElse Not String.Equals(_item.Type, "Episode", StringComparison.OrdinalIgnoreCase) Then Return
        Dim nextEpisode = Await PlaybackService.NextEpisodeAsync(_client, AppSession.Current.UserId, _item)
        If nextEpisode Is Nothing Then Return
        _item = nextEpisode
        TitleText.Text = _item.Name
        ErrorText.Text = ""
        TracksPanel.Visibility = Visibility.Collapsed
        Await StartAsync(0, False)
    End Function

    Private Async Sub Stop_Click(sender As Object, e As RoutedEventArgs)
        Await SafeReportStoppedAsync()
        MediaEl.Stop()
        Services.NavigationService.GoBack()
    End Sub

    ' A tap anywhere toggles the controls: whatever is hidden is one tap away.
    Private Sub Root_Tap(sender As Object, e As GestureEventArgs)
        If OverlayRoot.Visibility = Visibility.Visible Then
            HideControls()
        Else
            ShowControls()
        End If
    End Sub

    Private Sub Seek_Changed(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If _updatingSeek Then Return
        SeekTo(SeekTarget.Absolute(SeekSlider.Value, Duration()))
    End Sub

    Private Sub SeekTo(target As TimeSpan)
        If Duration() <= TimeSpan.Zero Then Return
        _seekGraceUntil = DateTime.UtcNow.AddSeconds(SeekGraceSeconds)
        MediaEl.Position = target
        '_updatingSeek keeps this write, and the beats that follow it, from
        ' being read back as a user seek.
        _updatingSeek = True
        SeekSlider.Value = target.TotalSeconds
        _updatingSeek = False
        PositionText.Text = Ticks.FormatSpan(target)
    End Sub

    Private Sub StartUiTimer()
        If _uiTimer Is Nothing Then
            _uiTimer = New DispatcherTimer()
            _uiTimer.Interval = TimeSpan.FromSeconds(1)
            AddHandler _uiTimer.Tick, AddressOf UiTimer_Tick
        End If
        _beats = 0
        _uiTimer.Start()
    End Sub

    Private Async Sub UiTimer_Tick(sender As Object, e As EventArgs)
        _beats += 1
        TickUi()
        If _beats Mod BeatsBetweenReports = 0 Then Await ReportProgressAsync()
    End Sub

    Private Sub TickUi()
        PositionText.Text = Ticks.FormatSpan(MediaEl.Position)

        ' While the video plays, the slider follows it - but never while the user
        ' is dragging it, and not for a moment after a seek.
        If MediaEl.CurrentState = MediaElementState.Playing AndAlso
           Duration() > TimeSpan.Zero AndAlso
           DateTime.UtcNow >= _seekGraceUntil Then
            _updatingSeek = True
            SeekSlider.Maximum = Duration().TotalSeconds
            SeekSlider.Value = Math.Min(MediaEl.Position.TotalSeconds, Duration().TotalSeconds)
            _updatingSeek = False
            DurationText.Text = Ticks.FormatSpan(Duration())
        End If

        ' The track picker is something the user is reading: the controls wait while
        ' it is open instead of hiding the panel out from under a choice.
        If OverlayRoot.Visibility = Visibility.Visible AndAlso
           TracksPanel.Visibility <> Visibility.Visible AndAlso
           MediaEl.CurrentState = MediaElementState.Playing Then
            _controlsRemaining -= 1
            If _controlsRemaining <= 0 Then HideControls()
        End If
    End Sub

    Private Sub ShowDuration()
        If Duration() <= TimeSpan.Zero Then Return
        _updatingSeek = True
        SeekSlider.Maximum = Duration().TotalSeconds
        _updatingSeek = False
        DurationText.Text = Ticks.FormatSpan(Duration())
    End Sub

    Private Sub ShowControls()
        OverlayRoot.Visibility = Visibility.Visible
        _controlsRemaining = ControlsSeconds
    End Sub

    Private Sub HideControls()
        OverlayRoot.Visibility = Visibility.Collapsed
        TracksPanel.Visibility = Visibility.Collapsed
        _controlsRemaining = 0
    End Sub

    Private Sub SetBuffering(active As Boolean)
        BufferingRing.Visibility = If(active, Visibility.Visible, Visibility.Collapsed)
        BufferingText.Visibility = If(active, Visibility.Visible, Visibility.Collapsed)
        If active Then ShowControls()
    End Sub

    ' Every icon button shows its state twice: which of the two Paths is visible,
    ' and the accessible name a screen reader announces. The name describes the
    ' action the tap will perform next.
    Private Sub ShowPlayingState()
        Dim playing = MediaEl.CurrentState = MediaElementState.Playing
        PlayIcon.Visibility = If(playing, Visibility.Collapsed, Visibility.Visible)
        PauseIcon.Visibility = If(playing, Visibility.Visible, Visibility.Collapsed)
        SetName(PlayPauseButton, Localized.Text(If(playing, "PauseAction", "PlayAction")))
    End Sub

    Private Sub ShowMuteState()
        MuteSlash.Visibility = If(MediaEl.IsMuted, Visibility.Visible, Visibility.Collapsed)
        SetName(MuteButton, Localized.Text(If(MediaEl.IsMuted, "ActionUnmute", "ActionMute")))
    End Sub

    Private Sub ShowStretchState()
        Dim fill = MediaEl.Stretch = Stretch.UniformToFill
        IconFit.Visibility = If(fill, Visibility.Collapsed, Visibility.Visible)
        IconFill.Visibility = If(fill, Visibility.Visible, Visibility.Collapsed)
        SetName(FitButton, Localized.Text(If(fill, "ActionStretchFit", "ActionStretchFill")))
    End Sub

    Private Sub ShowFullScreenState()
        IconEnterFullScreen.Visibility = If(_surface.IsFullScreen, Visibility.Collapsed, Visibility.Visible)
        IconExitFullScreen.Visibility = If(_surface.IsFullScreen, Visibility.Visible, Visibility.Collapsed)
        SetName(FullScreenButton, Localized.Text(If(_surface.IsFullScreen, "ActionExitFullScreen", "ActionFullScreen")))
        SetName(TracksButton, Localized.Text("ActionTracks"))
    End Sub

    Private Sub NameActions()
        SetName(CloseButton, Localized.Text("ActionClose"))
        SetName(SkipBackButton, Localized.Text("ActionSkipBack"))
        SetName(SkipForwardButton, Localized.Text("ActionSkipForward"))
        SetName(SeekSlider, Localized.Text("ActionSeek"))
        ShowPlayingState()
        ShowMuteState()
        ShowStretchState()
        ShowFullScreenState()
    End Sub

    Private Sub SetName(element As FrameworkElement, name As String)
        System.Windows.Automation.AutomationProperties.SetName(element, name)
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

    ' However the user leaves - the close button, the hardware back button, a tap
    ' on a notification - the status bar, the rotation lock and the screen timeout
    ' go back to normal, so the next screen looks the way it always does.
    Protected Overrides Async Sub OnNavigatedFrom(e As NavigationEventArgs)
        MyBase.OnNavigatedFrom(e)
        If _uiTimer IsNot Nothing Then _uiTimer.Stop()
        Services.NavigationService.BackInterceptor = Nothing
        _surface.RestoreAll()
        If MediaEl.CurrentState <> MediaElementState.Stopped Then
            Await SafeReportStoppedAsync()
            MediaEl.Stop()
        End If
    End Sub
End Class
