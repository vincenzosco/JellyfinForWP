Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

Namespace Services
    ' One row of the picker; keeps XAML binding simple (no converters), like PosterItem.
    Public Class AccountItem
        Public Property Account As AccountDto
        Public Property Title As String
        Public Property Subtitle As String
        Public Property IsActive As Boolean
        Public Property Marker As String

        ' Locals are named `saved`, not `account`: VB is case-insensitive and the
        ' Account property above would swallow them.
        Public Shared Function Build(accounts As IList(Of AccountDto), activeId As String) As List(Of AccountItem)
            Dim result As New List(Of AccountItem)()
            If accounts Is Nothing Then Return result
            For Each saved In accounts
                result.Add(One(saved, activeId))
            Next
            Return result
        End Function

        Public Shared Function One(saved As AccountDto, activeId As String) As AccountItem
            Dim item As New AccountItem()
            item.Account = saved
            item.Title = DisplayName(saved)
            item.Subtitle = SubtitleFor(saved)
            item.IsActive = saved.Id = activeId
            item.Marker = If(item.IsActive, Localized.Text("CurrentAccountMarker"), "")
            Return item
        End Function

        ' A migrated account has no name until Home fetches it once.
        Public Shared Function DisplayName(saved As AccountDto) As String
            If saved Is Nothing Then Return ""
            If Not String.IsNullOrEmpty(saved.UserName) Then Return saved.UserName
            Return If(saved.UserId, "")
        End Function

        Private Shared Function SubtitleFor(saved As AccountDto) As String
            If saved Is Nothing Then Return ""
            If String.IsNullOrEmpty(saved.ServerName) Then Return saved.BaseUrl
            Return saved.ServerName & " - " & saved.BaseUrl
        End Function
    End Class
End Namespace
