<TestClass>
Public Class ResponseFailuresTests
    <TestMethod>
    Public Sub Html_bodies_are_detected()
        Assert.IsTrue(ResponseFailures.LooksLikeHtml("<!DOCTYPE html><html></html>"))
        Assert.IsTrue(ResponseFailures.LooksLikeHtml("  " & vbCrLf & "<html><body>nginx</body></html>"))
        Assert.IsTrue(ResponseFailures.LooksLikeHtml("<h1>404</h1>"))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml(""))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("  "))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml(Nothing))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("{}"))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("[1,2]"))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("null"))
    End Sub

    <TestMethod>
    Public Sub DescribeHttpFailure_does_not_include_the_html_page()
        Dim message = ResponseFailures.DescribeHttpFailure(500, "text/html; charset=utf-8",
            "<html><head><title>500 Internal Server Error</title></head><body>nginx/1.2</body></html>")
        Assert.AreEqual("The server answered HTTP 500. It returned a web page instead of the Jellyfin API - check the address and the port.", message)
        Assert.IsFalse(message.Contains("<"), message)
    End Sub

    <TestMethod>
    Public Sub DescribeHttpFailure_truncates_a_long_json_body()
        Dim message = ResponseFailures.DescribeHttpFailure(400, "application/json", New String("x"c, 500))
        Assert.IsTrue(message.StartsWith("The server answered HTTP 400."), message)
        Assert.IsTrue(message.Length < 200, message)
        Assert.IsTrue(message.EndsWith("..."), message)
    End Sub

    <TestMethod>
    Public Sub DescribeHttpFailure_collapses_whitespace_in_the_detail()
        Dim message = ResponseFailures.DescribeHttpFailure(503, "application/json", "{ ""Message"" : " & vbCrLf & " ""disk full"" }")
        Assert.AreEqual("The server answered HTTP 503. { ""Message"" : ""disk full"" }", message)
    End Sub

    <TestMethod>
    Public Sub Parse_throws_a_classified_error_for_html()
        Try
            ResponseFailures.Parse(Of ServerInfoDto)("<!DOCTYPE html><html>hi</html>")
            Assert.Fail("expected a JellyfinException")
        Catch ex As JellyfinException
            Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message)
            Assert.IsFalse(ex.Message.Contains("<"), ex.Message)
            Assert.IsTrue(ex.Message.Length < 200, ex.Message)
        End Try
    End Sub

    <TestMethod>
    Public Sub Parse_throws_a_classified_error_for_broken_json()
        Try
            ResponseFailures.Parse(Of ServerInfoDto)("{""ServerName"":")
            Assert.Fail("expected a JellyfinException")
        Catch ex As JellyfinException
            Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message)
        End Try
    End Sub

    <TestMethod>
    Public Sub Parse_returns_the_object_for_json()
        Dim info = ResponseFailures.Parse(Of ServerInfoDto)("{""ServerName"":""Home"",""Id"":""abc""}")
        Assert.AreEqual("Home", info.ServerName)
        Assert.AreEqual("abc", info.Id)
    End Sub

    <TestMethod>
    Public Sub MapException_classifies_timeout_unreachable_and_unknown()
        Assert.AreEqual(JellyfinErrorKind.Timeout, HttpTransport.MapException(New TaskCanceledException()).Kind)
        Assert.AreEqual(JellyfinErrorKind.Unreachable, HttpTransport.MapException(New System.Net.Http.HttpRequestException("dns")).Kind)
        Assert.AreEqual(JellyfinErrorKind.Unknown, HttpTransport.MapException(New InvalidOperationException("?")).Kind)
        Assert.AreEqual(JellyfinErrorKind.HttpFailure, HttpTransport.MapException(New JellyfinException("x", 404, JellyfinErrorKind.HttpFailure)).Kind,
            "an already classified exception is passed through")
    End Sub
End Class
