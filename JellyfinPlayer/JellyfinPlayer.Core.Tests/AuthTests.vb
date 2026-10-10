<TestClass>
Public Class AuthTests
    Private Function MakeClient(fake As FakeTransport) As JellyfinApiClient
        Return New JellyfinApiClient(fake, New JellyfinUrls("http://host:8096"), "dev1")
    End Function

    <TestMethod>
    Public Sub GetServerInfo_parses()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""ServerName"":""Home"",""Version"":""10.9.10"",""Id"":""abc""}"
        Dim info = MakeClient(fake).GetServerInfoAsync().Result
        Assert.AreEqual("Home", info.ServerName)
    End Sub

    <TestMethod>
    Public Sub Authenticate_sets_token_user_and_device_header()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""User"":{""Id"":""u1"",""Name"":""bob""},""AccessToken"":""T"",""ServerId"":""s1""}"
        Dim client = MakeClient(fake)
        Dim r = client.AuthenticateAsync("bob", "pw").Result
        Assert.AreEqual("T", client.AccessToken)
        Assert.AreEqual("u1", client.UserId)
        Assert.AreEqual("s1", client.ServerId)
        Assert.IsTrue(fake.LastHeaders("X-Emby-Authorization").Contains("DeviceId=""dev1"""), fake.LastHeaders("X-Emby-Authorization"))
        Assert.IsTrue(fake.LastJson.Contains("""Pw"":""pw"""), fake.LastJson)
    End Sub

    <TestMethod>
    Public Sub Authenticate_sends_password_for_old_and_new_servers()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""AccessToken"":""T""}"
        MakeClient(fake).AuthenticateAsync("bob", "pw").Wait()
        Assert.IsTrue(fake.LastJson.Contains("""Pw"":""pw"""), fake.LastJson)
        Assert.IsTrue(fake.LastJson.Contains("""Password"":""pw"""), fake.LastJson)
    End Sub

    <TestMethod>
    Public Sub Authenticate_with_401_throws_401()
        Dim fake = New FakeTransport()
        fake.ErrorToThrow = New JellyfinException("nope", 401)
        Dim client = MakeClient(fake)
        Try
            client.AuthenticateAsync("bob", "x").GetAwaiter().GetResult()
            Assert.Fail("expected JellyfinException")
        Catch ex As JellyfinException
            Assert.AreEqual(401, ex.StatusCode)
        End Try
    End Sub

    <TestMethod>
    Public Sub Transport_maps_timeout()
        Dim mapped = HttpTransport.MapException(New TaskCanceledException())
        Assert.IsTrue(mapped.Message.ToLower().Contains("timeout"), mapped.Message)
    End Sub

    <TestMethod>
    Public Sub Transport_maps_network_error()
        Dim mapped = HttpTransport.MapException(New System.Net.Http.HttpRequestException("boom"))
        Assert.IsTrue(mapped.Message.Contains("Could not reach"), mapped.Message)
    End Sub
End Class
