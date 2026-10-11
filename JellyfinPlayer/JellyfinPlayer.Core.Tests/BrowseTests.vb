<TestClass>
Public Class BrowseTests
    Private Function MakeClient(fake As FakeTransport) As JellyfinApiClient
        Dim c = New JellyfinApiClient(fake, New JellyfinUrls("http://host:8096", "T"), "d")
        c.AccessToken = "T"
        c.UserId = "user1"
        Return c
    End Function

    <TestMethod>
    Public Sub GetViews_parses_items_and_collection_type()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Items"":[{""Id"":""v1"",""Name"":""Movies"",""CollectionType"":""movies""}],""TotalRecordCount"":1}"
        Dim views = MakeClient(fake).GetViewsAsync("user1").Result
        Assert.AreEqual(1, views.Count)
        Assert.AreEqual("movies", views(0).CollectionType)
    End Sub

    <TestMethod>
    Public Sub GetItems_passes_parent_and_paging()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Items"":[],""TotalRecordCount"":0}"
        MakeClient(fake).GetItemsAsync("user1", "v1", "Movie", 100, 50).Wait()
        Dim url = fake.LastUrl
        Assert.IsTrue(url.Contains("ParentId=v1"), url)
        Assert.IsTrue(url.Contains("StartIndex=100"), url)
        Assert.IsTrue(url.Contains("Limit=50"), url)
        Assert.IsTrue(url.Contains("Recursive=true"), url)
    End Sub

    <TestMethod>
    Public Sub Search_encodes_term()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Items"":[],""TotalRecordCount"":0}"
        MakeClient(fake).SearchAsync("user1", "the office").Wait()
        Assert.IsTrue(fake.LastUrl.Contains("SearchTerm=the%20office"), fake.LastUrl)
    End Sub

    <TestMethod>
    Public Sub GetItem_tolerates_missing_optional_fields()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Id"":""i1"",""Name"":""Solo""}"
        Dim item = MakeClient(fake).GetItemAsync("user1", "i1").Result
        Assert.AreEqual("Solo", item.Name)
        Assert.IsNull(item.RunTimeTicks)
    End Sub

    <TestMethod>
    Public Sub Token_is_sent_in_the_standard_authorization_header()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Items"":[],""TotalRecordCount"":0}"
        MakeClient(fake).GetViewsAsync("user1").Wait()
        Assert.IsTrue(fake.LastHeaders("Authorization").Contains("Token=""T"""), fake.LastHeaders("Authorization"))
        Assert.IsFalse(fake.LastHeaders.ContainsKey("X-Emby-Token"), "the legacy header must not be sent: Jellyfin 12 rejects it")
    End Sub

    <TestMethod>
    Public Sub GetResume_returns_list()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Items"":[{""Id"":""r1"",""Name"":""Resume me""}],""TotalRecordCount"":1}"
        Dim list = MakeClient(fake).GetResumeAsync("user1", 10).Result
        Assert.AreEqual(1, list.Count)
        Assert.AreEqual("Resume me", list(0).Name)
    End Sub
End Class
