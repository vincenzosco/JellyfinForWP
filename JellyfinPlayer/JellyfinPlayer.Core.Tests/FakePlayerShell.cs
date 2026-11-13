using System.Collections.Generic;
using System.Linq;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Core.Tests
{
    // Records every system-chrome request the controller makes, so the fullscreen,
    // rotation and keep-awake decisions can be asserted on the desktop.
    public class FakePlayerShell : IPlayerShell
    {
        public readonly List<string> Calls = new List<string>();

        public void HideStatusBar()
        {
            Calls.Add("HideStatusBar");
        }

        public void ShowStatusBar()
        {
            Calls.Add("ShowStatusBar");
        }

        public void AllowRotation()
        {
            Calls.Add("AllowRotation");
        }

        public void RestorePortrait()
        {
            Calls.Add("RestorePortrait");
        }

        public void RequestKeepAwake()
        {
            Calls.Add("RequestKeepAwake");
        }

        public void ReleaseKeepAwake()
        {
            Calls.Add("ReleaseKeepAwake");
        }

        public int CountOf(string name)
        {
            return Calls.Where(c => c == name).Count();
        }
    }
}
