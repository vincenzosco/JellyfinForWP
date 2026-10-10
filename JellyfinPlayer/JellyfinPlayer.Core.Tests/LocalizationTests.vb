Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

' Guards the English/Italian resources against the failure modes a user would
' otherwise meet: a missing translation, an empty value, a placeholder that
' survives translation differently, an untranslated XAML literal, and a page
' that prints a raw .NET exception message. The files are copied next to the
' test assembly by the project file, so the tests read the real ones.
<TestClass>
Public Class LocalizationTests
    Private Shared ReadOnly BaseDir As String = AppDomain.CurrentDomain.BaseDirectory

    Private Shared Function Resources(language As String) As Dictionary(Of String, String)
        Dim doc = XDocument.Load(System.IO.Path.Combine(BaseDir, "Localization", language, "Resources.resw"))
        Return doc.Descendants("data").ToDictionary(
            Function(d) d.Attribute("name").Value,
            Function(d) d.Element("value").Value)
    End Function

    Private Shared ReadOnly English As Dictionary(Of String, String) = Resources("en-US")
    Private Shared ReadOnly Italian As Dictionary(Of String, String) = Resources("it-IT")

    Private Shared Function Placeholders(value As String) As String
        Return String.Join(",", Regex.Matches(value, "{[0-9]+}").Cast(Of Match)().
                                  Select(Function(m) m.Value).Distinct().OrderBy(Function(s) s).ToArray())
    End Function

    Private Shared Function XamlFiles() As String()
        Return System.IO.Directory.GetFiles(System.IO.Path.Combine(BaseDir, "Xaml"), "*.xaml")
    End Function

    Private Shared Function AppSources() As String()
        Return System.IO.Directory.GetFiles(System.IO.Path.Combine(BaseDir, "App"), "*.vb")
    End Function

    <TestMethod>
    Public Sub Italian_and_English_resources_have_the_same_keys()
        Dim missingInItalian = English.Keys.Except(Italian.Keys).OrderBy(Function(k) k).ToArray()
        Dim missingInEnglish = Italian.Keys.Except(English.Keys).OrderBy(Function(k) k).ToArray()
        Assert.AreEqual(0, missingInItalian.Length, "missing in it-IT: " & String.Join(", ", missingInItalian))
        Assert.AreEqual(0, missingInEnglish.Length, "missing in en-US: " & String.Join(", ", missingInEnglish))
        Assert.IsTrue(English.Count > 40, "only " & English.Count.ToString() & " resources in en-US")
    End Sub

    <TestMethod>
    Public Sub No_resource_value_is_empty()
        For Each pair In {Tuple.Create("en-US", English), Tuple.Create("it-IT", Italian)}
            For Each entry In pair.Item2
                Assert.IsFalse(String.IsNullOrWhiteSpace(entry.Value), pair.Item1 & " has an empty value for " & entry.Key)
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub Format_placeholders_match_between_languages()
        For Each key In English.Keys
            Assert.AreEqual(Placeholders(English(key)), Placeholders(Italian(key)), "placeholder mismatch for " & key)
        Next
    End Sub

    <TestMethod>
    Public Sub Every_xUid_in_XAML_has_a_resource_in_both_languages()
        Dim allXaml = XamlFiles()
        Assert.IsTrue(allXaml.Length >= 6, "expected the page XAML files, found " & allXaml.Length.ToString())
        For Each xamlFile In allXaml
            Dim text As String = System.IO.File.ReadAllText(xamlFile)
            For Each uid In Regex.Matches(text, "x:Uid=""([^""]+)""").Cast(Of Match)().Select(Function(m) m.Groups(1).Value)
                Dim matching = English.Keys.Where(Function(k) k.StartsWith(uid & ".", StringComparison.Ordinal)).ToArray()
                Assert.AreEqual(1, matching.Length,
                    System.IO.Path.GetFileName(xamlFile) & ": x:Uid=""" & uid & """ needs exactly one of " & uid & ".Text/.Content/.PlaceholderText, found " & matching.Length.ToString())
                Assert.IsTrue(Italian.ContainsKey(matching(0)), "missing it-IT translation for " & matching(0))
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub Every_key_used_from_code_exists()
        Dim used As New List(Of String)()
        For Each sourceFile In AppSources()
            Dim text As String = System.IO.File.ReadAllText(sourceFile)
            For Each m In Regex.Matches(text, "Localized\.(?:Text|Format)\(\s*""([^""]+)""").Cast(Of Match)()
                used.Add(m.Groups(1).Value)
            Next
        Next
        Assert.IsTrue(used.Count > 10, "expected localized call sites in the app, found " & used.Count.ToString())
        For Each key In used.Distinct()
            Assert.IsTrue(English.ContainsKey(key), "en-US is missing " & key)
            Assert.IsTrue(Italian.ContainsKey(key), "it-IT is missing " & key)
        Next
    End Sub

    <TestMethod>
    Public Sub Manifest_declares_english_and_italian()
        ' The check is about which languages are declared, not about how the XML is
        ' written: Visual Studio reflows this file (spaces before '/>', one attribute
        ' per line) whenever the manifest designer saves it, and that must not fail.
        Dim text As String = System.IO.File.ReadAllText(System.IO.Path.Combine(BaseDir, "Manifest", "Package.appxmanifest")).Replace(" />", "/>")
        Assert.IsTrue(text.Contains("<Resource Language=""en-US""/>"), text)
        Assert.IsTrue(text.Contains("<Resource Language=""it-IT""/>"), text)
        Assert.IsFalse(text.Contains("x-generate"), "declare the supported languages explicitly")
    End Sub

    <TestMethod>
    Public Sub No_page_shows_a_raw_exception_message()
        For Each sourceFile In AppSources()
            Dim text As String = System.IO.File.ReadAllText(sourceFile)
            Assert.IsFalse(Regex.IsMatch(text, "\.Text\s*=\s*ex\.Message"),
                System.IO.Path.GetFileName(sourceFile) & " prints ex.Message directly; use Localized.DescribeError(ex)")
        Next
    End Sub

    <TestMethod>
    Public Sub No_maintained_XAML_literal_text_remains()
        ' The brand name and the example URL are deliberately not translated;
        ' "10" and "30" are the numeric badges over the skip icons - digits, not prose.
        Dim allowed = {"Jellyfin", "http://192.168.1.10:8096", "10", "30"}
        For Each xamlFile In XamlFiles()
            Dim text As String = System.IO.File.ReadAllText(xamlFile)
            For Each m In Regex.Matches(text, "\s(?:Text|Content|PlaceholderText)=""([^""{][^""]*)""").Cast(Of Match)()
                Dim value = m.Groups(1).Value
                Assert.IsTrue(allowed.Contains(value),
                    System.IO.Path.GetFileName(xamlFile) & ": literal """ & value & """ is not localized")
            Next
        Next
    End Sub
End Class
