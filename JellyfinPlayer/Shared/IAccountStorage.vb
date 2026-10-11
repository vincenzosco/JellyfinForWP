Namespace Global.JellyfinPlayer.Shared
    ' Keeps AccountStore free of WinRT: the app binds this to LocalSettings, the
    ' tests to memory. Same pattern as IHttpTransport.
    Public Interface IAccountStorage
        Function Load() As String
        Sub Save(json As String)
    End Interface
End Namespace
