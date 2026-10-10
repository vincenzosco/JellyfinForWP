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

    <TestMethod>
    Public Sub Does_not_duplicate_api_key()
        Dim s = Source()
        s.DirectStreamUrl = "/Videos/i1/stream?static=true&api_key=TOK"
        Dim r = PlaybackUrlResolver.Resolve("http://h:8096", "TOK", "i1", s, 8000000)
        Assert.AreEqual(1, Split(r.Url, "api_key=").Length - 1, r.Url)
    End Sub
End Class
