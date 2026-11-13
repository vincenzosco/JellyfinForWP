namespace JellyfinPlayer.Shared
{
    // Owns the system chrome the player borrows: the status bar, the rotation lock
    // and the screen timeout. Every call is idempotent, so the page can call
    // EnterFullScreen/SetPlaying as often as it likes and RestoreAll exactly once
    // on the way out - the next screen always starts from the normal state.
    public class PlayerSurfaceController
    {
        private readonly IPlayerShell _shell;
        private bool _fullScreen;
        private bool _awake;

        public PlayerSurfaceController(IPlayerShell shell)
        {
            _shell = shell;
        }

        public bool IsFullScreen
        {
            get { return _fullScreen; }
        }

        public void EnterFullScreen()
        {
            if (_fullScreen) return;
            _fullScreen = true;
            _shell.HideStatusBar();
            _shell.AllowRotation();
        }

        public void ExitFullScreen()
        {
            if (!_fullScreen) return;
            _fullScreen = false;
            _shell.ShowStatusBar();
            _shell.RestorePortrait();
        }

        // Keeps the screen lit while the video plays, and lets it time out again
        // when it stops. Pausing must not hold the screen on any longer.
        public void SetPlaying(bool playing)
        {
            if (playing == _awake) return;
            _awake = playing;
            if (playing)
            {
                _shell.RequestKeepAwake();
            }
            else
            {
                _shell.ReleaseKeepAwake();
            }
        }

        public void RestoreAll()
        {
            ExitFullScreen();
            SetPlaying(false);
        }
    }
}
