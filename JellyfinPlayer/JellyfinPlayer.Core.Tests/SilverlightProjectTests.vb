Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions

' The port replaced the WinRT app with a Windows Phone Silverlight one. These
' checks are the tripwire for the way back: a project that silently keeps a WinRT
' target, a page that is no longer a PhoneApplicationPage, a route that points at
' a missing page, or manifests that disagree about identity and version all build
' here and only fail on a device. The real files are copied or embedded by the test
' project, never re-typed.
<TestClass>
Public Class SilverlightProjectTests
    Private Shared ReadOnly BaseDir As String = AppDomain.CurrentDomain.BaseDirectory

    Private Shared Function ProjectText() As String
        Dim stream = GetType(SilverlightProjectTests).Assembly.GetManifestResourceStream("JellyfinPlayer.vbproj")
        Assert.IsNotNull(stream, "the current app project must be embedded into the test assembly")
        Using reader As New StreamReader(stream)
            Return reader.ReadToEnd()
        End Using
    End Function

    Private Shared Function XamlFiles() As String()
        Return Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml")
    End Function

    Private Shared Function AppFiles() As String()
        Return Directory.GetFiles(Path.Combine(BaseDir, "App"), "*.vb")
    End Function

    Private Shared Function ReadManifest(fileName As String) As String
        Return System.IO.File.ReadAllText(Path.Combine(BaseDir, "Manifest", fileName))
    End Function

    <TestMethod>
    Public Sub App_project_targets_windows_phone_silverlight_81()
        Dim project As String = ProjectText()

        StringAssert.Contains(project, "<TargetFrameworkIdentifier>WindowsPhone</TargetFrameworkIdentifier>")
        StringAssert.Contains(project, "<TargetFrameworkVersion>v8.1</TargetFrameworkVersion>")
        StringAssert.Contains(project, "<SilverlightApplication>true</SilverlightApplication>")
        StringAssert.Contains(project, "Microsoft.WindowsPhone.VisualBasic.targets")
        Assert.IsFalse(project.Contains("WindowsXaml"), "remove the WinRT XAML targets")
        Assert.IsFalse(project.Contains("NETFX_CORE"), "remove the WinRT compilation symbol")
        Assert.IsFalse(project.Contains("AppContainerExe"), "Silverlight apps are not AppContainer executables")
    End Sub

    <TestMethod>
    Public Sub No_WinRT_only_api_survives_in_the_app()
        ' Each entry is a type or mechanism the Silverlight target does not have.
        ' A leftover would compile on the desktop tests and fail as a missing type
        ' in the phone build, or worse, silently keep an x:Uid nothing resolves.
        Dim forbidden = {
            "Windows.UI.Xaml",
            "Windows.Storage",
            "Windows.Networking.Sockets",
            "DatagramSocket",
            "ResourceLoader",
            "ProgressRing",
            "ThemeResource",
            "ms-appx",
            "x:Uid"
        }
        Dim files = AppFiles().Concat(XamlFiles()).ToArray()
        Assert.IsTrue(files.Length >= 9, "expected the app sources and page XAML, found " & files.Length.ToString())
        For Each file In files
            Dim text As String = System.IO.File.ReadAllText(file)
            For Each term As String In forbidden
                Assert.IsFalse(text.Contains(term),
                    Path.GetFileName(file) & " still uses " & term & "; the Silverlight target does not provide it")
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub Every_screen_is_a_phone_application_page()
        Dim files = XamlFiles()
        Assert.IsTrue(files.Length >= 9, "expected MainPage and the eight pages, found " & files.Length.ToString())
        For Each file In files
            Dim text As String = System.IO.File.ReadAllText(file).TrimStart()
            StringAssert.StartsWith(text, "<phone:PhoneApplicationPage",
                Path.GetFileName(file) & " must be a phone:PhoneApplicationPage, not a WinRT Page")
        Next
    End Sub

    <TestMethod>
    Public Sub The_startup_route_exists_and_is_a_project_page()
        Dim appSource As String = System.IO.File.ReadAllText(Path.Combine(BaseDir, "App", "App.xaml.vb"))
        Dim route = Regex.Match(appSource, "Navigate\(\s*New Uri\(""([^""]+)"", UriKind\.Relative\)")
        Assert.IsTrue(route.Success, "App.xaml.vb must navigate to a startup page")
        Assert.AreEqual("/MainPage.xaml", route.Groups(1).Value)
        Assert.IsTrue(System.IO.File.Exists(Path.Combine(BaseDir, "Xaml", "MainPage.xaml")),
            "the startup page must exist")
        StringAssert.Contains(ProjectText(), "<Page Include=""MainPage.xaml""")
    End Sub

    <TestMethod>
    Public Sub Every_project_page_and_source_exists()
        Dim project As String = ProjectText()
        Dim pages = Regex.Matches(project, "<Page Include=""([^""]+)""").Cast(Of Match)().ToList()
        Assert.IsTrue(pages.Count >= 9, "expected the Silverlight pages in the project, found " & pages.Count.ToString())

        Dim pageNames As New List(Of String)()
        For Each page In pages
            Dim relative = page.Groups(1).Value.Replace("\", "/")
            Dim name = relative.Substring(relative.LastIndexOf("/"c) + 1)
            pageNames.Add(name)
            Assert.IsTrue(System.IO.File.Exists(Path.Combine(BaseDir, "Xaml", name)),
                "the project lists " & relative & " but the page file is missing")
        Next
        For Each file In XamlFiles()
            Dim name = Path.GetFileName(file)
            Assert.IsTrue(pageNames.Contains(name), name & " exists but the project never compiles it")
        Next

        For Each item In Regex.Matches(project, "<Compile Include=""([^""]+)""").Cast(Of Match)()
            Dim relative = item.Groups(1).Value.Replace("\", "/")
            ' Wildcards are covered by the build; the generated AssemblyInfo is not copied.
            If relative.Contains("*") OrElse relative.StartsWith("My Project/") Then Continue For
            Dim name = relative.Substring(relative.LastIndexOf("/"c) + 1)
            Assert.IsTrue(System.IO.File.Exists(Path.Combine(BaseDir, "App", name)),
                "the project compiles " & relative & " but the source file is missing")
        Next
    End Sub

    <TestMethod>
    Public Sub Both_manifests_agree_on_identity_and_version()
        Dim package As String = ReadManifest("Package.appxmanifest")
        Dim phone As String = ReadManifest("WMAppManifest.xml")

        Dim packageIdentity = Regex.Match(package, "<Identity[^>]*Name=""([^""]+)""[^>]*Version=""([^""]+)""")
        Assert.IsTrue(packageIdentity.Success, "Package.appxmanifest must carry an Identity name and version")

        Dim phoneIdentity = Regex.Match(phone, "ProductID=""\{([^}]+)\}""")
        ' A bare "Version=" would also match the root's AppPlatformVersion="8.1".
        Dim phoneVersion = Regex.Match(phone, "\sVersion=""([^""]+)""")
        Assert.IsTrue(phoneIdentity.Success, "WMAppManifest.xml must carry a ProductID")
        Assert.IsTrue(phoneVersion.Success, "WMAppManifest.xml must carry a version")

        Assert.AreEqual(packageIdentity.Groups(1).Value, phoneIdentity.Groups(1).Value,
            "the package and the phone manifest disagree about the product identity")
        Assert.AreEqual(packageIdentity.Groups(2).Value, phoneVersion.Groups(1).Value,
            "the package and the phone manifest disagree about the version")
        Assert.AreEqual("1.2.1.4", phoneVersion.Groups(1).Value,
            "the user-approved version must survive the port")
    End Sub

    <TestMethod>
    Public Sub The_phone_manifest_declares_networking_and_a_start_page()
        Dim phone As String = ReadManifest("WMAppManifest.xml")

        StringAssert.Contains(phone, "ID_CAP_NETWORKING", "the app talks to the server and discovery needs networking")
        StringAssert.Contains(phone, "RuntimeType=""Silverlight""")
        StringAssert.Contains(phone, "<DefaultTask Name=""_default"" NavigationPage=""MainPage.xaml"" />")
    End Sub
End Class
