Imports System

Namespace Global.JellyfinPlayer.Shared
    Public Class JellyfinException
        Inherits Exception

        Private ReadOnly _statusCode As Integer
        Private ReadOnly _kind As JellyfinErrorKind

        Public ReadOnly Property StatusCode As Integer
            Get
                Return _statusCode
            End Get
        End Property

        ' Why the request failed, so the app can pick a localized sentence
        ' without inspecting the message body.
        Public ReadOnly Property Kind As JellyfinErrorKind
            Get
                Return _kind
            End Get
        End Property

        Public Sub New(message As String, statusCode As Integer,
                       Optional kind As JellyfinErrorKind = JellyfinErrorKind.Unknown,
                       Optional innerException As Exception = Nothing)
            MyBase.New(message, innerException)
            _statusCode = statusCode
            _kind = kind
        End Sub
    End Class
End Namespace
