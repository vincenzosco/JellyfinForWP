<TestClass>
Public Class ServerDiscoveryParserTests
    <TestMethod>
    Public Sub Parses_a_valid_response()
        Dim server As DiscoveredServerDto = Nothing
        Assert.IsTrue(ServerDiscoveryParser.TryParse("{""Address"":""http://192.168.0.108:8096"",""Id"":""abc"",""Name"":""Home""}", server))
        Assert.IsNotNull(server)
        Assert.AreEqual("http://192.168.0.108:8096", server.Address)
        Assert.AreEqual("abc", server.Id)
        Assert.AreEqual("Home", server.Name)
    End Sub

    <TestMethod>
    Public Sub Ignores_unknown_keys()
        Dim server As DiscoveredServerDto = Nothing
        Assert.IsTrue(ServerDiscoveryParser.TryParse("{""Address"":""http://h:8096"",""Id"":""i"",""Name"":""n"",""Extra"":1}", server))
        Assert.AreEqual("http://h:8096", server.Address)
    End Sub

    <TestMethod>
    Public Sub Rejects_a_response_without_an_address()
        Dim server As DiscoveredServerDto = Nothing
        Assert.IsFalse(ServerDiscoveryParser.TryParse("{""Id"":""i"",""Name"":""n""}", server))
        Assert.IsNull(server)
    End Sub

    <TestMethod>
    Public Sub Rejects_malformed_json_without_throwing()
        Dim server As DiscoveredServerDto = Nothing
        Assert.IsFalse(ServerDiscoveryParser.TryParse("not json", server))
        Assert.IsNull(server)
    End Sub

    <TestMethod>
    Public Sub Rejects_empty_input()
        Dim server As DiscoveredServerDto = Nothing
        Assert.IsFalse(ServerDiscoveryParser.TryParse("", server))
        Assert.IsNull(server)
    End Sub

    <TestMethod>
    Public Sub Exposes_the_udp_probe_constants()
        Assert.AreEqual(7359, ServerDiscoveryParser.DiscoveryPort)
        Assert.AreEqual("who is JellyfinServer?", ServerDiscoveryParser.ProbePayload)
    End Sub
End Class
