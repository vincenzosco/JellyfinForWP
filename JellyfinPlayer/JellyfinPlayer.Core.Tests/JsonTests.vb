<TestClass>
Public Class JsonTests
    <TestMethod>
    Public Sub Deserialize_server_info()
        Dim dto = Json.Deserialize(Of ServerInfoDto)("{""ServerName"":""Home"",""Version"":""10.9.10"",""Id"":""abc""}")
        Assert.AreEqual("Home", dto.ServerName)
        Assert.AreEqual("10.9.10", dto.Version)
        Assert.AreEqual("abc", dto.Id)
    End Sub

    <TestMethod>
    Public Sub Deserialize_query_result_empty()
        Dim q = Json.Deserialize(Of QueryResultDto(Of BaseItemDto))("{""Items"":[],""TotalRecordCount"":0}")
        Assert.IsNotNull(q.Items)
        Assert.AreEqual(0, q.Items.Count)
        Assert.AreEqual(0, q.TotalRecordCount)
    End Sub

    <TestMethod>
    Public Sub Serialize_progress_contains_ticks()
        Dim s = Json.Serialize(New PlaybackProgressDto With {.ItemId = "i", .PositionTicks = 5})
        Assert.IsTrue(s.Contains("""PositionTicks"":5"), s)
    End Sub

    <TestMethod>
    Public Sub Deserialize_ignores_unknown_fields()
        Dim dto = Json.Deserialize(Of ServerInfoDto)("{""ServerName"":""H"",""Whatever"":42}")
        Assert.AreEqual("H", dto.ServerName)
    End Sub

    <TestMethod>
    Public Sub Deserialize_image_tags_object()
        Dim item = Json.Deserialize(Of BaseItemDto)("{""Id"":""x"",""ImageTags"":{""Primary"":""t1"",""Logo"":""l1""}}")
        Assert.AreEqual("t1", item.ImageTags.Primary)
    End Sub
End Class
