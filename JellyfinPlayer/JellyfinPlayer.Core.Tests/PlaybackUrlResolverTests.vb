<TestClass>
Public Class PlaybackUrlResolverTests
    Private Function Source() As MediaSourceInfoDto
        Return New MediaSourceInfoDto With {.Id = "m1", .Container = "mp4"}
    End Function

    <TestMethod>
    Public Sub Uses_direct_stream_url_when_present()
        Dim s = Source()
        s.DirectStreamUrl = "/Videos/i1/stream?static=true"
        Dim r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.IsFalse(r.IsTranscode)
        Assert.IsTrue(r.Url.Contains("static=true"), r.Url)
        Assert.IsTrue(r.Url.Contains("api_key=TOK"), r.Url)
    End Sub

    <TestMethod>
    Public Sub Uses_transcoding_url_when_present()
        Dim s = Source()
        s.TranscodingUrl = "/Videos/i1/stream.mp4?x=1"
        Dim r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.IsTrue(r.IsTranscode)
        Assert.IsTrue(r.Url.Contains("api_key=TOK"), r.Url)
    End Sub

    <TestMethod>
    Public Sub Builds_static_stream_when_only_direct_flags_are_returned()
        Dim s = Source()
        s.SupportsDirectPlay = True
        s.SupportsDirectStream = True
        Dim r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.IsFalse(r.IsTranscode)
        Assert.AreEqual("http://h:8096/Videos/i1/stream?static=true&mediaSourceId=m1&api_key=TOK", r.Url)
    End Sub

    <TestMethod>
    Public Sub Builds_progressive_mp4_when_only_transcoding_is_supported()
        Dim s = Source()
        s.SupportsTranscoding = True
        Dim r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.IsTrue(r.IsTranscode)
        Assert.IsTrue(r.Url.Contains("/Videos/i1/stream.mp4"), r.Url)
        Assert.IsTrue(r.Url.Contains("MaxStreamingBitrate=8000000"), r.Url)
        Assert.IsTrue(r.Url.Contains("api_key=TOK"), r.Url)
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_when_no_source_is_playable()
        Dim s = Source()
        Assert.IsNull(PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000))
    End Sub

    ' The two below build the URL a failed stream falls back to, so both are tried
    ' only after the player has already failed to play the other one.

    <TestMethod>
    Public Sub Transcode_url_prefers_the_one_the_server_gave()
        Dim s = Source()
        s.TranscodingUrl = "/Videos/i1/stream.mp4?DeviceProfileId=abc"
        Dim u = PlaybackUrlResolver.TranscodeUrl("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.IsTrue(u.Contains("DeviceProfileId=abc"), u)
        Assert.IsTrue(u.Contains("api_key=TOK"), u)
    End Sub

    <TestMethod>
    Public Sub Transcode_url_is_built_when_the_server_gave_none()
        Dim u = PlaybackUrlResolver.TranscodeUrl("http://h:8096", "TOK", "i1", Source(), 2000000)
        Assert.IsTrue(u.Contains("/Videos/i1/stream.mp4"), u)
        Assert.IsTrue(u.Contains("MaxStreamingBitrate=2000000"), u)
        Assert.IsTrue(u.Contains("api_key=TOK"), u)
    End Sub

    <TestMethod>
    Public Sub Direct_url_is_nothing_when_the_source_cannot_be_played_directly()
        Assert.IsNull(PlaybackUrlResolver.DirectUrl("http://h:8096", "TOK", "i1", Source()))
    End Sub

    <TestMethod>
    Public Sub Direct_url_builds_the_static_stream_when_the_flags_allow_it()
        Dim s = Source()
        s.SupportsDirectStream = True
        Assert.AreEqual("http://h:8096/Videos/i1/stream?static=true&mediaSourceId=m1&api_key=TOK",
                        PlaybackUrlResolver.DirectUrl("http://h:8096", "TOK", "i1", s))
    End Sub

    <TestMethod>
    Public Sub Does_not_duplicate_api_key()
        Dim s = Source()
        s.DirectStreamUrl = "/Videos/i1/stream?static=true&api_key=TOK"
        Dim r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.AreEqual(1, Split(r.Url, "api_key=").Length - 1, r.Url)
    End Sub
End Class
