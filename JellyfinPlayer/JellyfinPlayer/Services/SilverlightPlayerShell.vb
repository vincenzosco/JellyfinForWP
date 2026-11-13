Imports JellyfinPlayer.Shared
Imports Microsoft.Phone.Controls
Imports Microsoft.Phone.Shell

Namespace Services
    ' The Windows Phone Silverlight 8.1 binding of IPlayerShell. Silverlight-only,
    ' so it is verified by compilation and by the manual checklist in agents/TESTING.md.
    '
    ' There is no platform fullscreen media mode on WP8.1 Silverlight: "fullscreen"
    ' means the System Tray is hidden, the page allows landscape, and the video is
    ' sized to the whole page surface.
    Public Class SilverlightPlayerShell
        Implements IPlayerShell

        Private ReadOnly _page As PhoneApplicationPage
        Private _portraitOrientation As SupportedPageOrientation

        Public Sub New(page As PhoneApplicationPage)
            _page = page
            _portraitOrientation = page.SupportedOrientations
        End Sub

        Public Sub HideStatusBar() Implements IPlayerShell.HideStatusBar
            SystemTray.IsVisible = False
        End Sub

        Public Sub ShowStatusBar() Implements IPlayerShell.ShowStatusBar
            SystemTray.IsVisible = True
        End Sub

        ' Best effort, on purpose: the tray is cosmetic, so a device that refuses
        ' to hide it must still play the video edge to edge rather than crash.
        Public Sub AllowRotation() Implements IPlayerShell.AllowRotation
            _page.SupportedOrientations = SupportedPageOrientation.PortraitOrLandscape
        End Sub

        Public Sub RestorePortrait() Implements IPlayerShell.RestorePortrait
            _page.SupportedOrientations = _portraitOrientation
        End Sub

        ' Keeps the screen lit while the video plays, and lets it time out again
        ' when it stops.
        Public Sub RequestKeepAwake() Implements IPlayerShell.RequestKeepAwake
            PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled
        End Sub

        Public Sub ReleaseKeepAwake() Implements IPlayerShell.ReleaseKeepAwake
            PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Enabled
        End Sub
    End Class
End Namespace
