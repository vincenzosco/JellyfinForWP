Imports System
Imports JellyfinPlayer.Shared
Imports Windows.ApplicationModel.Resources

Namespace Services
    ' Every user-visible string the code builds at run time. Strings that live
    ' in XAML use x:Uid instead. English (en-US) is the default and the
    ' fallback; the device language picks the resource file.
    Public Module Localized
        Private ReadOnly _loader As ResourceLoader = ResourceLoader.GetForViewIndependentUse()

        Public Function Text(key As String) As String
            Return _loader.GetString(key)
        End Function

        Public Function Format(key As String, ParamArray args As Object()) As String
            Return String.Format(Text(key), args)
        End Function

        ' The user-facing sentence for a failed request, chosen from the
        ' classification the shared layer attached to it. A 401 stays a
        ' credentials message and is handled by the caller.
        Public Function DescribeError(ex As Exception) As String
            Dim failure = TryCast(ex, JellyfinException)
            If failure Is Nothing Then Return Format("ErrorGeneric", ex.Message)
            Select Case failure.Kind
                Case JellyfinErrorKind.HttpFailure
                    Return Format("ErrorHttp", failure.StatusCode)
                Case JellyfinErrorKind.NonJsonResponse
                    Return Text("ErrorNotJson")
                Case JellyfinErrorKind.Timeout
                    Return Text("ErrorTimeout")
                Case JellyfinErrorKind.Unreachable
                    Return Text("ErrorUnreachable")
                Case Else
                    Return Format("ErrorGeneric", failure.Message)
            End Select
        End Function
    End Module
End Namespace
