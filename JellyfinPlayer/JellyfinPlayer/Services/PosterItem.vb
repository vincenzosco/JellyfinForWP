Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports Windows.UI.Xaml.Media
Imports Windows.UI.Xaml.Media.Imaging

Namespace Services
    ' View model for the grids; keeps XAML binding simple (no converters).
    Public Class PosterItem
        Public Property Id As String
        Public Property Title As String
        Public Property Subtitle As String
        Public Property Poster As ImageSource
        Public Property Raw As BaseItemDto

        Public Shared Function Build(items As IList(Of BaseItemDto), baseUrl As String, token As String) As List(Of PosterItem)
            Dim result As New List(Of PosterItem)()
            If items Is Nothing Then Return result
            For Each item In items
                result.Add(One(item, baseUrl, token))
            Next
            Return result
        End Function

        Public Shared Function One(item As BaseItemDto, baseUrl As String, token As String) As PosterItem
            Dim p As New PosterItem()
            p.Id = item.Id
            p.Raw = item
            p.Title = item.Name
            p.Subtitle = SubtitleFor(item)
            If ImageHelper.HasPrimaryImage(item) Then
                p.Poster = New BitmapImage(ImageHelper.ImageUrl(baseUrl, token, item.Id, 300))
            End If
            Return p
        End Function

        Private Shared Function SubtitleFor(item As BaseItemDto) As String
            If item.Type = "Episode" Then
                Dim number As String = ""
                If item.ParentIndexNumber.HasValue Then number &= "S" & item.ParentIndexNumber.Value.ToString("00")
                If item.IndexNumber.HasValue Then number &= "E" & item.IndexNumber.Value.ToString("00")
                If Not String.IsNullOrEmpty(item.SeriesName) Then Return item.SeriesName & " " & number
                Return number
            End If
            If item.ProductionYear.HasValue Then Return item.ProductionYear.Value.ToString()
            Return ""
        End Function
    End Class
End Namespace
