Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

' The rules behind the queue, the genre panel, the quality list and the download
' store. They are page-free, so a device is not needed to know they are right.
<TestClass>
Public Class ClientRulesTests
    Private Shared Function Item(id As String) As BaseItemDto
        Return New BaseItemDto With {.Id = id, .Name = id}
    End Function

    <TestMethod>
    Public Sub PlayQueue_moves_to_the_next_entry()
        Dim items As New List(Of BaseItemDto) From {Item("a"), Item("b"), Item("c")}
        Assert.AreEqual(1, PlayQueue.NextIndex(items, 0))
        Assert.AreEqual(2, PlayQueue.NextIndex(items, 1))
        Assert.AreEqual("b", PlayQueue.At(items, 1).Id)
    End Sub

    <TestMethod>
    Public Sub PlayQueue_stops_after_the_last_entry()
        Dim items As New List(Of BaseItemDto) From {Item("a")}
        Assert.AreEqual(-1, PlayQueue.NextIndex(items, 0))
        Assert.AreEqual(-1, PlayQueue.NextIndex(Nothing, 0))
        Assert.IsNull(PlayQueue.At(items, 5))
        Assert.IsNull(PlayQueue.At(Nothing, 0))
    End Sub

    <TestMethod>
    Public Sub GenreChoices_start_with_all_and_skip_broken_rows()
        Dim genres As New List(Of BaseItemDto) From {
            New BaseItemDto With {.Id = "g1", .Name = "Drama"},
            New BaseItemDto With {.Id = "", .Name = "No id"},
            Nothing,
            New BaseItemDto With {.Id = "g2", .Name = Nothing}
        }
        Dim choices = LibraryFilters.GenreChoices(genres, "All genres")
        Assert.AreEqual(3, choices.Count)
        Assert.IsTrue(choices(0).IsAll, "the first row must mean no filter")
        Assert.IsNull(choices(0).Id)
        Assert.AreEqual("Drama", choices(1).Name)
        ' A nameless genre still needs something to show.
        Assert.AreEqual("g2", choices(2).Name)
    End Sub

    <TestMethod>
    Public Sub GenreChoices_work_without_a_genre_list()
        Dim choices = LibraryFilters.GenreChoices(Nothing, "All")
        Assert.AreEqual(1, choices.Count)
        Assert.IsTrue(choices(0).IsAll)
    End Sub

    <TestMethod>
    Public Sub QualityChoices_are_the_stored_bitrates()
        Dim choices = QualityOptions.Choices("Mbps")
        Assert.AreEqual(4, choices.Count)
        Assert.AreEqual(8000000, choices(2).Bitrate)
        Assert.AreEqual("8 Mbps", choices(2).Label)
        Assert.AreEqual(2, QualityOptions.IndexOf(8000000))
        Assert.AreEqual(-1, QualityOptions.IndexOf(123))
    End Sub

    <TestMethod>
    Public Sub Downloads_use_a_safe_file_name()
        Assert.AreEqual("abc-123_x.mp4", Downloads.FileNameFor("abc-123_x"))
        Assert.AreEqual("abcmp4.mp4", Downloads.FileNameFor("a/b c.mp4"))
        ' Nothing usable must still be a file, not a bare extension.
        Assert.AreEqual("download.mp4", Downloads.FileNameFor("../.."))
        Assert.AreEqual("download.mp4", Downloads.FileNameFor(Nothing))
    End Sub

    <TestMethod>
    Public Sub Download_percent_never_leaves_zero_to_a_hundred()
        Assert.AreEqual(0, Downloads.Percent(0, 0))
        Assert.AreEqual(50, Downloads.Percent(5, 10))
        Assert.AreEqual(100, Downloads.Percent(11, 10))
        Assert.AreEqual(0, Downloads.Percent(-1, 10))
    End Sub

    <TestMethod>
    Public Sub Download_size_is_readable()
        Assert.AreEqual("1 MB", Downloads.SizeLabel(0))
        Assert.AreEqual("10 MB", Downloads.SizeLabel(10L * 1024 * 1024))
        Assert.AreEqual("1.5 GB", Downloads.SizeLabel(CType(1.5 * 1024 * 1024 * 1024, Long)))
    End Sub

    <TestMethod>
    Public Sub A_download_record_survives_json()
        Dim records As New List(Of DownloadRecordDto) From {
            New DownloadRecordDto With {.ItemId = "i1", .Name = "Film", .Subtitle = "2020",
                                        .FileName = "i1.mp4", .RunTimeTicks = 100L, .PositionTicks = 50L,
                                        .SizeBytes = 2048, .DownloadedAtTicks = 7L}
        }
        Dim text = Json.Serialize(records)
        Dim copy = Json.Deserialize(Of List(Of DownloadRecordDto))(text)
        Assert.AreEqual(1, copy.Count)
        Assert.AreEqual("Film", copy(0).Name)
        Assert.AreEqual(50L, copy(0).PositionTicks.Value)
        Assert.AreEqual(2048L, copy(0).SizeBytes)
    End Sub
End Class
