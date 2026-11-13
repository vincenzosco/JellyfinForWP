Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading.Tasks

Namespace Global.JellyfinPlayer.Shared
    ' Cross-target HTTP transport: compiles against desktop .NET 4.5 (for MSTest)
    ' and the Windows Phone Silverlight 8.1 reference assemblies. HttpWebRequest
    ' is used because Silverlight does not expose HttpClient; the request uses the
    ' begin/end async pattern so the UI thread never blocks.
    Public Class HttpTransport
        Implements IHttpTransport

        Private Const RequestTimeoutMs As Integer = 15000

        Public Function GetStringAsync(url As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.GetStringAsync
            Return SendAsync("GET", url, Nothing, headers)
        End Function

        Public Function PostJsonAsync(url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.PostJsonAsync
            Return SendAsync("POST", url, json, headers)
        End Function

        Public Function SendAsync(method As String, url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.SendAsync
            Dim completion As New TaskCompletionSource(Of String)()
            Dim request As HttpWebRequest = Nothing
            Dim timedOut As Boolean = False

            Try
                request = DirectCast(WebRequest.Create(url), HttpWebRequest)
                request.Method = method
                If headers IsNot Nothing Then
                    For Each header In headers
                        request.Headers(header.Key) = header.Value
                    Next
                End If

                ' A manual 15 s bound: Silverlight's HttpWebRequest has no Timeout
                ' property, and the timer must not block the calling thread.
                Dim timer = Task.Delay(RequestTimeoutMs).ContinueWith(
                    Sub(t)
                        timedOut = True
                        Try
                            request.Abort()
                        Catch
                        End Try
                    End Sub)

                If json Is Nothing Then
                    BeginGetResponse(request, completion, Function() timedOut)
                Else
                    request.ContentType = "application/json; charset=utf-8"
                    Dim payload = Encoding.UTF8.GetBytes(json)
                    request.ContentLength = payload.Length
                    request.BeginGetRequestStream(
                        Sub(result)
                            Try
                                Using requestStream = request.EndGetRequestStream(result)
                                    requestStream.Write(payload, 0, payload.Length)
                                End Using
                                BeginGetResponse(request, completion, Function() timedOut)
                            Catch ex As Exception
                                completion.TrySetException(MapException(ex, timedOut))
                            End Try
                        End Sub, Nothing)
                End If
            Catch ex As Exception
                completion.TrySetException(MapException(ex, timedOut))
            End Try

            Return completion.Task
        End Function

        Private Shared Sub BeginGetResponse(request As HttpWebRequest, completion As TaskCompletionSource(Of String), isTimedOut As Func(Of Boolean))
            Try
                request.BeginGetResponse(
                    Sub(result)
                        Try
                            Using response = DirectCast(request.EndGetResponse(result), HttpWebResponse)
                                Using responseStream = response.GetResponseStream()
                                    Using reader As New StreamReader(responseStream)
                                        Dim body = reader.ReadToEnd()
                                        Dim code = CInt(response.StatusCode)
                                        If code < 200 OrElse code >= 300 Then
                                            Throw New JellyfinException(ResponseFailures.DescribeHttpFailure(
                                                code, response.ContentType, body), code, JellyfinErrorKind.HttpFailure)
                                        End If
                                        completion.TrySetResult(body)
                                    End Using
                                End Using
                            End Using
                        Catch ex As WebException
                            Dim response = TryCast(ex.Response, HttpWebResponse)
                            If response Is Nothing Then
                                completion.TrySetException(MapException(ex, isTimedOut()))
                            Else
                                Try
                                    Using response
                                        Using responseStream = response.GetResponseStream()
                                            Using reader As New StreamReader(responseStream)
                                                Dim body = reader.ReadToEnd()
                                                Dim mediaType = If(response.ContentType, String.Empty).Split(";"c)(0)
                                                Dim code = CInt(response.StatusCode)
                                                completion.TrySetException(New JellyfinException(
                                                    ResponseFailures.DescribeHttpFailure(code, mediaType, body),
                                                    code, JellyfinErrorKind.HttpFailure))
                                            End Using
                                        End Using
                                    End Using
                                Catch readException As Exception
                                    completion.TrySetException(MapException(readException, isTimedOut()))
                                End Try
                            End If
                        Catch ex As Exception
                            completion.TrySetException(MapException(ex, isTimedOut()))
                        End Try
                    End Sub, Nothing)
            Catch ex As Exception
                completion.TrySetException(MapException(ex, isTimedOut()))
            End Try
        End Sub

        ' Pure mapping so the failure messages can be unit-tested without a network.
        Public Shared Function MapException(ex As Exception) As JellyfinException
            Return MapException(ex, False)
        End Function

        Private Shared Function MapException(ex As Exception, timedOut As Boolean) As JellyfinException
            If TypeOf ex Is JellyfinException Then Return DirectCast(ex, JellyfinException)
            If timedOut Then
                Return New JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout)
            End If
            If TypeOf ex Is TaskCanceledException OrElse TypeOf ex Is OperationCanceledException Then
                Return New JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout)
            End If
            If TypeOf ex Is WebException Then
                Dim webException = DirectCast(ex, WebException)
                If webException.Status = WebExceptionStatus.Timeout Then
                    Return New JellyfinException("The server did not respond (timeout).", 0, JellyfinErrorKind.Timeout)
                End If
                Return New JellyfinException("Could not reach the server: " & webException.Message, 0, JellyfinErrorKind.Unreachable)
            End If
            If TypeOf ex Is IOException Then
                Return New JellyfinException("Could not reach the server: " & ex.Message, 0, JellyfinErrorKind.Unreachable)
            End If
            ' HttpRequestException lives in System.Net.Http, which the Windows Phone
            ' Silverlight reference set does not expose. Match it by name so the shared
            ' mapping compiles on both targets while still classifying it as unreachable.
            If ex.GetType().Name = "HttpRequestException" Then
                Return New JellyfinException("Could not reach the server: " & ex.Message, 0, JellyfinErrorKind.Unreachable)
            End If
            Return New JellyfinException(ex.Message, 0)
        End Function
    End Class
End Namespace
