using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using JellyfinPlayer.Shared;
using JellyfinPlayer.Services;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer
{
    public partial class PlayerPage : PhoneApplicationPage
    {
        // The controls may hide themselves only while the video is playing, and only
        // after this long without a tap. Paused or buffering, they stay put.
        private const int ControlsSeconds = 4;
        // The ui timer beats once a second; the server hears about the position every
        // tenth beat, which keeps the 10 s reporting cadence.
        private const int BeatsBetweenReports = 10;
        private const int SeekGraceSeconds = 2;

        private BaseItemDto _item;
        private PlaybackPlan _plan;
        // The URL that is playing right now, and whether the automatic fallback to the
        // transcode has already been spent for this attempt. The rule itself lives in
        // Shared (StreamFallback), so it is covered by the desktop tests.
        private Uri _playingUri;
        private bool _retriedTranscode;
        private JellyfinApiClient _client;
        private DispatcherTimer _uiTimer;
        private int _beats;
        private int _controlsRemaining;
        private DateTime _seekGraceUntil;
        private bool _updatingSeek;
        private PlayerSurfaceController _surface;
        // The tracks this playback was started with; null means the default audio
        // and no subtitle. Changing either restarts the item at the same position.
        private int? _audioIndex;
        private int? _subtitleIndex;
        private bool _suppressTracks;
        private bool _suppressQuality;
        // A playlist: the item being played, the entries, and which one it is. Empty
        // for a plain item, which then follows the next-episode rule.
        private List<BaseItemDto> _queue;
        private int _queueIndex = -1;
        // Set when this item was opened from the downloads list: its stream is local
        // and no report ever leaves the phone.
        private DownloadRecordDto _download;
        private IsolatedStorageFileStream _localStream;

        public PlayerPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _surface = new PlayerSurfaceController(new SilverlightPlayerShell(this));
            // The frame caches this page. Nothing that described the previous item may
            // survive: an offline flag or a playlist here would make a normal item
            // unplayable or start the wrong entry.
            _download = null;
            _queue = new List<BaseItemDto>();
            _queueIndex = -1;
            CloseLocalStream();
            var pending = Services.NavigationService.ConsumePending();
            var request = pending as PlayRequest;
            if (request != null)
            {
                _item = request.Item;
                _queue = request.Queue;
                _queueIndex = request.Index;
            }
            else
            {
                _download = pending as DownloadRecordDto;
                if (_download != null)
                {
                    _item = new BaseItemDto { Id = _download.ItemId, Name = _download.Name, RunTimeTicks = _download.RunTimeTicks };
                }
                else
                {
                    _item = pending as BaseItemDto;
                }
            }
            if (_item == null) return;
            // The frame caches this page: a track or queue chosen for the previous item
            // must not leak into the next one.
            _audioIndex = null;
            _subtitleIndex = null;
            if (_queue.Count == 0) _queueIndex = -1;
            TitleText.Text = _item.Name;
            _client = AppSession.Current.CreateClient();
            BufferingText.Text = Localized.Text("BufferingText.Text");
            DirectButton.Content = Localized.Text("DirectButton.Content");
            TracksTitle.Text = Localized.Text("TracksTitle");
            AudioTracksLabel.Text = Localized.Text("AudioTracksLabel");
            SubtitleTracksLabel.Text = Localized.Text("SubtitleTracksLabel");
            QualityLabel.Text = Localized.Text("QualityLabel");
            CloseTracksButton.Content = Localized.Text("ActionClose");
            NameActions();
            ShowControls();

            // The hardware back button gives up fullscreen before it gives up the page.
            Services.NavigationService.BackInterceptor = OnBackPressed;

            long startTicks = 0;
            if (_download != null)
            {
                // The stored position is the only resume point an offline file has.
                if (_download.PositionTicks.HasValue) startTicks = _download.PositionTicks.Value;
            }
            else if (_item.UserData != null && _item.UserData.PlaybackPositionTicks.HasValue)
            {
                startTicks = _item.UserData.PlaybackPositionTicks.Value;
            }
            await StartAsync(startTicks, false, null, null);
        }

        // True means the page consumed the back press.
        private bool OnBackPressed()
        {
            if (!_surface.IsFullScreen) return false;
            LeaveFullScreen();
            return true;
        }

        private async Task StartAsync(long startTicks, bool forceDirect,
                                      int? audioIndex = null,
                                      int? subtitleIndex = null)
        {
            SetBuffering(true);
            ErrorText.Text = "";
            DirectButton.Visibility = Visibility.Collapsed;
            // A forced direct retry cannot carry a chosen track: the original file is
            // the file, with its own default audio and no burned-in subtitle.
            _audioIndex = forceDirect ? null : audioIndex;
            _subtitleIndex = forceDirect ? null : subtitleIndex;
            if (_download != null)
            {
                StartOffline(startTicks);
                return;
            }
            try
            {
                _plan = await PlaybackService.PrepareAsync(_item, startTicks, _audioIndex, _subtitleIndex);
                if (forceDirect)
                {
                    if (_plan.DirectFallbackUri == null) throw new JellyfinException(Localized.Text("PlaybackNoDirectStream"), 0);
                    _plan.IsTranscode = false;
                    _plan.Uri = _plan.DirectFallbackUri;
                }

                // Silverlight MediaElement plays both a direct stream and the progressive
                // HTTP MP4 that Jellyfin produces for a transcode.
                _retriedTranscode = false;
                MediaEl.Source = _plan.Uri;
                _playingUri = _plan.Uri;
                MediaEl.Play();
                StartUiTimer();
                BuildTracks();
                await ReportStartAsync();
            }
            catch (Exception ex)
            {
                AppLog.Error("Playback start for " + _item.Id, ex);
                SetBuffering(false);
                ErrorText.Text = Localized.DescribeError(ex);
                DirectButton.Visibility = _plan != null && _plan.DirectFallbackUri != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // An offline file: the stream comes from isolated storage, the resume position
        // from the download record, and nothing is reported. No tracks or bitrate can
        // be chosen for a file that is already on the phone.
        private void StartOffline(long startTicks)
        {
            SetBuffering(true);
            ErrorText.Text = "";
            DirectButton.Visibility = Visibility.Collapsed;
            try
            {
                CloseLocalStream();
                _localStream = DownloadStore.Current.OpenFile(_download);
                _plan = new PlaybackPlan
                {
                    StartPosition = Ticks.ToTimeSpan(startTicks),
                    Tracks = new List<MediaStreamDto>()
                };
                _playingUri = null;
                // Nothing to fall back to off the network.
                _retriedTranscode = true;
                TitleText.Text = _download.Name;
                MediaEl.SetSource(_localStream);
                MediaEl.Play();
                StartUiTimer();
                BuildTracks();
            }
            catch (Exception ex)
            {
                SetBuffering(false);
                ErrorText.Text = Localized.DescribeError(ex);
            }
        }

        // The duration is unknown until the media opens, and it stays unknown while a
        // transcode is still starting - reading NaturalDuration then returns false.
        private TimeSpan Duration()
        {
            if (MediaEl.NaturalDuration.HasTimeSpan) return MediaEl.NaturalDuration.TimeSpan;
            return TimeSpan.Zero;
        }

        private void Media_Opened(object sender, RoutedEventArgs e)
        {
            if (_plan != null) MediaEl.Position = _plan.StartPosition;
            _seekGraceUntil = DateTime.UtcNow.AddSeconds(SeekGraceSeconds);
            ShowDuration();
            ShowPlayingState();
            ShowControls();
        }

        private void Media_StateChanged(object sender, RoutedEventArgs e)
        {
            ShowPlayingState();
            _surface.SetPlaying(MediaEl.CurrentState == MediaElementState.Playing);
            SetBuffering(MediaEl.CurrentState == MediaElementState.Opening ||
                         MediaEl.CurrentState == MediaElementState.Buffering ||
                         (MediaEl.CurrentState == MediaElementState.Playing && MediaEl.BufferingProgress < 1));
        }

        private void Media_BufferingChanged(object sender, RoutedEventArgs e)
        {
            if (MediaEl.CurrentState != MediaElementState.Playing) return;
            SetBuffering(MediaEl.BufferingProgress < 1);
        }

        private async void Media_Failed(object sender, ExceptionRoutedEventArgs e)
        {
            SetBuffering(false);
            ShowPlayingState();
            // The controls stay on screen: the way out must never be hidden by a failure.
            ShowControls();

            // A direct stream that failed is worth retrying through the transcode, on
            // its own: the file may simply be one this phone cannot open. Exactly once,
            // and never on the URL that just failed, so a second failure still reaches
            // the user as a sentence instead of bouncing between two broken URLs.
            var alternative = StreamFallback.Alternative(_playingUri,
                                                        _plan != null ? _plan.TranscodeFallbackUri : null,
                                                        _retriedTranscode);
            if (alternative != null)
            {
                _retriedTranscode = true;
                _plan.IsTranscode = true;
                ErrorText.Text = "";
                SetBuffering(true);
                MediaEl.Source = alternative;
                _playingUri = alternative;
                MediaEl.Play();
                // The same PlaySessionId, so this updates the session the server already
                // knows about with the play method that is now actually running.
                await ReportStartAsync();
                return;
            }

            AppLog.Error("Playback failed for " + _item.Id,
                         e.ErrorException == null ? null : e.ErrorException);
            string message = e.ErrorException != null && e.ErrorException.Message != null
                ? e.ErrorException.Message
                : Localized.Text("UnknownError");
            ErrorText.Text = Localized.Format("PlaybackFailed", message);
            DirectButton.Visibility = _plan != null && _plan.DirectFallbackUri != null ? Visibility.Visible : Visibility.Collapsed;
            await SafeReportStoppedAsync();
        }

        private async void Media_Ended(object sender, RoutedEventArgs e)
        {
            SetBuffering(false);
            if (Duration() > TimeSpan.Zero)
            {
                _updatingSeek = true;
                SeekSlider.Value = Duration().TotalSeconds;
                _updatingSeek = false;
            }
            ShowPlayingState();
            ShowControls();
            if (_download != null)
            {
                // Finished offline: the next play starts from the beginning again.
                SaveOfflinePosition(0);
                SetBuffering(false);
                return;
            }
            await ReportProgressAsync();
            await SafeReportStoppedAsync();
            await PlayNextAsync();
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (MediaEl.CurrentState == MediaElementState.Playing)
            {
                MediaEl.Pause();
            }
            else
            {
                MediaEl.Play();
            }
            ShowPlayingState();
            ShowControls();
        }

        private void SkipBack_Click(object sender, RoutedEventArgs e)
        {
            SeekTo(SeekTarget.Relative(MediaEl.Position, TimeSpan.FromSeconds(-10), Duration()));
        }

        private void SkipForward_Click(object sender, RoutedEventArgs e)
        {
            SeekTo(SeekTarget.Relative(MediaEl.Position, TimeSpan.FromSeconds(30), Duration()));
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            MediaEl.IsMuted = !MediaEl.IsMuted;
            ShowMuteState();
        }

        private void Fit_Click(object sender, RoutedEventArgs e)
        {
            MediaEl.Stretch = MediaEl.Stretch == Stretch.Uniform ? Stretch.UniformToFill : Stretch.Uniform;
            ShowStretchState();
        }

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (_surface.IsFullScreen)
            {
                LeaveFullScreen();
            }
            else
            {
                _surface.EnterFullScreen();
                ShowFullScreenState();
                if (MediaEl.CurrentState == MediaElementState.Playing) HideControls();
            }
        }

        private void LeaveFullScreen()
        {
            _surface.ExitFullScreen();
            ShowFullScreenState();
            ShowControls();
        }

        private async void Direct_Click(object sender, RoutedEventArgs e)
        {
            await StartAsync(Ticks.FromTimeSpan(MediaEl.Position), true, null, null);
        }

        // ---- tracks ---------------------------------------------------------

        // The panel is rebuilt after every start, because a transcode requested for a
        // subtitle can come back with a different set of streams than direct play.
        private void BuildTracks()
        {
            if (_plan == null) return;
            var streams = _plan.Tracks;
            var audio = TrackOptions.Audio(streams);
            var subtitles = TrackOptions.Subtitles(streams, Localized.Text("SubtitlesOff"));

            _suppressTracks = true;
            AudioList.ItemsSource = audio;
            SubtitleList.ItemsSource = subtitles;
            AudioList.SelectedIndex = IndexOfOption(audio, _audioIndex ?? TrackOptions.DefaultAudioIndex(streams));
            SubtitleList.SelectedIndex = IndexOfOption(subtitles, _subtitleIndex);
            _suppressTracks = false;

            // A bitrate is only meaningful while the server produces the stream.
            QualitySection.Visibility = _download == null ? Visibility.Visible : Visibility.Collapsed;
            if (_download == null) BuildQuality();

            // While streaming there is always a bitrate to choose, so the panel opens
            // even for a file with one audio track and no subtitles. An offline file
            // has nothing to choose at all.
            TracksButton.Visibility = _download == null || audio.Count > 1 || subtitles.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BuildQuality()
        {
            var choices = QualityOptions.Choices(Localized.Text("MbpsSuffix"));
            _suppressQuality = true;
            QualityList.ItemsSource = choices;
            int index = QualityOptions.IndexOf(AppSession.Current.PreferredBitrate);
            QualityList.SelectedIndex = index >= 0 ? index : 2;
            _suppressQuality = false;
        }

        // The preference is device-wide, like the same picker on the settings screen;
        // the film restarts because the server is asked for a new transcode.
        private async void Quality_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressQuality) return;
            var chosen = QualityList.SelectedItem as QualityOption;
            if (chosen == null || _plan == null || _download != null) return;
            if (chosen.Bitrate == AppSession.Current.PreferredBitrate) return;
            AppSession.Current.PreferredBitrate = chosen.Bitrate;
            AppSession.Current.Save();
            await RestartForTracksAsync(_audioIndex, _subtitleIndex);
        }

        private static int IndexOfOption(List<TrackOption> options, int? index)
        {
            if (options == null) return -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (Nullable.Equals(options[i].Index, index)) return i;
            }
            return -1;
        }

        private void Tracks_Click(object sender, RoutedEventArgs e)
        {
            TracksPanel.Visibility = TracksPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            ShowControls();
        }

        private void CloseTracks_Click(object sender, RoutedEventArgs e)
        {
            TracksPanel.Visibility = Visibility.Collapsed;
        }

        private async void Audio_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTracks) return;
            var chosen = AudioList.SelectedItem as TrackOption;
            if (chosen == null || _plan == null) return;
            // The default audio is sent as null, so it still direct-plays.
            await RestartForTracksAsync(TrackOptions.AudioSelection(_plan.Tracks, chosen.Index), _subtitleIndex);
        }

        private async void Subtitle_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTracks) return;
            var chosen = SubtitleList.SelectedItem as TrackOption;
            if (chosen == null || _plan == null) return;
            await RestartForTracksAsync(_audioIndex, chosen.Index);
        }

        private async Task RestartForTracksAsync(int? audioIndex, int? subtitleIndex)
        {
            long position = Ticks.FromTimeSpan(MediaEl.Position);
            TracksPanel.Visibility = Visibility.Collapsed;
            await SafeReportStoppedAsync();
            MediaEl.Stop();
            await StartAsync(position, false, audioIndex, subtitleIndex);
        }

        // An episode that ends moves on to the next one in place, so Back still leaves
        // the player instead of walking into a finished episode.
        // A playlist continues with its next entry; only after its last one does the
        // episode rule apply.
        private async Task PlayNextAsync()
        {
            int nextIndex = PlayQueue.NextIndex(_queue, _queueIndex);
            if (nextIndex >= 0)
            {
                _queueIndex = nextIndex;
                _item = PlayQueue.At(_queue, nextIndex);
                if (_item == null) return;
                TitleText.Text = _item.Name;
                ErrorText.Text = "";
                TracksPanel.Visibility = Visibility.Collapsed;
                await StartAsync(0, false, null, null);
                return;
            }
            await PlayNextEpisodeAsync();
        }

        private async Task PlayNextEpisodeAsync()
        {
            if (_item == null || !string.Equals(_item.Type, "Episode", StringComparison.OrdinalIgnoreCase)) return;
            var nextEpisode = await PlaybackService.NextEpisodeAsync(_client, AppSession.Current.UserId, _item);
            if (nextEpisode == null) return;
            _item = nextEpisode;
            TitleText.Text = _item.Name;
            ErrorText.Text = "";
            TracksPanel.Visibility = Visibility.Collapsed;
            await StartAsync(0, false, null, null);
        }

        private async void Stop_Click(object sender, RoutedEventArgs e)
        {
            SaveOfflinePosition(Ticks.FromTimeSpan(MediaEl.Position));
            await SafeReportStoppedAsync();
            MediaEl.Stop();
            Services.NavigationService.GoBack();
        }

        // A tap anywhere toggles the controls: whatever is hidden is one tap away.
        private void Root_Tap(object sender, GestureEventArgs e)
        {
            if (OverlayRoot.Visibility == Visibility.Visible)
            {
                HideControls();
            }
            else
            {
                ShowControls();
            }
        }

        private void Seek_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingSeek) return;
            SeekTo(SeekTarget.Absolute(SeekSlider.Value, Duration()));
        }

        private void SeekTo(TimeSpan target)
        {
            if (Duration() <= TimeSpan.Zero) return;
            _seekGraceUntil = DateTime.UtcNow.AddSeconds(SeekGraceSeconds);
            MediaEl.Position = target;
            // _updatingSeek keeps this write, and the beats that follow it, from
            // being read back as a user seek.
            _updatingSeek = true;
            SeekSlider.Value = target.TotalSeconds;
            _updatingSeek = false;
            PositionText.Text = Ticks.FormatSpan(target);
        }

        private void StartUiTimer()
        {
            if (_uiTimer == null)
            {
                _uiTimer = new DispatcherTimer();
                _uiTimer.Interval = TimeSpan.FromSeconds(1);
                _uiTimer.Tick += UiTimer_Tick;
            }
            _beats = 0;
            _uiTimer.Start();
        }

        private async void UiTimer_Tick(object sender, EventArgs e)
        {
            _beats += 1;
            TickUi();
            if (_beats % BeatsBetweenReports == 0) await ReportProgressAsync();
        }

        private void TickUi()
        {
            PositionText.Text = Ticks.FormatSpan(MediaEl.Position);

            // While the video plays, the slider follows it - but never while the user
            // is dragging it, and not for a moment after a seek.
            if (MediaEl.CurrentState == MediaElementState.Playing &&
                Duration() > TimeSpan.Zero &&
                DateTime.UtcNow >= _seekGraceUntil)
            {
                _updatingSeek = true;
                SeekSlider.Maximum = Duration().TotalSeconds;
                SeekSlider.Value = Math.Min(MediaEl.Position.TotalSeconds, Duration().TotalSeconds);
                _updatingSeek = false;
                DurationText.Text = Ticks.FormatSpan(Duration());
            }

            // The track picker is something the user is reading: the controls wait while
            // it is open instead of hiding the panel out from under a choice.
            if (OverlayRoot.Visibility == Visibility.Visible &&
                TracksPanel.Visibility != Visibility.Visible &&
                MediaEl.CurrentState == MediaElementState.Playing)
            {
                _controlsRemaining -= 1;
                if (_controlsRemaining <= 0) HideControls();
            }
        }

        private void ShowDuration()
        {
            if (Duration() <= TimeSpan.Zero) return;
            _updatingSeek = true;
            SeekSlider.Maximum = Duration().TotalSeconds;
            _updatingSeek = false;
            DurationText.Text = Ticks.FormatSpan(Duration());
        }

        private void ShowControls()
        {
            OverlayRoot.Visibility = Visibility.Visible;
            _controlsRemaining = ControlsSeconds;
        }

        private void HideControls()
        {
            OverlayRoot.Visibility = Visibility.Collapsed;
            TracksPanel.Visibility = Visibility.Collapsed;
            _controlsRemaining = 0;
        }

        private void SetBuffering(bool active)
        {
            BufferingRing.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            BufferingText.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (active) ShowControls();
        }

        // Every icon button shows its state twice: which of the two Paths is visible,
        // and the accessible name a screen reader announces. The name describes the
        // action the tap will perform next.
        private void ShowPlayingState()
        {
            bool playing = MediaEl.CurrentState == MediaElementState.Playing;
            PlayIcon.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
            PauseIcon.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
            SetName(PlayPauseButton, Localized.Text(playing ? "PauseAction" : "PlayAction"));
        }

        private void ShowMuteState()
        {
            MuteSlash.Visibility = MediaEl.IsMuted ? Visibility.Visible : Visibility.Collapsed;
            SetName(MuteButton, Localized.Text(MediaEl.IsMuted ? "ActionUnmute" : "ActionMute"));
        }

        private void ShowStretchState()
        {
            bool fill = MediaEl.Stretch == Stretch.UniformToFill;
            IconFit.Visibility = fill ? Visibility.Collapsed : Visibility.Visible;
            IconFill.Visibility = fill ? Visibility.Visible : Visibility.Collapsed;
            SetName(FitButton, Localized.Text(fill ? "ActionStretchFit" : "ActionStretchFill"));
        }

        private void ShowFullScreenState()
        {
            IconEnterFullScreen.Visibility = _surface.IsFullScreen ? Visibility.Collapsed : Visibility.Visible;
            IconExitFullScreen.Visibility = _surface.IsFullScreen ? Visibility.Visible : Visibility.Collapsed;
            SetName(FullScreenButton, Localized.Text(_surface.IsFullScreen ? "ActionExitFullScreen" : "ActionFullScreen"));
            SetName(TracksButton, Localized.Text("ActionTracks"));
        }

        private void NameActions()
        {
            SetName(CloseButton, Localized.Text("ActionClose"));
            SetName(SkipBackButton, Localized.Text("ActionSkipBack"));
            SetName(SkipForwardButton, Localized.Text("ActionSkipForward"));
            SetName(SeekSlider, Localized.Text("ActionSeek"));
            ShowPlayingState();
            ShowMuteState();
            ShowStretchState();
            ShowFullScreenState();
        }

        private void SetName(FrameworkElement element, string name)
        {
            System.Windows.Automation.AutomationProperties.SetName(element, name);
        }

        private PlaybackProgressDto BuildProgress()
        {
            return new PlaybackProgressDto
            {
                ItemId = _item.Id,
                MediaSourceId = _plan != null ? _plan.MediaSourceId : null,
                PlaySessionId = _plan != null ? _plan.PlaySessionId : null,
                PositionTicks = Ticks.FromTimeSpan(MediaEl.Position),
                CanSeek = true,
                IsPaused = MediaEl.CurrentState == MediaElementState.Paused,
                PlayMethod = _plan != null && _plan.IsTranscode ? "Transcode" : "DirectPlay"
            };
        }

        // An offline file has no session on the server: it was never started there,
        // so reporting a position would only invent one.
        private async Task ReportStartAsync()
        {
            if (_download != null) return;
            try
            {
                await _client.ReportPlaybackStartAsync(BuildProgress());
            }
            catch
            {
            }
        }

        private async Task ReportProgressAsync()
        {
            if (_download != null) return;
            try
            {
                await _client.ReportPlaybackProgressAsync(BuildProgress());
            }
            catch
            {
            }
        }

        private async Task SafeReportStoppedAsync()
        {
            if (_download != null) return;
            try
            {
                await _client.ReportPlaybackStoppedAsync(BuildProgress());
            }
            catch
            {
            }
        }

        // However the user leaves - the close button, the hardware back button, a tap
        // on a notification - the status bar, the rotation lock and the screen timeout
        // go back to normal, so the next screen looks the way it always does.
        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_uiTimer != null) _uiTimer.Stop();
            Services.NavigationService.BackInterceptor = null;
            _surface.RestoreAll();
            SaveOfflinePosition(Ticks.FromTimeSpan(MediaEl.Position));
            if (MediaEl.CurrentState != MediaElementState.Stopped)
            {
                await SafeReportStoppedAsync();
                MediaEl.Stop();
            }
            CloseLocalStream();
        }

        private void SaveOfflinePosition(long ticks)
        {
            if (_download == null) return;
            try
            {
                DownloadStore.Current.UpdatePosition(_download.ItemId, ticks);
                _download.PositionTicks = ticks;
            }
            catch
            {
                // The position is a courtesy; a read-only store must not break leaving.
            }
        }

        private void CloseLocalStream()
        {
            if (_localStream == null) return;
            try
            {
                _localStream.Dispose();
            }
            catch
            {
            }
            _localStream = null;
        }
    }
}
