<TestClass>
Public Class SeekTargetTests
    Private Shared ReadOnly TwoHours As TimeSpan = TimeSpan.FromHours(2)

    <TestMethod>
    Public Sub Relative_forward_stays_inside_the_video()
        Assert.AreEqual(TimeSpan.FromSeconds(90), SeekTarget.Relative(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30), TwoHours))
    End Sub

    <TestMethod>
    Public Sub Relative_back_at_the_start_stops_at_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Relative(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(-30), TwoHours))
    End Sub

    <TestMethod>
    Public Sub Relative_forward_at_the_end_stops_at_the_end()
        Assert.AreEqual(TwoHours, SeekTarget.Relative(TimeSpan.FromSeconds(7190), TimeSpan.FromSeconds(30), TwoHours))
    End Sub

    <TestMethod>
    Public Sub Relative_without_a_duration_is_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Relative(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), TimeSpan.Zero))
    End Sub

    <TestMethod>
    Public Sub Relative_treats_a_negative_position_as_zero()
        Assert.AreEqual(TimeSpan.FromSeconds(10), SeekTarget.Relative(TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(10), TwoHours))
    End Sub

    <TestMethod>
    Public Sub Absolute_clamps_to_the_duration()
        Assert.AreEqual(TwoHours, SeekTarget.Absolute(9999.0, TwoHours))
    End Sub

    <TestMethod>
    Public Sub Absolute_below_zero_is_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Absolute(-5.0, TwoHours))
    End Sub

    <TestMethod>
    Public Sub Absolute_keeps_a_valid_position()
        Assert.AreEqual(TimeSpan.FromSeconds(75.5), SeekTarget.Absolute(75.5, TwoHours))
    End Sub

    <TestMethod>
    Public Sub Absolute_without_a_duration_is_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Absolute(30.0, TimeSpan.Zero))
    End Sub

    <TestMethod>
    Public Sub Absolute_ignores_not_a_number()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Absolute(Double.NaN, TwoHours))
    End Sub

    <TestMethod>
    Public Sub Absolute_never_exceeds_the_shorter_duration()
        Assert.AreEqual(TimeSpan.FromSeconds(30), SeekTarget.Absolute(90.0, TimeSpan.FromSeconds(30)))
    End Sub
End Class
