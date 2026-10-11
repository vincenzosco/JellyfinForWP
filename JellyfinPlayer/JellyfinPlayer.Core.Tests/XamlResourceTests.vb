Imports System
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions

' Guards the XAML against the two mistakes that make a whole page fail to load on
' a device. A page that cannot load is the worst failure this app has: the button
' that opened it looks dead, because Frame.Navigate returns False and nothing is
' shown.
'
' 1. An object element whose type XAML cannot create (the WinRT base types:
'    Geometry, Brush, Transform...). A Geometry value needs a nonabstract derived
'    type - PathGeometry, for one - so the base type cannot be created.
' 2. A {StaticResource} with no matching x:Key in the same file.
<TestClass>
Public Class XamlResourceTests
    Private Shared ReadOnly BaseDir As String = AppDomain.CurrentDomain.BaseDirectory

    ' XAML base types that markup cannot instantiate: a resource or object element
    ' has to name a derived, activatable type instead.
    Private Shared ReadOnly NotCreatableTypes As String() = {
        "Geometry",
        "Brush",
        "Transform",
        "Shape",
        "PathSegment",
        "ImageSource",
        "Timeline",
        "EasingFunctionBase"
    }

    Private Shared Function XamlFiles() As String()
        Return Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml")
    End Function

    <TestMethod>
    Public Sub No_XAML_element_uses_a_type_that_XAML_cannot_create()
        Dim files = XamlFiles()
        Assert.IsTrue(files.Length >= 6, "expected the page XAML files, found " & files.Length.ToString())
        For Each file In files
            ' System.IO.File, not File: the loop variable is called file too.
            Dim text As String = System.IO.File.ReadAllText(file)
            For Each typeName As String In NotCreatableTypes
                ' "<Geometry ", "<Geometry>" or "<Geometry/>", but not a property
                ' element such as <Geometry.Transform>.
                Dim m = Regex.Match(text, "<\s*" & typeName & "(?![A-Za-z.])")
                Assert.IsFalse(m.Success,
                    Path.GetFileName(file) & " creates <" & typeName & "> as an object element; use a derived type (for a path, PathGeometry or the Path's own Data attribute)")
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub Every_StaticResource_has_an_xKey_in_the_same_file()
        For Each file In XamlFiles()
            Dim text As String = System.IO.File.ReadAllText(file)
            Dim defined = Regex.Matches(text, "x:Key=""([^""]+)""").Cast(Of Match)().
                                 Select(Function(m) m.Groups(1).Value).ToList()
            For Each m In Regex.Matches(text, "StaticResource\s+([^}\s]+)").Cast(Of Match)()
                Dim key As String = m.Groups(1).Value
                Assert.IsTrue(defined.Contains(key),
                    Path.GetFileName(file) & " uses {StaticResource " & key & "} but defines no x:Key for it")
            Next
        Next
    End Sub
End Class
