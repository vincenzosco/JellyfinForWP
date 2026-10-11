<TestClass>
Public Class AuthHeaderTests
    <TestMethod>
    Public Sub Identify_names_the_client_before_a_token_exists()
        Assert.AreEqual("MediaBrowser Client=""JellyfinPlayer"", Device=""WindowsPhone"", DeviceId=""dev1"", Version=""1.0.0""",
                        JellyfinAuthHeader.Identify("dev1"))
    End Sub

    <TestMethod>
    Public Sub WithToken_carries_the_token_along_with_the_identification()
        Assert.AreEqual("MediaBrowser Token=""TOK"", Client=""JellyfinPlayer"", Device=""WindowsPhone"", DeviceId=""dev1"", Version=""1.0.0""",
                        JellyfinAuthHeader.WithToken("TOK", "dev1"))
    End Sub

    ' Jellyfin 12 turns the legacy X-Emby-Authorization / X-Emby-Token headers off by
    ' default, so both values must identify the client through the MediaBrowser scheme,
    ' which that setting never gates.
    <TestMethod>
    Public Sub Both_forms_use_the_MediaBrowser_scheme()
        Assert.IsTrue(JellyfinAuthHeader.Identify("d").StartsWith("MediaBrowser "), JellyfinAuthHeader.Identify("d"))
        Assert.IsTrue(JellyfinAuthHeader.WithToken("t", "d").StartsWith("MediaBrowser "), JellyfinAuthHeader.WithToken("t", "d"))
    End Sub

    <TestMethod>
    Public Sub Every_authenticated_call_sends_the_standard_header_and_never_the_legacy_one()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{}"
        Dim client = New JellyfinApiClient(fake, New JellyfinUrls("http://host:8096", "T"), "dev1")
        client.AccessToken = "T"
        client.UserId = "u1"

        client.GetViewsAsync("u1").Wait()
        Dim read = fake.LastHeaders
        Assert.IsTrue(read("Authorization").Contains("Token=""T"""), read("Authorization"))
        Assert.IsFalse(read.ContainsKey("X-Emby-Token"), "GET must not carry the legacy token header")

        client.SetFavoriteAsync("u1", "i1", True).Wait()
        Dim write = fake.LastHeaders
        Assert.IsTrue(write("Authorization").Contains("Token=""T"""), write("Authorization"))
        Assert.IsFalse(write.ContainsKey("X-Emby-Token"), "POST must not carry the legacy token header")
    End Sub
End Class
