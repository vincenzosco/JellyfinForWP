using JellyfinPlayer.Shared;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace JellyfinPlayer.Services
{
    // The Windows Phone Silverlight 8.1 binding of IPlayerShell. Silverlight-only,
    // so it is verified by compilation and by the manual checklist in agents/TESTING.md.
    //
    // There is no platform fullscreen media mode on WP8.1 Silverlight: "fullscreen"
    // means the System Tray is hidden, the page allows landscape, and the video is
    // sized to the whole page surface.
    public class SilverlightPlayerShell : IPlayerShell
    {
        private readonly PhoneApplicationPage _page;
        private readonly SupportedPageOrientation _portraitOrientation;

        public SilverlightPlayerShell(PhoneApplicationPage page)
        {
            _page = page;
            _portraitOrientation = page.SupportedOrientations;
        }

        public void HideStatusBar()
        {
            SystemTray.IsVisible = false;
        }

        public void ShowStatusBar()
        {
            SystemTray.IsVisible = true;
        }

        // Best effort, on purpose: the tray is cosmetic, so a device that refuses
        // to hide it must still play the video edge to edge rather than crash.
        public void AllowRotation()
        {
            _page.SupportedOrientations = SupportedPageOrientation.PortraitOrLandscape;
        }

        public void RestorePortrait()
        {
            _page.SupportedOrientations = _portraitOrientation;
        }

        // Keeps the screen lit while the video plays, and lets it time out again
        // when it stops.
        public void RequestKeepAwake()
        {
            PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
        }

        public void ReleaseKeepAwake()
        {
            PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Enabled;
        }
    }
}
