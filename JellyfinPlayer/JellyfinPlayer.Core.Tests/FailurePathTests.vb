<TestClass>
Public Class FailurePathTests
    Private Function ClientFor(server As StubHttpServer) As JellyfinApiClient
        Return New JellyfinApiClient(New HttpTransport(), New JellyfinUrls(server.BaseUrl), "test-device")
    End Function

    ' The reported emulator run: a 200 whose body is an HTML page.
    <TestMethod>
    Public Sub Html_page_on_a_successful_response_is_classified_as_not_json()
        Using server As New StubHttpServer("200 OK", "text/html; charset=utf-8", "<!DOCTYPE html><html><body>nginx</body></html>")
            Try
                ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message)
                Assert.IsFalse(ex.Message.Contains("<"), ex.Message)
                Assert.IsTrue(ex.Message.Length < 200, ex.Message)
            End Try
        End Using
    End Sub

    <TestMethod>
    Public Sub Html_page_on_a_failed_response_is_classified_as_an_http_failure()
        Using server As New StubHttpServer("404 Not Found", "text/html", "<html><body>Not Found</body></html>")
            Try
                ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind, ex.Message)
                Assert.AreEqual(404, ex.StatusCode)
                Assert.IsTrue(ex.Message.Contains("HTTP 404"), ex.Message)
                Assert.IsFalse(ex.Message.Contains("<"), ex.Message)
            End Try
        End Using
    End Sub

    <TestMethod>
    Public Sub Json_error_detail_is_kept_short()
        Using server As New StubHttpServer("500 Internal Server Error", "application/json", "{""Message"":""disk full""}")
            Try
                ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind, ex.Message)
                Assert.IsTrue(ex.Message.Contains("500"), ex.Message)
                Assert.IsTrue(ex.Message.Contains("disk full"), ex.Message)
            End Try
        End Using
    End Sub

    ' A 200 that is JSON still works: the guard must not break the happy path.
    <TestMethod>
    Public Sub Json_on_a_successful_response_is_parsed()
        Using server As New StubHttpServer("200 OK", "application/json", "{""ServerName"":""Home"",""Id"":""abc""}")
            Dim info = ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
            Assert.AreEqual("Home", info.ServerName)
            Assert.AreEqual("abc", info.Id)
        End Using
    End Sub

    <TestMethod>
    Public Sub Closed_port_is_classified_as_unreachable()
        Try
            Dim client As New JellyfinApiClient(New HttpTransport(), New JellyfinUrls("http://127.0.0.1:9"), "test-device")
            client.GetServerInfoAsync().GetAwaiter().GetResult()
            Assert.Fail("expected a JellyfinException")
        Catch ex As JellyfinException
            Assert.AreEqual(JellyfinErrorKind.Unreachable, ex.Kind, ex.Message)
        End Try
    End Sub
End Class
