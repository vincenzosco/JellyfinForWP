Imports JellyfinPlayer.Shared

Namespace Services
    Public Module ItemNavigator
        Public Sub Open(item As BaseItemDto)
            If item Is Nothing Then Return
            If item.Type = "Series" Then
                NavigationService.Navigate(GetType(SeriesPage), item)
            Else
                NavigationService.Navigate(GetType(ItemDetailPage), item)
            End If
        End Sub
    End Module
End Namespace
