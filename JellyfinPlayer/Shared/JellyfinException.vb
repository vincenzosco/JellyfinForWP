Imports System

Namespace Global.JellyfinPlayer.Shared
    Public Class JellyfinException
        Inherits Exception

        Private ReadOnly _statusCode As Integer

        Public ReadOnly Property StatusCode As Integer
            Get
                Return _statusCode
            End Get
        End Property

        Public Sub New(message As String, statusCode As Integer)
            MyBase.New(message)
            _statusCode = statusCode
        End Sub
    End Class
End Namespace
