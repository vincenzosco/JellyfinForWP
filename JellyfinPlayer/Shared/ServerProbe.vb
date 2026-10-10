Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks

Namespace Global.JellyfinPlayer.Shared
    ' UDP discovery on port 7359 answers with anything that cares to reply, and
    ' that reply is only a shape ({Address, Id, Name}) - not proof of a Jellyfin
    ' server. Probing the address before filling it in keeps manual control when
    ' the reply came from something else.
    Public Module ServerProbe
        Private Const MaxProbes As Integer = 3

        Public Async Function FindFirstJellyfinAsync(candidates As IEnumerable(Of DiscoveredServerDto),
                                                     createClient As Func(Of String, JellyfinApiClient)) As Task(Of DiscoveredServerDto)
            If candidates Is Nothing OrElse createClient Is Nothing Then Return Nothing

            For Each candidate In candidates.Where(Function(c) c IsNot Nothing AndAlso Not String.IsNullOrEmpty(c.Address)).Take(MaxProbes)
                Try
                    If Await createClient(candidate.Address).ValidateConnectionAsync().ConfigureAwait(False) Then
                        Return candidate
                    End If
                Catch
                    ' A candidate that does not answer, or answers with something
                    ' that is not Jellyfin JSON, is simply not the server.
                End Try
            Next

            Return Nothing
        End Function
    End Module
End Namespace
