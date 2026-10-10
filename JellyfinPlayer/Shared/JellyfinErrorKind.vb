Namespace Global.JellyfinPlayer.Shared
    ' Why a request failed, so the app can pick a localized sentence without
    ' inspecting the message. Unknown = 0 keeps every existing two-argument
    ' JellyfinException call site meaning "unspecified".
    Public Enum JellyfinErrorKind As Integer
        Unknown = 0
        HttpFailure = 1
        NonJsonResponse = 2
        Timeout = 3
        Unreachable = 4
    End Enum
End Namespace
