Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.IsolatedStorage
Imports JellyfinPlayer.Shared

Namespace Services
    ' Where the offline videos live: one folder, one file per item, one JSON
    ' document listing them. Silverlight has no database and no background agent
    ' here, so this is the whole store.
    Public Class DownloadStore
        Private Const Folder As String = "downloads"
        Private Const IndexFile As String = "downloads.json"

        Private Shared _current As DownloadStore
        Public Shared ReadOnly Property Current As DownloadStore
            Get
                If _current Is Nothing Then _current = New DownloadStore()
                Return _current
            End Get
        End Property

        Private ReadOnly _records As New List(Of DownloadRecordDto)()
        Private _loaded As Boolean

        Public Function Records() As List(Of DownloadRecordDto)
            EnsureLoaded()
            Return _records
        End Function

        Public Function Find(itemId As String) As DownloadRecordDto
            If String.IsNullOrEmpty(itemId) Then Return Nothing
            EnsureLoaded()
            For Each record In _records
                If record IsNot Nothing AndAlso record.ItemId = itemId Then Return record
            Next
            Return Nothing
        End Function

        Public Function Has(itemId As String) As Boolean
            Return Find(itemId) IsNot Nothing
        End Function

        Public Function FilePath(record As DownloadRecordDto) As String
            Return Folder & "\" & record.FileName
        End Function

        ' A fresh file for a new download; a re-download replaces the old one.
        Public Function CreateFile(fileName As String) As IsolatedStorageFileStream
            Dim files = Storage()
            EnsureFolder(files)
            Return files.CreateFile(Folder & "\" & fileName)
        End Function

        Public Function OpenFile(record As DownloadRecordDto) As IsolatedStorageFileStream
            Dim files = Storage()
            EnsureFolder(files)
            Return files.OpenFile(Folder & "\" & record.FileName, FileMode.Open, FileAccess.Read)
        End Function

        Public Sub Save(record As DownloadRecordDto)
            If record Is Nothing OrElse String.IsNullOrEmpty(record.ItemId) Then Return
            EnsureLoaded()
            For i = 0 To _records.Count - 1
                If _records(i).ItemId = record.ItemId Then
                    ' The old file may have a different name; do not leave it behind.
                    If _records(i).FileName <> record.FileName Then DeleteFile(_records(i).FileName)
                    _records(i) = record
                    WriteIndex()
                    Return
                End If
            Next
            _records.Add(record)
            WriteIndex()
        End Sub

        Public Sub Remove(itemId As String)
            EnsureLoaded()
            For i = _records.Count - 1 To 0 Step -1
                If _records(i).ItemId = itemId Then
                    DeleteFile(_records(i).FileName)
                    _records.RemoveAt(i)
                End If
            Next
            WriteIndex()
        End Sub

        ' A download that failed half way leaves no partial file behind.
        Public Sub Discard(fileName As String)
            DeleteFile(fileName)
        End Sub

        ' The resume position is written when playback stops, not every second.
        Public Sub UpdatePosition(itemId As String, ticks As Long)
            Dim record = Find(itemId)
            If record Is Nothing Then Return
            record.PositionTicks = ticks
            WriteIndex()
        End Sub

        Public Function TotalBytes() As Long
            Dim total As Long = 0
            For Each record In Records()
                total += record.SizeBytes
            Next
            Return total
        End Function

        Private Sub EnsureLoaded()
            If _loaded Then Return
            _loaded = True
            _records.Clear()
            Try
                Dim files = Storage()
                EnsureFolder(files)
                If Not files.FileExists(IndexFile) Then Return
                Using stream = files.OpenFile(IndexFile, FileMode.Open, FileAccess.Read)
                    Using reader As New StreamReader(stream)
                        Dim text = reader.ReadToEnd()
                        If String.IsNullOrEmpty(text) Then Return
                        Dim loaded = Json.Deserialize(Of List(Of DownloadRecordDto))(text)
                        If loaded IsNot Nothing Then _records.AddRange(loaded)
                    End Using
                End Using
            Catch
                ' A damaged index must not stop the app: the files that remain are
                ' unlisted, and a new download writes a clean document.
            End Try
        End Sub

        Private Sub WriteIndex()
            Try
                Dim files = Storage()
                EnsureFolder(files)
                Using stream = files.CreateFile(IndexFile)
                    Using writer As New StreamWriter(stream)
                        writer.Write(Json.Serialize(Records()))
                    End Using
                End Using
            Catch
                ' Storage full or locked: the video itself is still there.
            End Try
        End Sub

        Private Sub DeleteFile(fileName As String)
            If String.IsNullOrEmpty(fileName) Then Return
            Try
                Dim files = Storage()
                If files.FileExists(Folder & "\" & fileName) Then files.DeleteFile(Folder & "\" & fileName)
            Catch
            End Try
        End Sub

        Private Shared Function Storage() As IsolatedStorageFile
            Return IsolatedStorageFile.GetUserStoreForApplication()
        End Function

        Private Shared Sub EnsureFolder(files As IsolatedStorageFile)
            If Not files.DirectoryExists(Folder) Then files.CreateDirectory(Folder)
        End Sub
    End Class
End Namespace
