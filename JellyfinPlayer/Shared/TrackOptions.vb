Imports System
Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    ' One row of the player's track picker. Index is Nothing only for the explicit
    ' "off" subtitle entry.
    Public Class TrackOption
        Public Property Index As Integer?
        Public Property Label As String
    End Class

    ' The audio and subtitle streams a Jellyfin media source carries. Pure, so the
    ' contents of the picker and the "is this the default track" rule are covered by
    ' the desktop tests instead of by a device.
    Public Module TrackOptions
        Public Function Audio(streams As List(Of MediaStreamDto)) As List(Of TrackOption)
            Dim options As New List(Of TrackOption)()
            If streams Is Nothing Then Return options
            For Each stream In streams
                If stream IsNot Nothing AndAlso IsType(stream, "Audio") Then
                    options.Add(New TrackOption With {.Index = stream.Index, .Label = Describe(stream)})
                End If
            Next
            Return options
        End Function

        ' "Off" comes first: choosing it is how the user gets back to the
        ' untouched stream after watching with burned-in subtitles.
        Public Function Subtitles(streams As List(Of MediaStreamDto), offLabel As String) As List(Of TrackOption)
            Dim options As New List(Of TrackOption)()
            options.Add(New TrackOption With {.Index = Nothing, .Label = offLabel})
            If streams Is Nothing Then Return options
            For Each stream In streams
                If stream IsNot Nothing AndAlso IsType(stream, "Subtitle") Then
                    options.Add(New TrackOption With {.Index = stream.Index, .Label = Describe(stream)})
                End If
            Next
            Return options
        End Function

        ' The track the server would pick: the one marked default, else the first.
        Public Function DefaultAudioIndex(streams As List(Of MediaStreamDto)) As Integer?
            Dim first As Integer? = Nothing
            If streams Is Nothing Then Return Nothing
            For Each stream In streams
                If stream IsNot Nothing AndAlso IsType(stream, "Audio") Then
                    If stream.IsDefault Then Return stream.Index
                    If Not first.HasValue Then first = stream.Index
                End If
            Next
            Return first
        End Function

        ' Nothing goes to the server while the default audio is playing: a normal
        ' playback must still direct-play instead of being transcoded for nothing.
        Public Function AudioSelection(streams As List(Of MediaStreamDto), index As Integer?) As Integer?
            If Not index.HasValue Then Return Nothing
            If DefaultAudioIndex(streams) = index Then Return Nothing
            Return index
        End Function

        Public Function Describe(stream As MediaStreamDto) As String
            If stream Is Nothing Then Return ""
            If Not String.IsNullOrEmpty(stream.DisplayTitle) Then Return stream.DisplayTitle
            Dim parts As New List(Of String)()
            If Not String.IsNullOrEmpty(stream.Language) Then parts.Add(stream.Language)
            If Not String.IsNullOrEmpty(stream.Codec) Then parts.Add(stream.Codec.ToUpperInvariant())
            If parts.Count = 0 Then parts.Add("Track " & stream.Index.ToString())
            Return String.Join(" - ", parts.ToArray())
        End Function

        Private Function IsType(stream As MediaStreamDto, type As String) As Boolean
            Return String.Equals(stream.Type, type, StringComparison.OrdinalIgnoreCase)
        End Function
    End Module
End Namespace
