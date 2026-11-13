Imports JellyfinPlayer.Shared

Namespace Services
    Public Module ItemNavigator
        Public Sub Open(item As BaseItemDto)
            If item Is Nothing Then Return
            If item.Type = "Series" Then
                Services.NavigationService.Navigate(GetType(SeriesPage), item)
            ElseIf item.Type = "Playlist" Then
                Services.NavigationService.Navigate(GetType(PlaylistPage), item)
            Else
                Services.NavigationService.Navigate(GetType(ItemDetailPage), item)
            End If
        End Sub
    End Module
End Namespace
