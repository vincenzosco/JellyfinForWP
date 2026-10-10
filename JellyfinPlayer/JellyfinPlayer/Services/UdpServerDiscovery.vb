Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared
Imports Windows.Networking
Imports Windows.Networking.Sockets
Imports Windows.Storage.Streams

Namespace Services
    ' Broadcasts the Jellyfin discovery probe on UDP 7359 and collects the replies.
    ' WinRT-only (Windows.Networking.Sockets), so it is verified by compilation and
    ' the manual check in agents/TESTING.md, not by MSTest.
    Public Module UdpServerDiscovery
        Public Async Function DiscoverAsync(timeoutMs As Integer) As Task(Of List(Of DiscoveredServerDto))
            Dim found As New List(Of DiscoveredServerDto)()
            Dim socket As DatagramSocket = Nothing
            Try
                socket = New DatagramSocket()
                AddHandler socket.MessageReceived, Sub(sender, args)
                                                       Try
                                                           Dim reader = args.GetDataReader()
                                                           Dim payload = reader.ReadString(reader.UnconsumedBufferLength)
                                                           reader.DetachStream()
                                                           Dim server As DiscoveredServerDto = Nothing
                                                           If ServerDiscoveryParser.TryParse(payload, server) Then found.Add(server)
                                                       Catch
                                                           ' Ignore malformed datagrams.
                                                       End Try
                                                   End Sub

                ' Bind to any local port before broadcasting so replies can arrive.
                Await socket.BindServiceNameAsync("")

                Dim stream = Await socket.GetOutputStreamAsync(New HostName("255.255.255.255"), ServerDiscoveryParser.DiscoveryPort.ToString())
                Dim writer As New DataWriter(stream)
                writer.WriteString(ServerDiscoveryParser.ProbePayload)
                Await writer.StoreAsync()

                Await Task.Delay(timeoutMs)
            Catch
                ' No network, or broadcast is unavailable: report nothing found.
            Finally
                If socket IsNot Nothing Then
                    Try
                        socket.Dispose()
                    Catch
                    End Try
                End If
            End Try

            Return found
        End Function
    End Module
End Namespace
