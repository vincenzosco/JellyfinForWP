Imports System
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading.Tasks

' Answers every HTTP request with one canned response. Test-only: it lets the real
' HttpTransport run against a socket that speaks HTML instead of Jellyfin JSON,
' without needing admin rights (HttpListener would) or touching the LAN.
Public Class StubHttpServer
    Implements IDisposable

    Private ReadOnly _listener As TcpListener
    Private ReadOnly _response As Byte()
    Private ReadOnly _port As Integer

    Public Sub New(statusLine As String, contentType As String, body As String)
        Dim payload As Byte() = Encoding.UTF8.GetBytes(body)
        Dim header As String = "HTTP/1.1 " & statusLine & vbCrLf &
                               "Content-Type: " & contentType & vbCrLf &
                               "Content-Length: " & payload.Length.ToString() & vbCrLf &
                               "Connection: close" & vbCrLf & vbCrLf
        _response = Join(Encoding.UTF8.GetBytes(header), payload)
        _listener = New TcpListener(IPAddress.Loopback, 0)
        _listener.Start()
        _port = CType(_listener.LocalEndpoint, IPEndPoint).Port
        Task.Run(AddressOf Serve)
    End Sub

    Public ReadOnly Property Port As Integer
        Get
            Return _port
        End Get
    End Property

    Public ReadOnly Property BaseUrl As String
        Get
            Return "http://127.0.0.1:" & _port.ToString()
        End Get
    End Property

    Private Function Join(first As Byte(), second As Byte()) As Byte()
        Dim all(first.Length + second.Length - 1) As Byte
        Array.Copy(first, 0, all, 0, first.Length)
        Array.Copy(second, 0, all, first.Length, second.Length)
        Return all
    End Function

    Private Sub Serve()
        Do While True
            Dim client As TcpClient = Nothing
            Try
                client = _listener.AcceptTcpClient()
                Using stream = client.GetStream()
                    ' Read the request headers, answer once, then close.
                    Dim buffer(1023) As Byte
                    Dim text As New StringBuilder()
                    Do While text.ToString().IndexOf(vbCrLf & vbCrLf, StringComparison.Ordinal) < 0
                        Dim read As Integer = stream.Read(buffer, 0, buffer.Length)
                        If read <= 0 Then Exit Do
                        text.Append(Encoding.UTF8.GetString(buffer, 0, read))
                    Loop
                    stream.Write(_response, 0, _response.Length)
                    stream.Flush()
                End Using
            Catch
                Exit Do
            Finally
                If client IsNot Nothing Then client.Close()
            End Try
        Loop
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            _listener.Stop()
        Catch
        End Try
    End Sub
End Class
