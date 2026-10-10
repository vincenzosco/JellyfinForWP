Imports System

Namespace Global.JellyfinPlayer.Shared
    ' Turns a failure into the sentence shown to the user. `lookup` resolves a
    ' resource key, so the decision lives here (testable without WinRT) while the
    ' text lives in Strings/<lang>/Resources.resw.
    Public Module FailureText
        Public Function Describe(ex As Exception, lookup As Func(Of String, String)) As String
            Dim failure = TryCast(ex, JellyfinException)
            If failure Is Nothing Then Return String.Format(lookup("ErrorGeneric"), ex.Message)
            Select Case failure.Kind
                Case JellyfinErrorKind.HttpFailure
                    Return String.Format(lookup("ErrorHttp"), failure.StatusCode)
                Case JellyfinErrorKind.NonJsonResponse
                    Return lookup("ErrorNotJson")
                Case JellyfinErrorKind.Timeout
                    Return lookup("ErrorTimeout")
                Case JellyfinErrorKind.Unreachable
                    Return lookup("ErrorUnreachable")
                Case Else
                    ' Already user-facing text (a playback limitation, say): wrapping
                    ' it as "unexpected error" would double the sentence.
                    Return failure.Message
            End Select
        End Function
    End Module
End Namespace
