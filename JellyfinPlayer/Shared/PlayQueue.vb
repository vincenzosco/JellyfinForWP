Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    ' What the player is asked to play: one item, plus the playlist it came from so
    ' the entries after it continue without a trip back to the playlist screen.
    Public Class PlayRequest
        Public Property Item As BaseItemDto
        Public Property Queue As List(Of BaseItemDto)
        Public Property Index As Integer
    End Class

    ' A playlist plays as a queue: the entry that was tapped, then whatever follows
    ' in the playlist. The player asks this module what comes next, so the rule is
    ' covered on the desktop instead of only on a device.
    Public Module PlayQueue
        Public Function At(items As IList(Of BaseItemDto), index As Integer) As BaseItemDto
            If items Is Nothing OrElse index < 0 OrElse index >= items.Count Then Return Nothing
            Return items(index)
        End Function

        ' -1 means "this was the last entry"; the caller then falls back to its
        ' own continuation rule (an episode's next episode).
        Public Function NextIndex(items As IList(Of BaseItemDto), currentIndex As Integer) As Integer
            If items Is Nothing Then Return -1
            Dim candidate = currentIndex + 1
            If candidate < 0 OrElse candidate >= items.Count Then Return -1
            Return candidate
        End Function
    End Module
End Namespace
