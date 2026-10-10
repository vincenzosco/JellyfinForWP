<TestClass>
Public Class PlaceholderTests
    <TestMethod>
    Public Sub Add_sums_two_integers()
        Assert.AreEqual(5, JellyfinPlayer.Shared.Placeholder.Add(2, 3))
    End Sub
End Class
