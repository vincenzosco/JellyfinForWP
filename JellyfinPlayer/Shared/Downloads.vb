Imports System
Imports System.Globalization
Imports System.Runtime.Serialization
Imports System.Text

Namespace Global.JellyfinPlayer.Shared
    ' One stored video. It travels as JSON next to the files, so the app can list
    ' and resume downloads with no server round trip.
    <DataContract>
    Public Class DownloadRecordDto
        <DataMember> Public Property ItemId As String
        <DataMember> Public Property Name As String
        <DataMember> Public Property Subtitle As String
        <DataMember> Public Property FileName As String
        <DataMember> Public Property RunTimeTicks As Long?
        <DataMember> Public Property PositionTicks As Long?
        <DataMember> Public Property SizeBytes As Long
        <DataMember> Public Property DownloadedAtTicks As Long
    End Class

    ' The rules the download screens depend on: where a file goes, how much of it
    ' has arrived, and how big it is in words. Pure, so they are unit-tested.
    Public Module Downloads
        Private Const Megabyte As Long = 1048576

        ' Server ids are GUIDs, but a file name is built from whatever arrives, so
        ' everything that is not a letter, a digit, a dash or an underscore goes.
        Public Function FileNameFor(itemId As String) As String
            Dim safe As New StringBuilder()
            For Each c In If(itemId, String.Empty)
                If Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c Then safe.Append(c)
            Next
            If safe.Length = 0 Then safe.Append("download")
            Return safe.ToString() & ".mp4"
        End Function

        Public Function Percent(received As Long, total As Long) As Integer
            If total <= 0 Then Return 0
            Dim value = CInt(Math.Min(received, total) * 100L \ total)
            If value < 0 Then Return 0
            If value > 100 Then Return 100
            Return value
        End Function

        Public Function SizeLabel(bytes As Long) As String
            If bytes >= 1024L * Megabyte Then
                Return (bytes / (1024.0 * Megabyte)).ToString("0.0", CultureInfo.InvariantCulture) & " GB"
            End If
            Dim megabytes = CInt(Math.Round(bytes / CDbl(Megabyte)))
            If megabytes < 1 Then megabytes = 1
            Return megabytes.ToString() & " MB"
        End Function
    End Module
End Namespace
