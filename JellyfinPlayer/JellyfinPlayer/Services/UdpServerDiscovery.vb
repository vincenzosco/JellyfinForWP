Imports System
Imports System.Collections.Generic
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared

Namespace Services
    ' Broadcasts the Jellyfin discovery probe on UDP 7359 and collects the replies
    ' using the Windows Phone SocketAsyncEventArgs pattern. Broadcast reception is
    ' verified only on a device/emulator (see agents/TESTING.md); a failure here is
    ' never fatal - the login page always keeps manual URL entry.
    Public Module UdpServerDiscovery
        Public Async Function DiscoverAsync(timeoutMs As Integer) As Task(Of List(Of DiscoveredServerDto))
            Dim found As New List(Of DiscoveredServerDto)()
            Dim socket As Socket = Nothing
            Try
                socket = New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                socket.Bind(New IPEndPoint(IPAddress.Any, 0))

                Dim broadcast As New IPEndPoint(IPAddress.Broadcast, ServerDiscoveryParser.DiscoveryPort)
                Dim probe = Encoding.UTF8.GetBytes(ServerDiscoveryParser.ProbePayload)

                ' Send the probe (no reply is expected to the send itself).
                Await SendAsync(socket, probe, broadcast).ConfigureAwait(False)

                ' Collect replies until the bounded wait expires.
                Dim deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs)
                Dim buffer(2047) As Byte
                Do While DateTime.UtcNow < deadline
                    Dim sender As EndPoint = New IPEndPoint(IPAddress.Any, 0)
                    Dim received = Await ReceiveOnceAsync(socket, buffer, sender,
                                                          CInt((deadline - DateTime.UtcNow).TotalMilliseconds)).ConfigureAwait(False)
                    If received <= 0 Then Continue Do

                    Try
                        Dim payload = Encoding.UTF8.GetString(buffer, 0, received)
                        Dim server As DiscoveredServerDto = Nothing
                        If ServerDiscoveryParser.TryParse(payload, server) Then found.Add(server)
                    Catch
                        ' Ignore malformed datagrams.
                    End Try
                Loop
            Catch
                ' No network, or broadcast is unavailable: report nothing found.
            Finally
                If socket IsNot Nothing Then
                    Try
                        socket.Close()
                    Catch
                    End Try
                End If
            End Try

            Return found
        End Function

        Private Function SendAsync(socket As Socket, payload As Byte(), target As IPEndPoint) As Task
            Dim done As New TaskCompletionSource(Of Boolean)()
            Dim args As New SocketAsyncEventArgs()
            args.RemoteEndPoint = target
            args.SetBuffer(payload, 0, payload.Length)
            AddHandler args.Completed, Sub(s, e)
                                           done.TrySetResult(e.SocketError = SocketError.Success)
                                       End Sub
            Try
                If Not socket.SendToAsync(args) Then
                    done.TrySetResult(args.SocketError = SocketError.Success)
                End If
            Catch ex As Exception
                done.TrySetException(ex)
            End Try
            Return done.Task
        End Function

        Private Function ReceiveOnceAsync(socket As Socket, buffer As Byte(), sender As EndPoint, waitMs As Integer) As Task(Of Integer)
            Dim done As New TaskCompletionSource(Of Integer)()
            If waitMs <= 0 Then
                done.TrySetResult(0)
                Return done.Task
            End If

            Dim args As New SocketAsyncEventArgs()
            args.RemoteEndPoint = sender
            args.SetBuffer(buffer, 0, buffer.Length)
            AddHandler args.Completed, Sub(s, e)
                                           If e.SocketError = SocketError.Success Then
                                               done.TrySetResult(e.BytesTransferred)
                                           Else
                                               done.TrySetResult(0)
                                           End If
                                       End Sub
            Try
                If Not socket.ReceiveFromAsync(args) Then
                    done.TrySetResult(If(args.SocketError = SocketError.Success, args.BytesTransferred, 0))
                End If
            Catch ex As Exception
                done.TrySetResult(0)
            End Try
            Return done.Task
        End Function
    End Module
End Namespace
