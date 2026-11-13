Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    ' One row of the genre panel. The first row is always "all genres", which is
    ' no filter at all - its Id is Nothing.
    Public Class GenreChoice
        Public Property Id As String
        Public Property Name As String
        Public Property IsAll As Boolean
    End Class

    Public Module LibraryFilters
        Public Function GenreChoices(genres As List(Of BaseItemDto), allLabel As String) As List(Of GenreChoice)
            Dim choices As New List(Of GenreChoice)()
            choices.Add(New GenreChoice With {.Name = allLabel, .IsAll = True})
            If genres IsNot Nothing Then
                For Each genre In genres
                    ' A genre without an id cannot be filtered on, and a nameless
                    ' one would show an empty row.
                    If genre Is Nothing OrElse String.IsNullOrEmpty(genre.Id) Then Continue For
                    choices.Add(New GenreChoice With {.Id = genre.Id,
                                                      .Name = If(String.IsNullOrEmpty(genre.Name), genre.Id, genre.Name)})
                Next
            End If
            Return choices
        End Function
    End Module
End Namespace
