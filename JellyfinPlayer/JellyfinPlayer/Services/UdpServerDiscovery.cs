using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // Broadcasts the Jellyfin discovery probe on UDP 7359 and collects the replies
    // using the Windows Phone SocketAsyncEventArgs pattern. Broadcast reception is
    // verified only on a device/emulator (see agents/TESTING.md); a failure here is
    // never fatal - the login page always keeps manual URL entry.
    public static class UdpServerDiscovery
    {
        public static async Task<List<DiscoveredServerDto>> DiscoverAsync(int timeoutMs)
        {
            var found = new List<DiscoveredServerDto>();
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                // Silverlight's Socket has no SetSocketOption; a datagram socket here
                // may send to 255.255.255.255 as it is. A refusal is logged below
                // rather than silently reported as "no server".

                var broadcast = new IPEndPoint(IPAddress.Broadcast, ServerDiscoveryParser.DiscoveryPort);
                byte[] probe = Encoding.UTF8.GetBytes(ServerDiscoveryParser.ProbePayload);

                // Send the probe (no reply is expected to the send itself).
                AppLog.Info("Discovery: probe sent to " + broadcast.Address + ":" + broadcast.Port);
                await SendAsync(socket, probe, broadcast).ConfigureAwait(false);

                // Collect replies until the bounded wait expires.
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                var buffer = new byte[2048];
                while (DateTime.UtcNow < deadline)
                {
                    int waitMs = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                    if (waitMs <= 0) break;
                    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    // The socket never completes its receive until a datagram arrives,
                    // so the wait is bounded here: a server that does not answer must
                    // not leave the login page "searching" forever.
                    var receive = ReceiveOnceAsync(socket, buffer, sender, waitMs);
                    var finished = await Task.WhenAny(receive, Task.Delay(waitMs)).ConfigureAwait(false);
                    if (finished != receive)
                    {
                        AppLog.Info("Discovery: no reply within " + timeoutMs + " ms");
                        break;
                    }
                    int received = receive.Result;
                    if (received <= 0) continue;

                    try
                    {
                        string payload = Encoding.UTF8.GetString(buffer, 0, received);
                        DiscoveredServerDto server;
                        if (ServerDiscoveryParser.TryParse(payload, out server))
                        {
                            AppLog.Info("Discovery: reply " + server.Address + " (" + server.Name + ")");
                            found.Add(server);
                        }
                        else
                        {
                            AppLog.Info("Discovery: unparsable reply from " + sender);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignore malformed datagrams, but say so in the log.
                        AppLog.Error("Discovery: datagram", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                // No network, or broadcast is unavailable: report nothing found.
                AppLog.Error("Discovery: socket", ex);
            }
            finally
            {
                if (socket != null)
                {
                    try
                    {
                        socket.Close();
                    }
                    catch
                    {
                    }
                }
            }

            return found;
        }

        private static Task SendAsync(Socket socket, byte[] payload, IPEndPoint target)
        {
            var done = new TaskCompletionSource<bool>();
            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = target;
            args.SetBuffer(payload, 0, payload.Length);
            args.Completed += (s, e) => done.TrySetResult(e.SocketError == SocketError.Success);
            try
            {
                if (!socket.SendToAsync(args))
                {
                    done.TrySetResult(args.SocketError == SocketError.Success);
                }
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
            return done.Task;
        }

        private static Task<int> ReceiveOnceAsync(Socket socket, byte[] buffer, EndPoint sender, int waitMs)
        {
            var done = new TaskCompletionSource<int>();
            if (waitMs <= 0)
            {
                done.TrySetResult(0);
                return done.Task;
            }

            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = sender;
            args.SetBuffer(buffer, 0, buffer.Length);
            args.Completed += (s, e) =>
            {
                if (e.SocketError == SocketError.Success)
                {
                    done.TrySetResult(e.BytesTransferred);
                }
                else
                {
                    done.TrySetResult(0);
                }
            };
            try
            {
                if (!socket.ReceiveFromAsync(args))
                {
                    done.TrySetResult(args.SocketError == SocketError.Success ? args.BytesTransferred : 0);
                }
            }
            catch
            {
                done.TrySetResult(0);
            }
            return done.Task;
        }
    }
}
