Imports System

Namespace Global.JellyfinPlayer.Shared
    ' Jellyfin servers answer a UDP probe on port 7359 with
    ' {"Address":"http://host:8096","Id":"...","Name":"..."}.
    Public Module ServerDiscoveryParser
        Public Const DiscoveryPort As Integer = 7359
        Public Const ProbePayload As String = "who is JellyfinServer?"

        ' Never throws: a malformed datagram simply returns False.
        Public Function TryParse(payload As String, ByRef server As DiscoveredServerDto) As Boolean
            server = Nothing
            If String.IsNullOrEmpty(payload) Then Return False
            Try
                Dim parsed = Json.Deserialize(Of DiscoveredServerDto)(payload)
                If parsed Is Nothing Then Return False
                If String.IsNullOrEmpty(parsed.Address) Then Return False
                server = parsed
                Return True
            Catch
                Return False
            End Try
        End Function
    End Module
End Namespace
