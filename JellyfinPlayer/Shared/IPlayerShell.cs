namespace JellyfinPlayer.Shared
{
    // What the playback screen needs from the system chrome (status bar, rotation
    // lock, screen timeout). Platform-free on purpose, so the desktop tests can
    // drive the decisions; SilverlightPlayerShell is the phone binding.
    public interface IPlayerShell
    {
        void HideStatusBar();
        void ShowStatusBar();
        void AllowRotation();
        void RestorePortrait();
        void RequestKeepAwake();
        void ReleaseKeepAwake();
    }
}
