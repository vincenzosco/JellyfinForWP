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
        Assert.AreEqual("http://host:8096/Items/id1/Images/Primary?maxHeight=300&quality=90&ApiKey=TOK", MakeUrls().PrimaryImage("id1", 300))
    End Sub

    <TestMethod>
    Public Sub Items_url_has_paging_and_key()
        Dim u = MakeUrls().Items("user1", "v1", "Movie", 0, 50, "SortName", "Ascending")
        Assert.IsTrue(u.Contains("Recursive=true"), u)
        Assert.IsTrue(u.Contains("ParentId=v1"), u)
        Assert.IsTrue(u.Contains("StartIndex=0"), u)
        Assert.IsTrue(u.Contains("Limit=50"), u)
        Assert.IsTrue(u.Contains("ApiKey=TOK"), u)
    End Sub

    <TestMethod>
    Public Sub Search_encodes_term()
        Dim u = MakeUrls().Search("user1", "the office")
        Assert.IsTrue(u.Contains("SearchTerm=the%20office"), u)
    End Sub

    <TestMethod>
    Public Sub Device_profile_uses_progressive_mp4()
        Dim dp = DeviceProfileBuilder.DefaultProfile()
        Assert.AreEqual("http", dp.TranscodingProfiles(0).Protocol)
        Assert.AreEqual("mp4", dp.TranscodingProfiles(0).Container)
    End Sub

    <TestMethod>
    Public Sub Image_url_uses_the_parameter_jellyfin_12_accepts()
        ' 12.0 disabled the legacy api_key query parameter along with the X-Emby-*
        ' headers, and an image URL cannot carry an Authorization header.
        Dim u = MakeUrls().PrimaryImage("id1", 300)
        Assert.IsTrue(u.Contains("ApiKey=TOK"), u)
        Assert.IsFalse(u.Contains("api_key="), "the removed api_key parameter must not be sent: " & u)
    End Sub

    <TestMethod>
    Public Sub Device_profile_does_not_claim_mkv()
        ' Windows Phone Silverlight plays MP4/H.264; claiming Matroska would make
        ' the server choose a direct stream the phone cannot open.
        Dim dp = DeviceProfileBuilder.DefaultProfile()
        Dim container = dp.DirectPlayProfiles(0).Container
        Assert.AreEqual("mp4", container)
        Assert.IsFalse(container.Contains("mkv"), container)
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
        Assert.IsFalse(New JellyfinUrls("http://h").PrimaryImage("i", 100).Contains("ApiKey"), "no token should mean no ApiKey")
    End Sub

    <TestMethod>
    Public Sub User_url_carries_the_api_key()
        Dim u = MakeUrls().User("u1")
        Assert.IsTrue(u.Contains("/Users/u1"), u)
        Assert.IsTrue(u.Contains("ApiKey=TOK"), u)
    End Sub
End Class
