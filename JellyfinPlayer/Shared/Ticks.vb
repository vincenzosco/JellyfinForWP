Imports System

Namespace Global.JellyfinPlayer.Shared
    Public Module Ticks
        Public Const TicksPerSecond As Long = 10000000

        Public Function ToTimeSpan(ticks As Long) As TimeSpan
            Return TimeSpan.FromTicks(ticks)
        End Function

        Public Function FromTimeSpan(value As TimeSpan) As Long
            Return value.Ticks
        End Function

        ' Returns "" for Nothing, "h:mm:ss" when at least one hour, else "m:ss".
        Public Function Format(ticks As Long?) As String
            If Not ticks.HasValue Then Return ""
            Return FormatSpan(TimeSpan.FromTicks(ticks.Value))
        End Function

        Public Function FormatDuration(ticks As Long?) As String
            Return Format(ticks)
        End Function

        ' Same rules for a TimeSpan, which is what the player measures with.
        Public Function FormatSpan(value As TimeSpan) As String
            If value.Ticks < 0 Then value = TimeSpan.Zero
            If value.TotalHours >= 1 Then
                Return String.Format("{0}:{1:00}:{2:00}", CInt(Math.Floor(value.TotalHours)), value.Minutes, value.Seconds)
            End If
            Return String.Format("{0}:{1:00}", CInt(Math.Floor(value.TotalMinutes)), value.Seconds)
        End Function
    End Module
End Namespace
