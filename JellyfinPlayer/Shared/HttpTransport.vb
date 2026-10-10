Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks

Namespace Global.JellyfinPlayer.Shared
    Public Class HttpTransport
        Implements IHttpTransport

        Private ReadOnly _client As HttpClient

        Public Sub New()
            _client = New HttpClient()
            _client.Timeout = TimeSpan.FromSeconds(15)
        End Sub

        Public Function GetStringAsync(url As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.GetStringAsync
            Return SendAsync("GET", url, Nothing, headers)
        End Function

        Public Function PostJsonAsync(url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.PostJsonAsync
            Return SendAsync("POST", url, json, headers)
        End Function

        Public Async Function SendAsync(method As String, url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.SendAsync
            Dim request As New HttpRequestMessage(New HttpMethod(method), url)
            If json IsNot Nothing Then
                request.Content = New StringContent(json, Encoding.UTF8, "application/json")
            End If
            If headers IsNot Nothing Then
                For Each h In headers
                    request.Headers.TryAddWithoutValidation(h.Key, h.Value)
                Next
            End If

            Try
                Dim response As HttpResponseMessage = Await _client.SendAsync(request).ConfigureAwait(False)
                Dim body As String = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                If Not response.IsSuccessStatusCode Then
                    Dim mediaType As String = If(response.Content.Headers.ContentType Is Nothing, "", response.Content.Headers.ContentType.MediaType)
                    Throw New JellyfinException(ResponseFailures.DescribeHttpFailure(CInt(response.StatusCode), mediaType, body),
                                                CInt(response.StatusCode), JellyfinErrorKind.HttpFailure)
                End If
                Return body
            Catch ex As Exception
                Throw MapException(ex)
            End Try
        End Function

        ' Pure mapping so the failure messages can be unit-tested without a network.
        Public Shared Function MapException(ex As Exception) As JellyfinException
            If TypeOf ex Is JellyfinException Then Return DirectCast(ex, JellyfinException)
            If TypeOf ex Is TaskCanceledException OrElse TypeOf ex Is OperationCanceledException Then
                Return New JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout)
            End If
            If TypeOf ex Is HttpRequestException Then
                Return New JellyfinException("Could not reach the server: " & ex.Message, 0, JellyfinErrorKind.Unreachable)
            End If
            Return New JellyfinException(ex.Message, 0)
        End Function
    End Class
End Namespace
