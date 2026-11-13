Imports System.Globalization
Imports System.Resources
Imports JellyfinPlayer.Shared

Namespace Services
    ' Every user-visible string the code builds at run time. English (en-US) is the
    ' neutral resource and the fallback; the device UI culture selects the it-IT
    ' satellite when present. The strings live in Resources.resx / Resources.it-IT.resx.
    Public Module Localized
        Private ReadOnly _manager As New ResourceManager("JellyfinPlayer.Resources", GetType(Localized).Assembly)

        Public Function Text(key As String) As String
            Dim value = _manager.GetString(key, CultureInfo.CurrentUICulture)
            Return If(String.IsNullOrEmpty(value), key, value)
        End Function

        Public Function Format(key As String, ParamArray args As Object()) As String
            Return String.Format(CultureInfo.CurrentCulture, Text(key), args)
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
