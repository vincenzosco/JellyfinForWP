<TestClass>
Public Class PlaybackApiTests
    Private Function MakeClient(fake As FakeTransport) As JellyfinApiClient
        Dim c = New JellyfinApiClient(fake, New JellyfinUrls("http://host:8096", "T"), "d")
        c.AccessToken = "T"
        Return c
    End Function

    <TestMethod>
    Public Sub GetPlaybackInfo_posts_device_profile_and_returns_source()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""MediaSources"":[{""Id"":""m1"",""TranscodingUrl"":""http://h/x.m3u8"",""Container"":""ts""}],""PlaySessionId"":""ps1""}"
        Dim r = MakeClient(fake).GetPlaybackInfoAsync("u1", "i1", 0L).Result
        Assert.AreEqual("http://h/x.m3u8", r.MediaSources(0).TranscodingUrl)
        Assert.AreEqual("ps1", r.PlaySessionId)
        Assert.IsTrue(fake.LastJson.Contains("""Protocol"":""http"""), fake.LastJson)
    End Sub

    <TestMethod>
    Public Sub SetFavorite_true_uses_POST_and_false_uses_DELETE()
        Dim fake = New FakeTransport()
        Dim c = MakeClient(fake)
        c.SetFavoriteAsync("u1", "i1", True).Wait()
        Assert.AreEqual("POST", fake.LastMethod)
        c.SetFavoriteAsync("u1", "i1", False).Wait()
        Assert.AreEqual("DELETE", fake.LastMethod)
    End Sub

    <TestMethod>
    Public Sub SetPlayed_uses_POST_and_DELETE()
        Dim fake = New FakeTransport()
        Dim c = MakeClient(fake)
        c.SetPlayedAsync("u1", "i1", True).Wait()
        Assert.AreEqual("POST", fake.LastMethod)
        c.SetPlayedAsync("u1", "i1", False).Wait()
        Assert.AreEqual("DELETE", fake.LastMethod)
    End Sub

    <TestMethod>
    Public Sub ReportProgress_sends_position_ticks()
        Dim fake = New FakeTransport()
        MakeClient(fake).ReportPlaybackProgressAsync(New PlaybackProgressDto With {.ItemId = "i1", .PositionTicks = 30000000L}).Wait()
        Assert.IsTrue(fake.LastJson.Contains("""PositionTicks"":30000000"), fake.LastJson)
    End Sub
End Class
