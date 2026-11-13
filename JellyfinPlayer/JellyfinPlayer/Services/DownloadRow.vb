Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

Namespace Services
    ' A row of the downloads screens. Built once here so both the home section and
    ' the manage page show the same title, subtitle and size.
    Public Class DownloadRow
        Public Property Record As DownloadRecordDto
        Public Property Title As String
        Public Property Subtitle As String
        Public Property Size As String
        ' The delete button lives inside a data template, where a code-set label
        ' cannot reach it; the row carries the localised word instead.
        Public Property DeleteLabel As String

        Public Shared Function Build(records As IList(Of DownloadRecordDto)) As List(Of DownloadRow)
            Dim rows As New List(Of DownloadRow)()
            If records Is Nothing Then Return rows
            For Each entry In records
                If entry Is Nothing Then Continue For
                Dim row As New DownloadRow()
                row.Record = entry
                row.Title = If(String.IsNullOrEmpty(entry.Name), entry.ItemId, entry.Name)
                row.Subtitle = entry.Subtitle
                row.Size = Downloads.SizeLabel(entry.SizeBytes)
                row.DeleteLabel = Localized.Text("DeleteDownload")
                rows.Add(row)
            Next
            Return rows
        End Function
    End Class
End Namespace
