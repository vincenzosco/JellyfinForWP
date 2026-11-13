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

                var broadcast = new IPEndPoint(IPAddress.Broadcast, ServerDiscoveryParser.DiscoveryPort);
                byte[] probe = Encoding.UTF8.GetBytes(ServerDiscoveryParser.ProbePayload);

                // Send the probe (no reply is expected to the send itself).
                await SendAsync(socket, probe, broadcast).ConfigureAwait(false);

                // Collect replies until the bounded wait expires.
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                var buffer = new byte[2048];
                while (DateTime.UtcNow < deadline)
                {
                    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    int received = await ReceiveOnceAsync(socket, buffer, sender,
                                                          (int)(deadline - DateTime.UtcNow).TotalMilliseconds).ConfigureAwait(false);
                    if (received <= 0) continue;

                    try
                    {
                        string payload = Encoding.UTF8.GetString(buffer, 0, received);
                        DiscoveredServerDto server;
                        if (ServerDiscoveryParser.TryParse(payload, out server)) found.Add(server);
                    }
                    catch
                    {
                        // Ignore malformed datagrams.
                    }
                }
            }
            catch
            {
                // No network, or broadcast is unavailable: report nothing found.
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
