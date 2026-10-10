Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks

' Records the last request and returns canned JSON (or a faulted task).
Public Class FakeTransport
    Implements IHttpTransport

    Public Handler As Func(Of String, String)
    Public ErrorToThrow As Exception

    Public LastUrl As String
    Public LastMethod As String
    Public LastJson As String
    Public LastHeaders As IDictionary(Of String, String)

    Public Function GetStringAsync(url As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.GetStringAsync
        Return SendAsync("GET", url, Nothing, headers)
    End Function

    Public Function PostJsonAsync(url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.PostJsonAsync
        Return SendAsync("POST", url, json, headers)
    End Function

    Public Function SendAsync(method As String, url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String) Implements IHttpTransport.SendAsync
        LastUrl = url
        LastMethod = method
        LastJson = json
        LastHeaders = headers
        If ErrorToThrow IsNot Nothing Then
            Dim tcs As New TaskCompletionSource(Of String)()
            tcs.SetException(ErrorToThrow)
            Return tcs.Task
        End If
        Return Task.FromResult(If(Handler IsNot Nothing, Handler(url), "{}"))
    End Function
End Class
