Imports System

Namespace Global.JellyfinPlayer.Shared
    ' After a stream fails the player may try the other way - but only once, and
    ' only when that URL is a different one. Without this rule two broken URLs
    ' bounce the user between them and the failure sentence never arrives.
    Public Module StreamFallback
        Public Function Alternative(current As Uri, fallback As Uri, alreadyRetried As Boolean) As Uri
            If alreadyRetried Then Return Nothing
            If current Is Nothing OrElse fallback Is Nothing Then Return Nothing
            If String.Equals(current.ToString(), fallback.ToString(), StringComparison.OrdinalIgnoreCase) Then Return Nothing
            Return fallback
        End Function
    End Module
End Namespace
