Imports JellyfinPlayer.Shared
Imports Windows.Graphics.Display
Imports Windows.System.Display
Imports Windows.UI.ViewManagement

Namespace Services
    ' The Windows Phone 8.1 binding of IPlayerShell. WinRT-only, so it is verified
    ' by compilation and by the manual checklist in agents/TESTING.md.
    '
    ' There is no ApplicationView.TryEnterFullScreenMode on WP8.1: "fullscreen"
    ' means the status bar is hidden and the page draws its own video edge to edge.
    Public Class WinRtPlayerShell
        Implements IPlayerShell

        ' Every upright and landscape orientation: the player follows the phone in
        ' any direction it is turned. Portrait alone is restored on the way out.
        Private Const AllowedOrientations As DisplayOrientations = DisplayOrientations.Portrait Or
                                                                  DisplayOrientations.PortraitFlipped Or
                                                                  DisplayOrientations.Landscape Or
                                                                  DisplayOrientations.LandscapeFlipped

        Private _keepAwake As DisplayRequest

        Public Sub HideStatusBar() Implements IPlayerShell.HideStatusBar
            Dim bar = StatusBar.GetForCurrentView()
            If bar IsNot Nothing Then SetBarVisibleAsync(bar, False)
        End Sub

        Public Sub ShowStatusBar() Implements IPlayerShell.ShowStatusBar
            Dim bar = StatusBar.GetForCurrentView()
            If bar IsNot Nothing Then SetBarVisibleAsync(bar, True)
        End Sub

        ' Best effort, on purpose: the status bar is cosmetic, so a device that
        ' refuses to hide it must still play the video edge to edge rather than
        ' take the app down.
        Private Async Sub SetBarVisibleAsync(bar As StatusBar, visible As Boolean)
            Try
                If visible Then
                    Await bar.ShowAsync()
                Else
                    Await bar.HideAsync()
                End If
            Catch
            End Try
        End Sub

        Public Sub AllowRotation() Implements IPlayerShell.AllowRotation
            DisplayInformation.AutoRotationPreferences = AllowedOrientations
        End Sub

        Public Sub RestorePortrait() Implements IPlayerShell.RestorePortrait
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait
        End Sub

        Public Sub RequestKeepAwake() Implements IPlayerShell.RequestKeepAwake
            If _keepAwake Is Nothing Then _keepAwake = New DisplayRequest()
            _keepAwake.RequestActive()
        End Sub

        Public Sub ReleaseKeepAwake() Implements IPlayerShell.ReleaseKeepAwake
            If _keepAwake Is Nothing Then Return
            _keepAwake.RequestRelease()
            _keepAwake = Nothing
        End Sub
    End Class
End Namespace
