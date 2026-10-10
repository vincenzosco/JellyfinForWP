<TestClass>
Public Class PlayerSurfaceTests
    ' The helper is not called Surface: VB is case-insensitive, so a method named
    ' Surface and a local named surface are the same name to the compiler.
    Private Function NewSurface(fake As FakePlayerShell) As PlayerSurfaceController
        Return New PlayerSurfaceController(fake)
    End Function

    <TestMethod>
    Public Sub Enter_fullscreen_hides_the_status_bar_and_allows_rotation()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        Assert.IsTrue(surface.IsFullScreen)
        CollectionAssert.AreEqual({"HideStatusBar", "AllowRotation"}, fake.Calls.ToArray())
    End Sub

    <TestMethod>
    Public Sub Entering_twice_does_not_repeat_the_calls()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.EnterFullScreen()
        Assert.AreEqual(1, fake.CountOf("HideStatusBar"))
        Assert.AreEqual(1, fake.CountOf("AllowRotation"))
    End Sub

    <TestMethod>
    Public Sub Exit_fullscreen_restores_the_status_bar_and_portrait()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        fake.Calls.Clear()
        surface.ExitFullScreen()
        Assert.IsFalse(surface.IsFullScreen)
        CollectionAssert.AreEqual({"ShowStatusBar", "RestorePortrait"}, fake.Calls.ToArray())
    End Sub

    <TestMethod>
    Public Sub Exit_without_fullscreen_does_nothing()
        Dim fake As New FakePlayerShell()
        NewSurface(fake).ExitFullScreen()
        Assert.AreEqual(0, fake.Calls.Count)
    End Sub

    <TestMethod>
    Public Sub Playing_keeps_the_screen_awake_and_pausing_releases_it()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.SetPlaying(True)
        Assert.AreEqual(1, fake.CountOf("RequestKeepAwake"))
        surface.SetPlaying(True)
        Assert.AreEqual(1, fake.CountOf("RequestKeepAwake"), "idempotent while already playing")
        surface.SetPlaying(False)
        Assert.AreEqual(1, fake.CountOf("ReleaseKeepAwake"))
        surface.SetPlaying(False)
        Assert.AreEqual(1, fake.CountOf("ReleaseKeepAwake"), "idempotent while already stopped")
    End Sub

    ' Review Focus 1: however the user left the screen, the next one must look normal.
    <TestMethod>
    Public Sub RestoreAll_undoes_a_fullscreen_session_left_open()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.SetPlaying(True)
        fake.Calls.Clear()
        surface.RestoreAll()
        CollectionAssert.AreEqual({"ShowStatusBar", "RestorePortrait", "ReleaseKeepAwake"}, fake.Calls.ToArray())
        Assert.IsFalse(surface.IsFullScreen)
    End Sub

    <TestMethod>
    Public Sub RestoreAll_is_idempotent()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.RestoreAll()
        fake.Calls.Clear()
        surface.RestoreAll()
        Assert.AreEqual(0, fake.Calls.Count)
    End Sub

    <TestMethod>
    Public Sub RestoreAll_on_a_fresh_controller_does_nothing()
        Dim fake As New FakePlayerShell()
        NewSurface(fake).RestoreAll()
        Assert.AreEqual(0, fake.Calls.Count)
    End Sub

    <TestMethod>
    Public Sub Keeping_the_screen_awake_survives_leaving_fullscreen()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.SetPlaying(True)
        surface.ExitFullScreen()
        Assert.AreEqual(0, fake.CountOf("ReleaseKeepAwake"), "still playing, so still awake")
        Assert.IsFalse(surface.IsFullScreen)
    End Sub
End Class
