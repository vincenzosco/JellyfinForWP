<TestClass>
Public Class FailureTextTests
    ' Stands in for the resource lookup the app passes in (Windows resource
    ' loader), so the mapping is testable on the desktop. Same English strings
    ' as Strings/en-US/Resources.resw.
    Private Shared Function Lookup(key As String) As String
        Select Case key
            Case "ErrorHttp" : Return "The server answered HTTP {0}."
            Case "ErrorNotJson" : Return "The address answered with a web page instead of the Jellyfin API."
            Case "ErrorTimeout" : Return "The server did not respond in time."
            Case "ErrorUnreachable" : Return "Could not reach the server."
            Case "ErrorGeneric" : Return "Unexpected error: {0}"
            Case Else : Throw New InvalidOperationException("unexpected key " & key)
        End Select
    End Function

    <TestMethod>
    Public Sub Classified_failures_use_the_matching_sentence()
        Assert.AreEqual("The address answered with a web page instead of the Jellyfin API.",
            FailureText.Describe(New JellyfinException("ignored", 0, JellyfinErrorKind.NonJsonResponse), AddressOf Lookup))
        Assert.AreEqual("The server answered HTTP 404.",
            FailureText.Describe(New JellyfinException("ignored", 404, JellyfinErrorKind.HttpFailure), AddressOf Lookup))
        Assert.AreEqual("The server did not respond in time.",
            FailureText.Describe(New JellyfinException("ignored", 0, JellyfinErrorKind.Timeout), AddressOf Lookup))
        Assert.AreEqual("Could not reach the server.",
            FailureText.Describe(New JellyfinException("ignored", 0, JellyfinErrorKind.Unreachable), AddressOf Lookup))
    End Sub

    ' PlaybackService and the "play direct" fallback throw sentences that are
    ' already localized and already user-facing. Wrapping them as "unexpected
    ' error: <sentence>" shows the user a doubled message.
    <TestMethod>
    Public Sub An_unclassified_failure_keeps_its_own_message()
        Assert.AreEqual("Nessuno stream diretto disponibile.",
            FailureText.Describe(New JellyfinException("Nessuno stream diretto disponibile.", 0), AddressOf Lookup))
        Assert.AreEqual("That address is not a Jellyfin server.",
            FailureText.Describe(New JellyfinException("That address is not a Jellyfin server.", 0), AddressOf Lookup))
    End Sub

    <TestMethod>
    Public Sub An_unexpected_exception_is_reported_as_unexpected()
        Assert.AreEqual("Unexpected error: boom",
            FailureText.Describe(New InvalidOperationException("boom"), AddressOf Lookup))
    End Sub
End Class
