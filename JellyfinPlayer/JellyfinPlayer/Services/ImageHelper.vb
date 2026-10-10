Imports System
Imports JellyfinPlayer.Shared

Namespace Services
    Public Module ImageHelper
        ' BitmapImage cannot set HTTP headers, so image URLs carry ?api_key=.
        Public Function ImageUrl(baseUrl As String, token As String, itemId As String, maxHeight As Integer) As Uri
            Return New Uri(New JellyfinUrls(baseUrl, token).PrimaryImage(itemId, maxHeight))
        End Function

        Public Function BackdropUrl(baseUrl As String, token As String, itemId As String, maxWidth As Integer) As Uri
            Return New Uri(New JellyfinUrls(baseUrl, token).BackdropImage(itemId, maxWidth))
        End Function

        Public Function HasPrimaryImage(item As BaseItemDto) As Boolean
            Return item IsNot Nothing AndAlso item.ImageTags IsNot Nothing AndAlso Not String.IsNullOrEmpty(item.ImageTags.Primary)
        End Function
    End Module
End Namespace
