Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

<TestClass>
Public Class TrackOptionsTests
    Private Function Streams() As List(Of MediaStreamDto)
        Return New List(Of MediaStreamDto) From {
            New MediaStreamDto With {.Index = 0, .Type = "Video", .Codec = "h264"},
            New MediaStreamDto With {.Index = 1, .Type = "Audio", .Language = "eng", .Codec = "aac", .IsDefault = False},
            New MediaStreamDto With {.Index = 2, .Type = "Audio", .DisplayTitle = "Italiano - AAC stereo", .Codec = "aac", .IsDefault = True},
            New MediaStreamDto With {.Index = 3, .Type = "Subtitle", .DisplayTitle = "English", .Codec = "subrip", .IsExternal = True}
        }
    End Function

    <TestMethod>
    Public Sub Audio_lists_only_audio_streams_in_server_order()
        Dim options = TrackOptions.Audio(Streams())
        Assert.AreEqual(2, options.Count)
        Assert.AreEqual(1, options(0).Index.Value)
        Assert.AreEqual(2, options(1).Index.Value)
    End Sub

    <TestMethod>
    Public Sub Subtitles_put_off_first_then_every_subtitle()
        Dim options = TrackOptions.Subtitles(Streams(), "Off")
        Assert.AreEqual(2, options.Count)
        Assert.IsFalse(options(0).Index.HasValue, "the first row is the explicit off")
        Assert.AreEqual("Off", options(0).Label)
        Assert.AreEqual(3, options(1).Index.Value)
        Assert.AreEqual("English", options(1).Label)
    End Sub

    <TestMethod>
    Public Sub A_stream_without_subtitles_still_offers_off()
        Assert.AreEqual(1, TrackOptions.Subtitles(Nothing, "Off").Count)
        Assert.IsFalse(TrackOptions.Subtitles(Nothing, "Off")(0).Index.HasValue)
    End Sub

    <TestMethod>
    Public Sub The_default_audio_index_is_the_flagged_one_or_the_first()
        Assert.AreEqual(2, TrackOptions.DefaultAudioIndex(Streams()).Value)
        Dim noDefault As New List(Of MediaStreamDto) From {
            New MediaStreamDto With {.Index = 4, .Type = "Audio"}
        }
        Assert.AreEqual(4, TrackOptions.DefaultAudioIndex(noDefault).Value)
        Assert.IsFalse(TrackOptions.DefaultAudioIndex(Nothing).HasValue)
    End Sub

    <TestMethod>
    Public Sub The_default_audio_is_sent_as_nothing_so_it_can_direct_play()
        Assert.IsFalse(TrackOptions.AudioSelection(Streams(), 2).HasValue)
        Assert.AreEqual(1, TrackOptions.AudioSelection(Streams(), 1).Value)
        Assert.IsFalse(TrackOptions.AudioSelection(Streams(), Nothing).HasValue)
    End Sub

    <TestMethod>
    Public Sub A_label_falls_back_from_display_title_to_language_and_codec()
        Assert.AreEqual("Italiano - AAC stereo", TrackOptions.Describe(Streams()(2)))
        Assert.AreEqual("eng - AAC", TrackOptions.Describe(Streams()(1)))
        Assert.AreEqual("Track 7", TrackOptions.Describe(New MediaStreamDto With {.Index = 7}))
        Assert.AreEqual("", TrackOptions.Describe(Nothing))
    End Sub
End Class
