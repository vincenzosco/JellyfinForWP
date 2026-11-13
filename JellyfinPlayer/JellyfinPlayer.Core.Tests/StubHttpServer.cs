using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JellyfinPlayer.Core.Tests
{
    // Answers every HTTP request with one canned response. Test-only: it lets the real
    // HttpTransport run against a socket that speaks HTML instead of Jellyfin JSON,
    // without needing admin rights (HttpListener would) or touching the LAN.
    public class StubHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly byte[] _response;
        private readonly int _port;

        public StubHttpServer(string statusLine, string contentType, string body)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 " + statusLine + "\r\n" +
                            "Content-Type: " + contentType + "\r\n" +
                            "Content-Length: " + payload.Length.ToString() + "\r\n" +
                            "Connection: close\r\n\r\n";
            _response = Join(Encoding.UTF8.GetBytes(header), payload);
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Task.Run(new Action(Serve));
        }

        public int Port
        {
            get { return _port; }
        }

        public string BaseUrl
        {
            get { return "http://127.0.0.1:" + _port.ToString(); }
        }

        private byte[] Join(byte[] first, byte[] second)
        {
            var all = new byte[first.Length + second.Length];
            Array.Copy(first, 0, all, 0, first.Length);
            Array.Copy(second, 0, all, first.Length, second.Length);
            return all;
        }

        private void Serve()
        {
            while (true)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                    using (var stream = client.GetStream())
                    {
                        // Read the request headers, answer once, then close.
                        var buffer = new byte[1024];
                        var text = new StringBuilder();
                        while (text.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                        {
                            int read = stream.Read(buffer, 0, buffer.Length);
                            if (read <= 0) break;
                            text.Append(Encoding.UTF8.GetString(buffer, 0, read));
                        }
                        stream.Write(_response, 0, _response.Length);
                        stream.Flush();
                    }
                }
                catch
                {
                    break;
                }
                finally
                {
                    if (client != null) client.Close();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
        }
    }
}
