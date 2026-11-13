Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading.Tasks

<TestClass>
Public Class HttpTransportTests
    <TestMethod>
    Public Sub Get_sends_the_authorization_header_and_returns_the_response_body()
        Using server As New RecordingHttpServer("200 OK", "application/json", "{""ServerName"":""Jellyfin""}")
            Dim headers As New Dictionary(Of String, String) From {
                {"Authorization", JellyfinAuthHeader.WithToken("token-1", "device-1")}}
            Dim body = New HttpTransport().GetStringAsync(server.Url, headers).GetAwaiter().GetResult()

            Assert.AreEqual("{""ServerName"":""Jellyfin""}", body)
            Assert.IsTrue(server.RequestText.Contains("Authorization: MediaBrowser Token=""token-1"""), server.RequestText)
        End Using
    End Sub

    <TestMethod>
    Public Sub Post_sets_json_content_type_and_preserves_the_request_body()
        Using server As New RecordingHttpServer("200 OK", "application/json", "{}")
            Dim headers As New Dictionary(Of String, String) From {{"Authorization", JellyfinAuthHeader.Identify("device-2")}}
            Dim body = New HttpTransport().PostJsonAsync(server.Url, "{""Username"":""alice""}", headers).GetAwaiter().GetResult()

            Assert.AreEqual("{}", body)
            Assert.IsTrue(server.RequestText.Contains("Content-Type: application/json; charset=utf-8"), server.RequestText)
            Assert.IsTrue(server.RequestText.EndsWith("{""Username"":""alice""}"), server.RequestText)
        End Using
    End Sub

    <TestMethod>
    Public Sub Http_error_keeps_status_and_classifies_the_body()
        Using server As New RecordingHttpServer("401 Unauthorized", "application/json", "{""Message"":""invalid token""}")
            Try
                Dim ignored = New HttpTransport().GetStringAsync(server.Url, Nothing).GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(401, ex.StatusCode)
                Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind)
                StringAssert.Contains(ex.Message, "invalid token")
            End Try
        End Using
    End Sub

    <TestMethod>
    Public Sub Web_timeout_is_classified_as_timeout()
        Dim failure = HttpTransport.MapException(New WebException("request timed out", WebExceptionStatus.Timeout))

        Assert.AreEqual(JellyfinErrorKind.Timeout, failure.Kind)
    End Sub

    <TestMethod>
    Public Sub Web_name_resolution_failure_is_classified_as_unreachable()
        Dim failure = HttpTransport.MapException(New WebException("host not found", WebExceptionStatus.NameResolutionFailure))

        Assert.AreEqual(JellyfinErrorKind.Unreachable, failure.Kind)
    End Sub
End Class

Friend Class RecordingHttpServer
    Implements IDisposable

    Private ReadOnly _listener As Net.Sockets.TcpListener
    Private ReadOnly _body As String
    Private ReadOnly _status As String
    Private ReadOnly _contentType As String
    Private ReadOnly _task As Task(Of String)

    Public Sub New(status As String, contentType As String, body As String)
        _status = status
        _contentType = contentType
        _body = body
        _listener = New Net.Sockets.TcpListener(IPAddress.Loopback, 0)
        _listener.Start()
        Dim endpoint = DirectCast(_listener.LocalEndpoint, IPEndPoint)
        _url = "http://127.0.0.1:" & endpoint.Port.ToString() & "/"
        _task = Task.Run(Of String)(AddressOf Receive)
    End Sub

    Public ReadOnly Property Url As String
        Get
            Return _url
        End Get
    End Property

    Private ReadOnly _url As String

    Public ReadOnly Property RequestText As String
        Get
            Return _task.GetAwaiter().GetResult()
        End Get
    End Property

    Private Function Receive() As String
        Using client = _listener.AcceptTcpClient()
            Using stream = client.GetStream()
                Dim request As New StringBuilder()
                Dim buffer(1023) As Byte
                Dim contentLength = 0
                Dim headerComplete = False
                Dim bodyBytes As Integer = 0
                Do
                    Dim count = stream.Read(buffer, 0, buffer.Length)
                    If count <= 0 Then Exit Do
                    request.Append(Encoding.UTF8.GetString(buffer, 0, count))
                    Dim text = request.ToString()
                    Dim split = text.IndexOf(vbCrLf & vbCrLf, StringComparison.Ordinal)
                    If split >= 0 AndAlso Not headerComplete Then
                        headerComplete = True
                        For Each line In text.Substring(0, split).Split(New String() {vbCrLf}, StringSplitOptions.None)
                            If line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) Then
                                Integer.TryParse(line.Substring("Content-Length:".Length).Trim(), contentLength)
                            End If
                        Next
                        bodyBytes = Encoding.UTF8.GetByteCount(text.Substring(split + 4))
                    ElseIf headerComplete Then
                        bodyBytes += count
                    End If
                    If headerComplete AndAlso bodyBytes >= contentLength Then Exit Do
                Loop
                Dim payload = Encoding.UTF8.GetBytes(_body)
                Dim response = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 " & _status & vbCrLf &
                    "Content-Type: " & _contentType & vbCrLf &
                    "Content-Length: " & payload.Length.ToString() & vbCrLf &
                    "Connection: close" & vbCrLf & vbCrLf)
                stream.Write(response, 0, response.Length)
                stream.Write(payload, 0, payload.Length)
                stream.Flush()
                Return request.ToString()
            End Using
        End Using
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        _listener.Stop()
    End Sub
End Class
