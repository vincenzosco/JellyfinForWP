Imports System

Namespace Global.JellyfinPlayer.Shared
    ' Keeps every seek inside the video: never before the start, never past the
    ' end, and nothing at all while the duration is still unknown (a transcode
    ' that has not reported its length yet).
    Public Module SeekTarget
        Public Function Relative(position As TimeSpan, delta As TimeSpan, duration As TimeSpan) As TimeSpan
            If duration <= TimeSpan.Zero Then Return TimeSpan.Zero
            If position < TimeSpan.Zero Then position = TimeSpan.Zero
            Return Clamp(position + delta, duration)
        End Function

        Public Function Absolute(seconds As Double, duration As TimeSpan) As TimeSpan
            If duration <= TimeSpan.Zero Then Return TimeSpan.Zero
            If Double.IsNaN(seconds) OrElse seconds <= 0 Then Return TimeSpan.Zero
            Return Clamp(TimeSpan.FromSeconds(seconds), duration)
        End Function

        Private Function Clamp(value As TimeSpan, duration As TimeSpan) As TimeSpan
            If value < TimeSpan.Zero Then Return TimeSpan.Zero
            If value > duration Then Return duration
            Return value
        End Function
    End Module
End Namespace
