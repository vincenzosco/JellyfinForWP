Imports System
Imports System.Text

Namespace Global.JellyfinPlayer.Shared
    ' Turns a non-Jellyfin answer into a short, classified error. Before this,
    ' a server that answered with a web page put the whole page (or a raw
    ' SerializationException message) in front of the user.
    Public Module ResponseFailures
        Private Const MaxSnippetLength As Integer = 120
        Private Const NotJsonText As String = "The address answered with a web page instead of Jellyfin JSON. Check the address and the port."

        ' An HTML (or XML) body never starts with JSON; anything else is left to
        ' the deserializer.
        Public Function LooksLikeHtml(body As String) As Boolean
            If String.IsNullOrEmpty(body) Then Return False
            Return body.TrimStart().StartsWith("<", StringComparison.Ordinal)
        End Function

        ' Never returns the response body when that body is a web page.
        Public Function DescribeHttpFailure(statusCode As Integer, contentType As String, body As String) As String
            Dim message As New StringBuilder("The server answered HTTP " & statusCode.ToString() & ".")
            If LooksLikeHtml(body) OrElse (contentType IsNot Nothing AndAlso contentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0) Then
                message.Append(" It returned a web page instead of the Jellyfin API - check the address and the port.")
            ElseIf Not String.IsNullOrEmpty(body) Then
                message.Append(" ").Append(Snippet(body))
            End If
            Return message.ToString()
        End Function

        ' Deserializes a response body, classifying anything that is not
        ' Jellyfin JSON instead of letting SerializationException escape.
        Public Function Parse(Of T)(body As String) As T
            If LooksLikeHtml(body) Then Throw NotJson(Nothing)
            Try
                Return Json.Deserialize(Of T)(body)
            Catch ex As Exception
                Throw NotJson(ex)
            End Try
        End Function

        Private Function NotJson(inner As Exception) As JellyfinException
            Return New JellyfinException(NotJsonText, 0, JellyfinErrorKind.NonJsonResponse, inner)
        End Function

        Private Function Snippet(body As String) As String
            Dim collapsed As String = Collapse(body)
            If collapsed.Length <= MaxSnippetLength Then Return collapsed
            Return collapsed.Substring(0, MaxSnippetLength) & "..."
        End Function

        Private Function Collapse(body As String) As String
            Dim sb As New StringBuilder()
            Dim previousWasSpace As Boolean = False
            For Each c As Char In body
                If Char.IsWhiteSpace(c) Then
                    If Not previousWasSpace Then sb.Append(" "c)
                    previousWasSpace = True
                Else
                    sb.Append(c)
                    previousWasSpace = False
                End If
            Next
            Return sb.ToString().Trim()
        End Function
    End Module
End Namespace
