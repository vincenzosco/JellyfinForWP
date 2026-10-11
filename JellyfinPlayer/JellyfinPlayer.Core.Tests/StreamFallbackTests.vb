<TestClass>
Public Class StreamFallbackTests
    Private Shared Function U(url As String) As Uri
        Return New Uri(url)
    End Function

    <TestMethod>
    Public Sub Returns_the_fallback_when_it_is_a_different_url()
        Dim alternative = StreamFallback.Alternative(U("http://h/Videos/i1/stream?static=true"), U("http://h/Videos/i1/stream.mp4"), False)
        Assert.IsNotNull(alternative)
        Assert.AreEqual("http://h/Videos/i1/stream.mp4", alternative.ToString())
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_when_the_fallback_was_already_used()
        ' A second failure has to reach the user: two broken URLs must not ping-pong.
        Assert.IsNull(StreamFallback.Alternative(U("http://h/direct"), U("http://h/other.mp4"), True))
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_when_there_is_no_fallback()
        Assert.IsNull(StreamFallback.Alternative(U("http://h/direct"), Nothing, False))
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_when_the_fallback_is_the_url_that_just_failed()
        Assert.IsNull(StreamFallback.Alternative(U("http://h/direct"), U("http://h/direct"), False))
    End Sub

    <TestMethod>
    Public Sub Returns_nothing_when_there_is_no_current_url()
        Assert.IsNull(StreamFallback.Alternative(Nothing, U("http://h/other.mp4"), False))
    End Sub
End Class
