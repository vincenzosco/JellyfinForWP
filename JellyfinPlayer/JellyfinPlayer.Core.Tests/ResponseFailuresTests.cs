using System;
using System.Net.Http;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class ResponseFailuresTests
    {
        [TestMethod]
        public void Html_bodies_are_detected()
        {
            Assert.IsTrue(ResponseFailures.LooksLikeHtml("<!DOCTYPE html><html></html>"));
            Assert.IsTrue(ResponseFailures.LooksLikeHtml("  \r\n<html><body>nginx</body></html>"));
            Assert.IsTrue(ResponseFailures.LooksLikeHtml("<h1>404</h1>"));
            Assert.IsFalse(ResponseFailures.LooksLikeHtml(""));
            Assert.IsFalse(ResponseFailures.LooksLikeHtml("  "));
            Assert.IsFalse(ResponseFailures.LooksLikeHtml(null));
            Assert.IsFalse(ResponseFailures.LooksLikeHtml("{}"));
            Assert.IsFalse(ResponseFailures.LooksLikeHtml("[1,2]"));
            Assert.IsFalse(ResponseFailures.LooksLikeHtml("null"));
        }

        [TestMethod]
        public void DescribeHttpFailure_does_not_include_the_html_page()
        {
            string message = ResponseFailures.DescribeHttpFailure(500, "text/html; charset=utf-8",
                "<html><head><title>500 Internal Server Error</title></head><body>nginx/1.2</body></html>");
            Assert.AreEqual("The server answered HTTP 500. It returned a web page instead of the Jellyfin API - check the address and the port.", message);
            Assert.IsFalse(message.Contains("<"), message);
        }

        [TestMethod]
        public void DescribeHttpFailure_truncates_a_long_json_body()
        {
            string message = ResponseFailures.DescribeHttpFailure(400, "application/json", new string('x', 500));
            Assert.IsTrue(message.StartsWith("The server answered HTTP 400."), message);
            Assert.IsTrue(message.Length < 200, message);
            Assert.IsTrue(message.EndsWith("..."), message);
        }

        [TestMethod]
        public void DescribeHttpFailure_collapses_whitespace_in_the_detail()
        {
            string message = ResponseFailures.DescribeHttpFailure(503, "application/json", "{ \"Message\" : \r\n \"disk full\" }");
            Assert.AreEqual("The server answered HTTP 503. { \"Message\" : \"disk full\" }", message);
        }

        [TestMethod]
        public void Parse_throws_a_classified_error_for_html()
        {
            try
            {
                ResponseFailures.Parse<ServerInfoDto>("<!DOCTYPE html><html>hi</html>");
                Assert.Fail("expected a JellyfinException");
            }
            catch (JellyfinException ex)
            {
                Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message);
                Assert.IsFalse(ex.Message.Contains("<"), ex.Message);
                Assert.IsTrue(ex.Message.Length < 200, ex.Message);
            }
        }

        [TestMethod]
        public void Parse_throws_a_classified_error_for_broken_json()
        {
            try
            {
                ResponseFailures.Parse<ServerInfoDto>("{\"ServerName\":");
                Assert.Fail("expected a JellyfinException");
            }
            catch (JellyfinException ex)
            {
                Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message);
            }
        }

        [TestMethod]
        public void Parse_returns_the_object_for_json()
        {
            var info = ResponseFailures.Parse<ServerInfoDto>("{\"ServerName\":\"Home\",\"Id\":\"abc\"}");
            Assert.AreEqual("Home", info.ServerName);
            Assert.AreEqual("abc", info.Id);
        }

        [TestMethod]
        public void MapException_classifies_timeout_unreachable_and_unknown()
        {
            Assert.AreEqual(JellyfinErrorKind.Timeout, HttpTransport.MapException(new TaskCanceledException()).Kind);
            Assert.AreEqual(JellyfinErrorKind.Unreachable, HttpTransport.MapException(new HttpRequestException("dns")).Kind);
            Assert.AreEqual(JellyfinErrorKind.Unknown, HttpTransport.MapException(new InvalidOperationException("?")).Kind);
            Assert.AreEqual(JellyfinErrorKind.HttpFailure, HttpTransport.MapException(new JellyfinException("x", 404, JellyfinErrorKind.HttpFailure)).Kind,
                "an already classified exception is passed through");
        }
    }
}
