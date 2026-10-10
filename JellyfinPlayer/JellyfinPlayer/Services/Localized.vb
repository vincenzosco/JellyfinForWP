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
        ' classification the shared layer attached to it (FailureText owns the
        ' decision, so it is covered by the desktop tests). A 401 stays a
        ' credentials message and is handled by the caller.
        Public Function DescribeError(ex As Exception) As String
            Return FailureText.Describe(ex, AddressOf Text)
        End Function
    End Module
End Namespace
