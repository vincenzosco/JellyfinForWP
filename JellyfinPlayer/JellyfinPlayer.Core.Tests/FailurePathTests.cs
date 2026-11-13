using System;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class FailurePathTests
    {
        private JellyfinApiClient ClientFor(StubHttpServer server)
        {
            return new JellyfinApiClient(new HttpTransport(), new JellyfinUrls(server.BaseUrl), "test-device");
        }

        // The reported emulator run: a 200 whose body is an HTML page.
        [TestMethod]
        public void Html_page_on_a_successful_response_is_classified_as_not_json()
        {
            using (var server = new StubHttpServer("200 OK", "text/html; charset=utf-8", "<!DOCTYPE html><html><body>nginx</body></html>"))
            {
                try
                {
                    ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult();
                    Assert.Fail("expected a JellyfinException");
                }
                catch (JellyfinException ex)
                {
                    Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message);
                    Assert.IsFalse(ex.Message.Contains("<"), ex.Message);
                    Assert.IsTrue(ex.Message.Length < 200, ex.Message);
                }
            }
        }

        [TestMethod]
        public void Html_page_on_a_failed_response_is_classified_as_an_http_failure()
        {
            using (var server = new StubHttpServer("404 Not Found", "text/html", "<html><body>Not Found</body></html>"))
            {
                try
                {
                    ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult();
                    Assert.Fail("expected a JellyfinException");
                }
                catch (JellyfinException ex)
                {
                    Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind, ex.Message);
                    Assert.AreEqual(404, ex.StatusCode);
                    Assert.IsTrue(ex.Message.Contains("HTTP 404"), ex.Message);
                    Assert.IsFalse(ex.Message.Contains("<"), ex.Message);
                }
            }
        }

        [TestMethod]
        public void Json_error_detail_is_kept_short()
        {
            using (var server = new StubHttpServer("500 Internal Server Error", "application/json", "{\"Message\":\"disk full\"}"))
            {
                try
                {
                    ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult();
                    Assert.Fail("expected a JellyfinException");
                }
                catch (JellyfinException ex)
                {
                    Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind, ex.Message);
                    Assert.IsTrue(ex.Message.Contains("500"), ex.Message);
                    Assert.IsTrue(ex.Message.Contains("disk full"), ex.Message);
                }
            }
        }

        // A 200 that is JSON still works: the guard must not break the happy path.
        [TestMethod]
        public void Json_on_a_successful_response_is_parsed()
        {
            using (var server = new StubHttpServer("200 OK", "application/json", "{\"ServerName\":\"Home\",\"Id\":\"abc\"}"))
            {
                var info = ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult();
                Assert.AreEqual("Home", info.ServerName);
                Assert.AreEqual("abc", info.Id);
            }
        }

        [TestMethod]
        public void Closed_port_is_classified_as_unreachable()
        {
            try
            {
                var client = new JellyfinApiClient(new HttpTransport(), new JellyfinUrls("http://127.0.0.1:9"), "test-device");
                client.GetServerInfoAsync().GetAwaiter().GetResult();
                Assert.Fail("expected a JellyfinException");
            }
            catch (JellyfinException ex)
            {
                Assert.AreEqual(JellyfinErrorKind.Unreachable, ex.Kind, ex.Message);
            }
        }

        // A token the server no longer accepts must send the user back to the
        // password prompt of that one account, not print an HTTP sentence.
        [TestMethod]
        public void IsUnauthorized_is_true_for_401_and_403()
        {
            Assert.IsTrue(FailureText.IsUnauthorized(new JellyfinException("nope", 401)));
            Assert.IsTrue(FailureText.IsUnauthorized(new JellyfinException("nope", 403)));
            Assert.IsFalse(FailureText.IsUnauthorized(new JellyfinException("nope", 500)));
            Assert.IsFalse(FailureText.IsUnauthorized(new JellyfinException("timeout", 0, JellyfinErrorKind.Timeout)));
            Assert.IsFalse(FailureText.IsUnauthorized(new Exception("boom")));
        }
    }
}
