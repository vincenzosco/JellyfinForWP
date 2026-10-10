Imports System.Collections.Generic
Imports System.Linq
Imports JellyfinPlayer.Shared

' Records every system-chrome request the controller makes, so the fullscreen,
' rotation and keep-awake decisions can be asserted on the desktop.
Public Class FakePlayerShell
    Implements IPlayerShell

    Public ReadOnly Calls As New List(Of String)()

    Public Sub HideStatusBar() Implements IPlayerShell.HideStatusBar
        Calls.Add("HideStatusBar")
    End Sub

    Public Sub ShowStatusBar() Implements IPlayerShell.ShowStatusBar
        Calls.Add("ShowStatusBar")
    End Sub

    Public Sub AllowRotation() Implements IPlayerShell.AllowRotation
        Calls.Add("AllowRotation")
    End Sub

    Public Sub RestorePortrait() Implements IPlayerShell.RestorePortrait
        Calls.Add("RestorePortrait")
    End Sub

    Public Sub RequestKeepAwake() Implements IPlayerShell.RequestKeepAwake
        Calls.Add("RequestKeepAwake")
    End Sub

    Public Sub ReleaseKeepAwake() Implements IPlayerShell.ReleaseKeepAwake
        Calls.Add("ReleaseKeepAwake")
    End Sub

    Public Function CountOf(name As String) As Integer
        Return Calls.Where(Function(c) c = name).Count()
    End Function
End Class
