Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    ' The episode that follows the one that just ended. Pure: the caller supplies
    ' the series' episodes, so the ordering rule is tested on the desktop rather
    ' than discovered on a phone after the credits.
    Public Module NextEpisode
        Public Function Find(current As BaseItemDto, episodes As IEnumerable(Of BaseItemDto)) As BaseItemDto
            If current Is Nothing OrElse episodes Is Nothing Then Return Nothing
            If Not current.ParentIndexNumber.HasValue OrElse Not current.IndexNumber.HasValue Then Return Nothing

            Dim season = current.ParentIndexNumber.Value
            Dim number = current.IndexNumber.Value
            Dim best As BaseItemDto = Nothing
            For Each candidate In episodes
                If candidate Is Nothing OrElse candidate.Id = current.Id Then Continue For
                If Not candidate.ParentIndexNumber.HasValue OrElse Not candidate.IndexNumber.HasValue Then Continue For
                Dim candidateSeason = candidate.ParentIndexNumber.Value
                Dim candidateNumber = candidate.IndexNumber.Value
                ' Same season: only a later episode counts. Later season: anything.
                If candidateSeason = season AndAlso candidateNumber <= number Then Continue For
                If candidateSeason < season Then Continue For
                If best Is Nothing OrElse Before(candidate, best) Then best = candidate
            Next
            Return best
        End Function

        Private Function Before(left As BaseItemDto, right As BaseItemDto) As Boolean
            If left.ParentIndexNumber.Value <> right.ParentIndexNumber.Value Then
                Return left.ParentIndexNumber.Value < right.ParentIndexNumber.Value
            End If
            Return left.IndexNumber.Value < right.IndexNumber.Value
        End Function
    End Module
End Namespace
