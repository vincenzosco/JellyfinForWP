Namespace Global.JellyfinPlayer.Shared
    ' Owns the system chrome the player borrows: the status bar, the rotation lock
    ' and the screen timeout. Every call is idempotent, so the page can call
    ' EnterFullScreen/SetPlaying as often as it likes and RestoreAll exactly once
    ' on the way out - the next screen always starts from the normal state.
    Public Class PlayerSurfaceController
        Private ReadOnly _shell As IPlayerShell
        Private _fullScreen As Boolean
        Private _awake As Boolean

        Public Sub New(shell As IPlayerShell)
            _shell = shell
        End Sub

        Public ReadOnly Property IsFullScreen As Boolean
            Get
                Return _fullScreen
            End Get
        End Property

        Public Sub EnterFullScreen()
            If _fullScreen Then Return
            _fullScreen = True
            _shell.HideStatusBar()
            _shell.AllowRotation()
        End Sub

        Public Sub ExitFullScreen()
            If Not _fullScreen Then Return
            _fullScreen = False
            _shell.ShowStatusBar()
            _shell.RestorePortrait()
        End Sub

        ' Keeps the screen lit while the video plays, and lets it time out again
        ' when it stops. Pausing must not hold the screen on any longer.
        Public Sub SetPlaying(playing As Boolean)
            If playing = _awake Then Return
            _awake = playing
            If playing Then
                _shell.RequestKeepAwake()
            Else
                _shell.ReleaseKeepAwake()
            End If
        End Sub

        Public Sub RestoreAll()
            ExitFullScreen()
            SetPlaying(False)
        End Sub
    End Class
End Namespace
