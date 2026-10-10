<TestClass>
Public Class UrlsTests
    Private Function MakeUrls() As JellyfinUrls
        Return New JellyfinUrls("http://host:8096/", "TOK")
    End Function

    <TestMethod>
    Public Sub Base_url_is_normalized()
        Assert.AreEqual("http://host:8096", MakeUrls().BaseUrl)
    End Sub

    <TestMethod>
    Public Sub System_info_public_url()
        Assert.AreEqual("http://host:8096/System/Info/Public", MakeUrls().SystemInfoPublic())
    End Sub

    <TestMethod>
    Public Sub Primary_image_url_has_api_key()
        Assert.AreEqual("http://host:8096/Items/id1/Images/Primary?maxHeight=300&quality=90&api_key=TOK", MakeUrls().PrimaryImage("id1", 300))
    End Sub

    <TestMethod>
    Public Sub Items_url_has_paging_and_key()
        Dim u = MakeUrls().Items("user1", "v1", "Movie", 0, 50, "SortName", "Ascending")
        Assert.IsTrue(u.Contains("Recursive=true"), u)
        Assert.IsTrue(u.Contains("ParentId=v1"), u)
        Assert.IsTrue(u.Contains("StartIndex=0"), u)
        Assert.IsTrue(u.Contains("Limit=50"), u)
        Assert.IsTrue(u.Contains("api_key=TOK"), u)
    End Sub

    <TestMethod>
    Public Sub Search_encodes_term()
        Dim u = MakeUrls().Search("user1", "the office")
        Assert.IsTrue(u.Contains("SearchTerm=the%20office"), u)
    End Sub

    <TestMethod>
    Public Sub Device_profile_uses_hls()
        Dim dp = DeviceProfileBuilder.DefaultProfile()
        Assert.AreEqual("hls", dp.TranscodingProfiles(0).Protocol)
        Assert.AreEqual("ts", dp.TranscodingProfiles(0).Container)
    End Sub

    <TestMethod>
    Public Sub Playback_request_carries_user_and_profile()
        Dim r = DeviceProfileBuilder.ToRequest("u1", 0L)
        Assert.AreEqual("u1", r.UserId)
        Assert.AreEqual(8000000, r.MaxStreamingBitrate)
        Assert.IsTrue(r.AutoOpenLiveStream)
        Assert.IsNotNull(r.DeviceProfile)
    End Sub

    <TestMethod>
    Public Sub Missing_token_omits_api_key()
        Assert.IsFalse(New JellyfinUrls("http://h").PrimaryImage("i", 100).Contains("api_key"), "no token should mean no api_key")
    End Sub
End Class
