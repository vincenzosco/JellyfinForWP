Imports System.Collections.Generic
Imports System.Threading.Tasks

Namespace Global.JellyfinPlayer.Shared
    ' The seam that lets the API client be unit-tested without a network.
    Public Interface IHttpTransport
        Function GetStringAsync(url As String, headers As IDictionary(Of String, String)) As Task(Of String)
        Function PostJsonAsync(url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String)
        Function SendAsync(method As String, url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String)
    End Interface
End Namespace
