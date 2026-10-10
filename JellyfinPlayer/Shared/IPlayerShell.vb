Namespace Global.JellyfinPlayer.Shared
    ' What the playback screen needs from the system chrome (status bar, rotation
    ' lock, screen timeout). WinRT-free on purpose, so the desktop tests can drive
    ' the decisions; Services.WinRtPlayerShell is the Windows Phone 8.1 binding.
    Public Interface IPlayerShell
        Sub HideStatusBar()
        Sub ShowStatusBar()
        Sub AllowRotation()
        Sub RestorePortrait()
        Sub RequestKeepAwake()
        Sub ReleaseKeepAwake()
    End Interface
End Namespace
