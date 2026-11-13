Imports System
Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    Public Class QualityOption
        Public Property Bitrate As Integer
        Public Property Label As String
    End Class

    ' The bitrates the player offers. They are the same values the settings screen
    ' stores; the player writes the same preference, so the two screens agree.
    Public Module QualityOptions
        Public ReadOnly Bitrates As Integer() = {2000000, 4000000, 8000000, 20000000}

        Public Function Choices(mbpsSuffix As String) As List(Of QualityOption)
            Dim result As New List(Of QualityOption)()
            For Each bitrate In Bitrates
                result.Add(New QualityOption With {.Bitrate = bitrate,
                                                   .Label = (bitrate \ 1000000).ToString() & " " & mbpsSuffix})
            Next
            Return result
        End Function

        Public Function IndexOf(bitrate As Integer) As Integer
            Return Array.IndexOf(Bitrates, bitrate)
        End Function
    End Module
End Namespace
