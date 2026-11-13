Imports System
Imports System.IO
Imports System.Net
Imports System.Threading.Tasks
Imports JellyfinPlayer.Shared

Namespace Services
    ' Copies one video into isolated storage. Only the progressive MP4 the server
    ' produces is downloaded: the original file may be a container this phone
    ' cannot open, and a download the app cannot play would be dead weight.
    Public Module DownloadService
        ' report is called from a background thread; the page marshals it to the
        ' UI thread before touching a control.
        Public Async Function DownloadAsync(item As BaseItemDto, report As Action(Of Integer)) As Task(Of DownloadRecordDto)
            If item Is Nothing OrElse String.IsNullOrEmpty(item.Id) Then Return Nothing
            Dim session = AppSession.Current
            Dim url = Await TranscodeUrlAsync(item)
            If String.IsNullOrEmpty(url) Then Throw New JellyfinException(Localized.Text("PlaybackNoSource"), 0)

            Dim fileName = Downloads.FileNameFor(item.Id)
            Dim store = DownloadStore.Current
            Dim total As Long = 0
            Dim received As Long = 0
            Try
                Using response = Await GetResponseAsync(url, session.AccessToken)
                    total = response.ContentLength
                    ' A download that cannot fit is better refused before it starts.
                    If total > 0 AndAlso total > FreeSpace() Then Throw New JellyfinException(Localized.Text("DownloadNoSpace"), 0)
                    Using input = response.GetResponseStream()
                        Using output = store.CreateFile(fileName)
                            ' Reading a network stream blocks; doing it here would
                            ' freeze the page for the length of the film.
                            Await Task.Factory.StartNew(Sub() Copy(input, output, total, report))
                            received = output.Length
                        End Using
                    End Using
                End Using
            Catch
                store.Discard(fileName)
                Throw
            End Try

            Dim record As New DownloadRecordDto With {
                .ItemId = item.Id,
                .Name = item.Name,
                .Subtitle = SubtitleFor(item),
                .FileName = fileName,
                .RunTimeTicks = item.RunTimeTicks,
                .PositionTicks = 0L,
                .SizeBytes = If(received > 0, received, total),
                .DownloadedAtTicks = DateTime.UtcNow.Ticks
            }
            store.Save(record)
            report(100)
            Return record
        End Function

        ' The same source selection the player uses, narrowed to transcodable
        ' sources because only that URL is a plain MP4.
        Private Async Function TranscodeUrlAsync(item As BaseItemDto) As Task(Of String)
            Dim session = AppSession.Current
            Dim info = Await session.CreateClient().GetPlaybackInfoAsync(session.UserId, item.Id, 0, session.PreferredBitrate)
            If info Is Nothing OrElse info.MediaSources Is Nothing Then Return Nothing
            For Each source In info.MediaSources
                If source IsNot Nothing AndAlso source.SupportsTranscoding Then
                    Return PlaybackUrlResolver.TranscodeUrl(session.BaseUrl, session.AccessToken, item.Id, source, session.PreferredBitrate)
                End If
            Next
            Return Nothing
        End Function

        Private Sub Copy(input As Stream, output As Stream, total As Long, report As Action(Of Integer))
            Dim buffer(8192) As Byte
            Dim received As Long = 0
            Dim lastPercent As Integer = -1
            Dim read = input.Read(buffer, 0, buffer.Length)
            While read > 0
                output.Write(buffer, 0, read)
                received += read
                Dim percent = Downloads.Percent(received, total)
                If percent <> lastPercent AndAlso report IsNot Nothing Then
                    lastPercent = percent
                    report(percent)
                End If
                read = input.Read(buffer, 0, buffer.Length)
            End While
        End Sub

        Private Function GetResponseAsync(url As String, token As String) As Task(Of HttpWebResponse)
            Dim completion As New TaskCompletionSource(Of HttpWebResponse)()
            Try
                Dim request = DirectCast(WebRequest.Create(url), HttpWebRequest)
                ' A film is not a 15 second API call: no buffering, no timeout.
                request.AllowReadStreamBuffering = False
                If Not String.IsNullOrEmpty(token) Then
                    request.Headers("Authorization") = JellyfinAuthHeader.WithToken(token, AppSession.Current.DeviceId)
                End If
                request.BeginGetResponse(
                    Sub(result)
                        Try
                            completion.TrySetResult(DirectCast(request.EndGetResponse(result), HttpWebResponse))
                        Catch ex As Exception
                            completion.TrySetException(ex)
                        End Try
                    End Sub, Nothing)
            Catch ex As Exception
                completion.TrySetException(ex)
            End Try
            Return completion.Task
        End Function

        Private Function FreeSpace() As Long
            Try
                Return System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication().AvailableFreeSpace
            Catch
                Return Long.MaxValue
            End Try
        End Function

        Private Function SubtitleFor(item As BaseItemDto) As String
            If String.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase) Then
                Dim number As String = ""
                If item.ParentIndexNumber.HasValue Then number &= "S" & item.ParentIndexNumber.Value.ToString("00")
                If item.IndexNumber.HasValue Then number &= "E" & item.IndexNumber.Value.ToString("00")
                If Not String.IsNullOrEmpty(item.SeriesName) Then Return item.SeriesName & " " & number
                Return number
            End If
            If item.ProductionYear.HasValue Then Return item.ProductionYear.Value.ToString()
            Return ""
        End Function
    End Module
End Namespace
