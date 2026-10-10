<TestClass>
Public Class ServerProbeTests
    Private Shared Function Candidate(address As String) As DiscoveredServerDto
        Return New DiscoveredServerDto With {.Address = address, .Id = "id-" & address, .Name = "Home"}
    End Function

    Private Shared Function Prober(clients As IDictionary(Of String, FakeTransport)) As Func(Of String, JellyfinApiClient)
        Return Function(baseUrl) New JellyfinApiClient(clients(baseUrl), New JellyfinUrls(baseUrl), "dev")
    End Function

    ' A web page at the address is not a Jellyfin server: skip it.
    <TestMethod>
    Public Sub An_address_that_answers_with_html_is_not_a_server()
        Dim probe As New FakeTransport()
        probe.Handler = Function(url) "<html>nginx</html>"
        Dim clients = New Dictionary(Of String, FakeTransport)() From {{"http://127.0.0.1:9", probe}}

        Dim found = ServerProbe.FindFirstJellyfinAsync({Candidate("http://127.0.0.1:9")}, Prober(clients)).GetAwaiter().GetResult()

        Assert.IsNull(found, "an HTML answer must not validate as Jellyfin")
        Assert.IsTrue(probe.LastUrl.Contains("/System/Info/Public"), probe.LastUrl)
    End Sub

    <TestMethod>
    Public Sub Returns_the_first_validated_candidate()
        Dim bad As New FakeTransport()
        bad.Handler = Function(url) "<html>nginx</html>"
        Dim good As New FakeTransport()
        good.Handler = Function(url) "{""Id"":""abc"",""ServerName"":""Home""}"
        Dim clients = New Dictionary(Of String, FakeTransport)() From {
            {"http://192.168.0.50:8096", bad}, {"http://192.168.0.108:8096", good}}

        Dim found = ServerProbe.FindFirstJellyfinAsync(
            {Candidate("http://192.168.0.50:8096"), Candidate("http://192.168.0.108:8096")}, Prober(clients)).GetAwaiter().GetResult()

        Assert.IsNotNull(found)
        Assert.AreEqual("http://192.168.0.108:8096", found.Address)
    End Sub

    ' Server info without an Id is not a Jellyfin server reply.
    <TestMethod>
    Public Sub An_answer_without_an_id_is_not_a_server()
        Dim anonymous As New FakeTransport()
        anonymous.Handler = Function(url) "{""ServerName"":""Router""}"
        Dim clients = New Dictionary(Of String, FakeTransport)() From {{"http://192.168.0.50:8096", anonymous}}

        Dim found = ServerProbe.FindFirstJellyfinAsync({Candidate("http://192.168.0.50:8096")}, Prober(clients)).GetAwaiter().GetResult()

        Assert.IsNull(found)
    End Sub

    <TestMethod>
    Public Sub A_failing_candidate_does_not_stop_the_probe()
        Dim dead As New FakeTransport()
        dead.ErrorToThrow = New JellyfinException("Could not reach the server", 0, JellyfinErrorKind.Unreachable)
        Dim good As New FakeTransport()
        good.Handler = Function(url) "{""Id"":""abc""}"
        Dim clients = New Dictionary(Of String, FakeTransport)() From {
            {"http://192.168.0.50:8096", dead}, {"http://192.168.0.108:8096", good}}

        Dim found = ServerProbe.FindFirstJellyfinAsync(
            {Candidate("http://192.168.0.50:8096"), Candidate("http://192.168.0.108:8096")}, Prober(clients)).GetAwaiter().GetResult()

        Assert.IsNotNull(found)
        Assert.AreEqual("http://192.168.0.108:8096", found.Address)
    End Sub

    <TestMethod>
    Public Sub No_candidate_means_no_server()
        Dim clients = New Dictionary(Of String, FakeTransport)()
        Assert.IsNull(ServerProbe.FindFirstJellyfinAsync(New List(Of DiscoveredServerDto)(), Prober(clients)).GetAwaiter().GetResult())
    End Sub
End Class
