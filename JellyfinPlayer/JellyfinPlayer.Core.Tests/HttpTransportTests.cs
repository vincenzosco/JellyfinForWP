using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class HttpTransportTests
    {
        [TestMethod]
        public void Get_sends_the_authorization_header_and_returns_the_response_body()
        {
            using (var server = new RecordingHttpServer("200 OK", "application/json", "{\"ServerName\":\"Jellyfin\"}"))
            {
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", JellyfinAuthHeader.WithToken("token-1", "device-1") }
                };
                string body = new HttpTransport().GetStringAsync(server.Url, headers).GetAwaiter().GetResult();

                Assert.AreEqual("{\"ServerName\":\"Jellyfin\"}", body);
                Assert.IsTrue(server.RequestText.Contains("Authorization: MediaBrowser Token=\"token-1\""), server.RequestText);
            }
        }

        [TestMethod]
        public void Post_sets_json_content_type_and_preserves_the_request_body()
        {
            using (var server = new RecordingHttpServer("200 OK", "application/json", "{}"))
            {
                var headers = new Dictionary<string, string> { { "Authorization", JellyfinAuthHeader.Identify("device-2") } };
                string body = new HttpTransport().PostJsonAsync(server.Url, "{\"Username\":\"alice\"}", headers).GetAwaiter().GetResult();

                Assert.AreEqual("{}", body);
                Assert.IsTrue(server.RequestText.Contains("Content-Type: application/json; charset=utf-8"), server.RequestText);
                Assert.IsTrue(server.RequestText.EndsWith("{\"Username\":\"alice\"}"), server.RequestText);
            }
        }

        [TestMethod]
        public void Http_error_keeps_status_and_classifies_the_body()
        {
            using (var server = new RecordingHttpServer("401 Unauthorized", "application/json", "{\"Message\":\"invalid token\"}"))
            {
                try
                {
                    var ignored = new HttpTransport().GetStringAsync(server.Url, null).GetAwaiter().GetResult();
                    Assert.Fail("expected a JellyfinException");
                }
                catch (JellyfinException ex)
                {
                    Assert.AreEqual(401, ex.StatusCode);
                    Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind);
                    StringAssert.Contains(ex.Message, "invalid token");
                }
            }
        }

        [TestMethod]
        public void Web_timeout_is_classified_as_timeout()
        {
            var failure = HttpTransport.MapException(new WebException("request timed out", WebExceptionStatus.Timeout));

            Assert.AreEqual(JellyfinErrorKind.Timeout, failure.Kind);
        }

        [TestMethod]
        public void Web_name_resolution_failure_is_classified_as_unreachable()
        {
            var failure = HttpTransport.MapException(new WebException("host not found", WebExceptionStatus.NameResolutionFailure));

            Assert.AreEqual(JellyfinErrorKind.Unreachable, failure.Kind);
        }
    }

    internal class RecordingHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _body;
        private readonly string _status;
        private readonly string _contentType;
        private readonly Task<string> _task;
        private readonly string _url;

        public RecordingHttpServer(string status, string contentType, string body)
        {
            _status = status;
            _contentType = contentType;
            _body = body;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            _url = "http://127.0.0.1:" + endpoint.Port.ToString() + "/";
            _task = Task.Run(new Func<string>(Receive));
        }

        public string Url
        {
            get { return _url; }
        }

        public string RequestText
        {
            get { return _task.GetAwaiter().GetResult(); }
        }

        private string Receive()
        {
            using (var client = _listener.AcceptTcpClient())
            {
                using (var stream = client.GetStream())
                {
                    var request = new StringBuilder();
                    var buffer = new byte[1024];
                    int contentLength = 0;
                    bool headerComplete = false;
                    int bodyBytes = 0;
                    do
                    {
                        int count = stream.Read(buffer, 0, buffer.Length);
                        if (count <= 0) break;
                        request.Append(Encoding.UTF8.GetString(buffer, 0, count));
                        string text = request.ToString();
                        int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (split >= 0 && !headerComplete)
                        {
                            headerComplete = true;
                            foreach (var line in text.Substring(0, split).Split(new[] { "\r\n" }, StringSplitOptions.None))
                            {
                                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                {
                                    int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
                                }
                            }
                            bodyBytes = Encoding.UTF8.GetByteCount(text.Substring(split + 4));
                        }
                        else if (headerComplete)
                        {
                            bodyBytes += count;
                        }
                        if (headerComplete && bodyBytes >= contentLength) break;
                    } while (true);

                    byte[] payload = Encoding.UTF8.GetBytes(_body);
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 " + _status + "\r\n" +
                        "Content-Type: " + _contentType + "\r\n" +
                        "Content-Length: " + payload.Length.ToString() + "\r\n" +
                        "Connection: close\r\n\r\n");
                    stream.Write(response, 0, response.Length);
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush();
                    return request.ToString();
                }
            }
        }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}
