<TestClass>
Public Class TicksTests
    <TestMethod>
    Public Sub ToTimeSpan_one_second()
        Assert.AreEqual(TimeSpan.FromSeconds(1), Ticks.ToTimeSpan(10000000))
    End Sub

    <TestMethod>
    Public Sub Round_trip_preserves_ticks()
        Assert.AreEqual(12345L, Ticks.FromTimeSpan(Ticks.ToTimeSpan(12345L)))
    End Sub

    <TestMethod>
    Public Sub Format_null_is_empty()
        Assert.AreEqual("", Ticks.Format(Nothing))
    End Sub

    <TestMethod>
    Public Sub Format_minutes_seconds()
        Assert.AreEqual("1:30", Ticks.Format(900000000L))
    End Sub

    <TestMethod>
    Public Sub Format_hours_minutes_seconds()
        Assert.AreEqual("1:00:00", Ticks.Format(36000000000L))
    End Sub

    <TestMethod>
    Public Sub FormatDuration_uses_same_rules()
        Assert.AreEqual("2:05", Ticks.FormatDuration(1250000000L))
    End Sub
End Class
