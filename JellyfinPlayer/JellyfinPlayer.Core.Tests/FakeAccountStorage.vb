' An in-memory IAccountStorage: LocalSettings cannot be reached from a desktop test.
Public Class FakeAccountStorage
    Implements IAccountStorage

    Public ReadOnly Property Saved As String
        Get
            Return _saved
        End Get
    End Property

    Public Property ThrowOnLoad As Boolean
    Public Property ThrowOnSave As Boolean

    Public Sub New(Optional initial As String = Nothing)
        _saved = initial
    End Sub

    Public Function Load() As String Implements IAccountStorage.Load
        If ThrowOnLoad Then Throw New System.Exception("storage unavailable")
        Return _saved
    End Function

    Public Sub Save(json As String) Implements IAccountStorage.Save
        If ThrowOnSave Then Throw New System.Exception("storage full")
        _saved = json
    End Sub

    Private _saved As String
End Class
