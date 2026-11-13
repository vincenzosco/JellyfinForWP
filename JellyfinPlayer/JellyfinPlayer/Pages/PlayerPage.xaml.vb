Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.IsolatedStorage
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
    Private _suppressQuality As Boolean
    ' A playlist: the item being played, the entries, and which one it is. Empty
    ' for a plain item, which then follows the next-episode rule.
    Private _queue As List(Of BaseItemDto)
    Private _queueIndex As Integer = -1
    ' Set when this item was opened from the downloads list: its stream is local
    ' and no report ever leaves the phone.
    Private _download As DownloadRecordDto
    Private _localStream As IsolatedStorageFileStream

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        _surface = New PlayerSurfaceController(New SilverlightPlayerShell(Me))
        ' The frame caches this page. Nothing that described the previous item may
        ' survive: an offline flag or a playlist here would make a normal item
        ' unplayable or start the wrong entry.
        _download = Nothing
        _queue = New List(Of BaseItemDto)()
        _queueIndex = -1
        CloseLocalStream()
        Dim pending = Services.NavigationService.ConsumePending()
        Dim request = TryCast(pending, PlayRequest)
        If request IsNot Nothing Then
            _item = request.Item
            _queue = request.Queue
            _queueIndex = request.Index
        Else
            _download = TryCast(pending, DownloadRecordDto)
            If _download IsNot Nothing Then
                _item = New BaseItemDto With {.Id = _download.ItemId, .Name = _download.Name, .RunTimeTicks = _download.RunTimeTicks}
            Else
                _item = TryCast(pending, BaseItemDto)
            End If
        End If
        If _item Is Nothing Then Return
        ' The frame caches this page: a track or queue chosen for the previous item
        ' must not leak into the next one.
        _audioIndex = Nothing
        _subtitleIndex = Nothing
        If _queue.Count = 0 Then _queueIndex = -1
        TitleText.Text = _item.Name
        _client = AppSession.Current.CreateClient()
        BufferingText.Text = Localized.Text("BufferingText.Text")
        DirectButton.Content = Localized.Text("DirectButton.Content")
        TracksTitle.Text = Localized.Text("TracksTitle")
        AudioTracksLabel.Text = Localized.Text("AudioTracksLabel")
        SubtitleTracksLabel.Text = Localized.Text("SubtitleTracksLabel")
        QualityLabel.Text = Localized.Text("QualityLabel")
        CloseTracksButton.Content = Localized.Text("ActionClose")
        NameActions()
        ShowControls()

        ' The hardware back button gives up fullscreen before it gives up the page.
        Services.NavigationService.BackInterceptor = AddressOf OnBackPressed

        Dim startTicks As Long = 0
        If _download IsNot Nothing Then
            ' The stored position is the only resume point an offline file has.
            If _download.PositionTicks.HasValue Then startTicks = _download.PositionTicks.Value
        ElseIf _item.UserData IsNot Nothing AndAlso _item.UserData.PlaybackPositionTicks.HasValue Then
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
        If _download IsNot Nothing Then
            StartOffline(startTicks)
            Return
        End If
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

    ' An offline file: the stream comes from isolated storage, the resume position
    ' from the download record, and nothing is reported. No tracks or bitrate can
    ' be chosen for a file that is already on the phone.
    Private Sub StartOffline(startTicks As Long)
        SetBuffering(True)
        ErrorText.Text = ""
        DirectButton.Visibility = Visibility.Collapsed
        Try
            CloseLocalStream()
            _localStream = DownloadStore.Current.OpenFile(_download)
            _plan = New PlaybackPlan With {.StartPosition = Ticks.ToTimeSpan(startTicks),
                                           .Tracks = New List(Of MediaStreamDto)()}
            _playingUri = Nothing
            ' Nothing to fall back to off the network.
            _retriedTranscode = True
            TitleText.Text = _download.Name
            MediaEl.SetSource(_localStream)
            MediaEl.Play()
            StartUiTimer()
            BuildTracks()
        Catch ex As Exception
            SetBuffering(False)
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
    End Sub

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
        If _download IsNot Nothing Then
            ' Finished offline: the next play starts from the beginning again.
            SaveOfflinePosition(0)
            SetBuffering(False)
            Return
        End If
        Await ReportProgressAsync()
        Await SafeReportStoppedAsync()
        Await PlayNextAsync()
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

        ' A bitrate is only meaningful while the server produces the stream.
        QualitySection.Visibility = If(_download Is Nothing, Visibility.Visible, Visibility.Collapsed)
        If _download Is Nothing Then BuildQuality()

        ' While streaming there is always a bitrate to choose, so the panel opens
        ' even for a file with one audio track and no subtitles. An offline file
        ' has nothing to choose at all.
        TracksButton.Visibility = If(_download Is Nothing OrElse audio.Count > 1 OrElse subtitles.Count > 1,
                                     Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub BuildQuality()
        Dim choices = QualityOptions.Choices(Localized.Text("MbpsSuffix"))
        _suppressQuality = True
        QualityList.ItemsSource = choices
        Dim index = QualityOptions.IndexOf(AppSession.Current.PreferredBitrate)
        QualityList.SelectedIndex = If(index >= 0, index, 2)
        _suppressQuality = False
    End Sub

    ' The preference is device-wide, like the same picker on the settings screen;
    ' the film restarts because the server is asked for a new transcode.
    Private Async Sub Quality_Changed(sender As Object, e As SelectionChangedEventArgs)
        If _suppressQuality Then Return
        Dim chosen = TryCast(QualityList.SelectedItem, QualityOption)
        If chosen Is Nothing OrElse _plan Is Nothing OrElse _download IsNot Nothing Then Return
        If chosen.Bitrate = AppSession.Current.PreferredBitrate Then Return
        AppSession.Current.PreferredBitrate = chosen.Bitrate
        AppSession.Current.Save()
        Await RestartForTracksAsync(_audioIndex, _subtitleIndex)
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
    ' A playlist continues with its next entry; only after its last one does the
    ' episode rule apply.
    Private Async Function PlayNextAsync() As Task
        Dim nextIndex = PlayQueue.NextIndex(_queue, _queueIndex)
        If nextIndex >= 0 Then
            _queueIndex = nextIndex
            _item = PlayQueue.At(_queue, nextIndex)
            If _item Is Nothing Then Return
            TitleText.Text = _item.Name
            ErrorText.Text = ""
            TracksPanel.Visibility = Visibility.Collapsed
            Await StartAsync(0, False)
            Return
        End If
        Await PlayNextEpisodeAsync()
    End Function

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
        SaveOfflinePosition(Ticks.FromTimeSpan(MediaEl.Position))
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

    ' An offline file has no session on the server: it was never started there,
    ' so reporting a position would only invent one.
    Private Async Function ReportStartAsync() As Task
        If _download IsNot Nothing Then Return
        Try
            Await _client.ReportPlaybackStartAsync(BuildProgress())
        Catch
        End Try
    End Function

    Private Async Function ReportProgressAsync() As Task
        If _download IsNot Nothing Then Return
        Try
            Await _client.ReportPlaybackProgressAsync(BuildProgress())
        Catch
        End Try
    End Function

    Private Async Function SafeReportStoppedAsync() As Task
        If _download IsNot Nothing Then Return
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
        SaveOfflinePosition(Ticks.FromTimeSpan(MediaEl.Position))
        If MediaEl.CurrentState <> MediaElementState.Stopped Then
            Await SafeReportStoppedAsync()
            MediaEl.Stop()
        End If
        CloseLocalStream()
    End Sub

    Private Sub SaveOfflinePosition(ticks As Long)
        If _download Is Nothing Then Return
        Try
            DownloadStore.Current.UpdatePosition(_download.ItemId, ticks)
            _download.PositionTicks = ticks
        Catch
            ' The position is a courtesy; a read-only store must not break leaving.
        End Try
    End Sub

    Private Sub CloseLocalStream()
        If _localStream Is Nothing Then Return
        Try
            _localStream.Dispose()
        Catch
        End Try
        _localStream = Nothing
    End Sub
End Class
