using System;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class FailureTextTests
    {
        // Stands in for the resource lookup the app passes in, so the mapping is
        // testable on the desktop. Same English strings as Resources.resx.
        private static string Lookup(string key)
        {
            switch (key)
            {
                case "ErrorHttp": return "The server answered HTTP {0}.";
                case "ErrorNotJson": return "The address answered with a web page instead of the Jellyfin API.";
                case "ErrorTimeout": return "The server did not respond in time.";
                case "ErrorUnreachable": return "Could not reach the server.";
                case "ErrorGeneric": return "Unexpected error: {0}";
                default: throw new InvalidOperationException("unexpected key " + key);
            }
        }

        [TestMethod]
        public void Classified_failures_use_the_matching_sentence()
        {
            Assert.AreEqual("The address answered with a web page instead of the Jellyfin API.",
                FailureText.Describe(new JellyfinException("ignored", 0, JellyfinErrorKind.NonJsonResponse), Lookup));
            Assert.AreEqual("The server answered HTTP 404.",
                FailureText.Describe(new JellyfinException("ignored", 404, JellyfinErrorKind.HttpFailure), Lookup));
            Assert.AreEqual("The server did not respond in time.",
                FailureText.Describe(new JellyfinException("ignored", 0, JellyfinErrorKind.Timeout), Lookup));
            Assert.AreEqual("Could not reach the server.",
                FailureText.Describe(new JellyfinException("ignored", 0, JellyfinErrorKind.Unreachable), Lookup));
        }

        // PlaybackService and the "play direct" fallback throw sentences that are
        // already localized and already user-facing. Wrapping them as "unexpected
        // error: <sentence>" shows the user a doubled message.
        [TestMethod]
        public void An_unclassified_failure_keeps_its_own_message()
        {
            Assert.AreEqual("Nessuno stream diretto disponibile.",
                FailureText.Describe(new JellyfinException("Nessuno stream diretto disponibile.", 0), Lookup));
            Assert.AreEqual("That address is not a Jellyfin server.",
                FailureText.Describe(new JellyfinException("That address is not a Jellyfin server.", 0), Lookup));
        }

        [TestMethod]
        public void An_unexpected_exception_is_reported_as_unexpected()
        {
            Assert.AreEqual("Unexpected error: boom",
                FailureText.Describe(new InvalidOperationException("boom"), Lookup));
        }
    }
}
