using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class PlayerSurfaceTests
    {
        private PlayerSurfaceController NewSurface(FakePlayerShell fake)
        {
            return new PlayerSurfaceController(fake);
        }

        [TestMethod]
        public void Enter_fullscreen_hides_the_status_bar_and_allows_rotation()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.EnterFullScreen();
            Assert.IsTrue(surface.IsFullScreen);
            CollectionAssert.AreEqual(new[] { "HideStatusBar", "AllowRotation" }, fake.Calls.ToArray());
        }

        [TestMethod]
        public void Entering_twice_does_not_repeat_the_calls()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.EnterFullScreen();
            surface.EnterFullScreen();
            Assert.AreEqual(1, fake.CountOf("HideStatusBar"));
            Assert.AreEqual(1, fake.CountOf("AllowRotation"));
        }

        [TestMethod]
        public void Exit_fullscreen_restores_the_status_bar_and_portrait()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.EnterFullScreen();
            fake.Calls.Clear();
            surface.ExitFullScreen();
            Assert.IsFalse(surface.IsFullScreen);
            CollectionAssert.AreEqual(new[] { "ShowStatusBar", "RestorePortrait" }, fake.Calls.ToArray());
        }

        [TestMethod]
        public void Exit_without_fullscreen_does_nothing()
        {
            var fake = new FakePlayerShell();
            NewSurface(fake).ExitFullScreen();
            Assert.AreEqual(0, fake.Calls.Count);
        }

        [TestMethod]
        public void Playing_keeps_the_screen_awake_and_pausing_releases_it()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.SetPlaying(true);
            Assert.AreEqual(1, fake.CountOf("RequestKeepAwake"));
            surface.SetPlaying(true);
            Assert.AreEqual(1, fake.CountOf("RequestKeepAwake"), "idempotent while already playing");
            surface.SetPlaying(false);
            Assert.AreEqual(1, fake.CountOf("ReleaseKeepAwake"));
            surface.SetPlaying(false);
            Assert.AreEqual(1, fake.CountOf("ReleaseKeepAwake"), "idempotent while already stopped");
        }

        // Review Focus 1: however the user left the screen, the next one must look normal.
        [TestMethod]
        public void RestoreAll_undoes_a_fullscreen_session_left_open()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.EnterFullScreen();
            surface.SetPlaying(true);
            fake.Calls.Clear();
            surface.RestoreAll();
            CollectionAssert.AreEqual(new[] { "ShowStatusBar", "RestorePortrait", "ReleaseKeepAwake" }, fake.Calls.ToArray());
            Assert.IsFalse(surface.IsFullScreen);
        }

        [TestMethod]
        public void RestoreAll_is_idempotent()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.EnterFullScreen();
            surface.RestoreAll();
            fake.Calls.Clear();
            surface.RestoreAll();
            Assert.AreEqual(0, fake.Calls.Count);
        }

        [TestMethod]
        public void RestoreAll_on_a_fresh_controller_does_nothing()
        {
            var fake = new FakePlayerShell();
            NewSurface(fake).RestoreAll();
            Assert.AreEqual(0, fake.Calls.Count);
        }

        [TestMethod]
        public void Keeping_the_screen_awake_survives_leaving_fullscreen()
        {
            var fake = new FakePlayerShell();
            var surface = NewSurface(fake);
            surface.EnterFullScreen();
            surface.SetPlaying(true);
            surface.ExitFullScreen();
            Assert.AreEqual(0, fake.CountOf("ReleaseKeepAwake"), "still playing, so still awake");
            Assert.IsFalse(surface.IsFullScreen);
        }
    }
}
