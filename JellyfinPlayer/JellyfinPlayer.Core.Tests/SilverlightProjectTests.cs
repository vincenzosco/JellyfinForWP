using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    // The project is a Windows Phone Silverlight one. These checks are the tripwire
    // for a return to an unsupported target: a project that silently keeps another
    // target, a page that is no longer a PhoneApplicationPage, a route that points at
    // a missing page, or manifests that disagree about identity and version all build
    // here and only fail on a device. The real files are copied or embedded by the
    // test project, never re-typed.
    [TestClass]
    public class SilverlightProjectTests
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        private static string ProjectText()
        {
            var stream = typeof(SilverlightProjectTests).Assembly.GetManifestResourceStream("JellyfinPlayer.csproj");
            Assert.IsNotNull(stream, "the current app project must be embedded into the test assembly");
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static string[] XamlFiles()
        {
            return Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml");
        }

        private static string[] AppFiles()
        {
            return Directory.GetFiles(Path.Combine(BaseDir, "App"), "*.cs");
        }

        private static string ReadManifest(string fileName)
        {
            return File.ReadAllText(Path.Combine(BaseDir, "Manifest", fileName));
        }

        [TestMethod]
        public void App_project_targets_windows_phone_silverlight_8()
        {
            string project = ProjectText();

            StringAssert.Contains(project, "<TargetFrameworkIdentifier>WindowsPhone</TargetFrameworkIdentifier>");
            StringAssert.Contains(project, "<TargetFrameworkVersion>v8.0</TargetFrameworkVersion>");
            StringAssert.Contains(project, "Microsoft.WindowsPhone.v8.0.Overrides.targets");
            StringAssert.Contains(project, "<SilverlightApplication>true</SilverlightApplication>");
            StringAssert.Contains(project, "Microsoft.WindowsPhone.CSharp.targets");
            Assert.IsFalse(project.Contains("WindowsXaml"), "remove the WinRT XAML targets");
            Assert.IsFalse(project.Contains("NETFX_CORE"), "remove the WinRT compilation symbol");
            Assert.IsFalse(project.Contains("AppContainerExe"), "Silverlight apps are not AppContainer executables");
        }

        [TestMethod]
        public void No_platform_only_api_survives_in_the_app()
        {
            // Each entry is a type or mechanism the Silverlight target does not have.
            // A leftover would compile on the desktop tests and fail as a missing type
            // in the phone build, or worse, silently keep an x:Uid nothing resolves.
            var forbidden = new[]
            {
                "Windows.UI.Xaml",
                "Windows.Storage",
                "Windows.Networking.Sockets",
                "DatagramSocket",
                "ResourceLoader",
                "ProgressRing",
                "ThemeResource",
                "ms-appx",
                "x:Uid"
            };
            var files = AppFiles().Concat(XamlFiles()).ToArray();
            Assert.IsTrue(files.Length >= 9, "expected the app sources and page XAML, found " + files.Length.ToString());
            foreach (var file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    Assert.IsFalse(text.Contains(term),
                        Path.GetFileName(file) + " still uses " + term + "; the Silverlight target does not provide it");
                }
            }
        }

        [TestMethod]
        public void Every_screen_is_a_phone_application_page()
        {
            var files = XamlFiles();
            Assert.IsTrue(files.Length >= 9, "expected the pages, found " + files.Length.ToString());
            foreach (var file in files)
            {
                string text = File.ReadAllText(file).TrimStart();
                StringAssert.StartsWith(text, "<phone:PhoneApplicationPage",
                    Path.GetFileName(file) + " must be a phone:PhoneApplicationPage, not a WinRT Page");
            }
        }

        [TestMethod]
        public void The_startup_route_exists_and_is_a_project_page()
        {
            string appSource = File.ReadAllText(Path.Combine(BaseDir, "App", "App.xaml.cs"));
            // The first page is the library when a login is saved, the login page
            // otherwise; both have to be pages this project compiles.
            StringAssert.Contains(appSource, "StartRoute.For(Services.AppSession.Current.IsLoggedIn)",
                "App.xaml.cs must pick the startup page from the saved login");
            foreach (var route in new[] { StartRoute.Login, StartRoute.Library })
            {
                string name = route.Substring(route.LastIndexOf('/') + 1);
                Assert.IsTrue(File.Exists(Path.Combine(BaseDir, "Xaml", name)), route + " must exist");
            }
            StringAssert.Contains(ProjectText(), "<Page Include=\"MainPage.xaml\"");
            StringAssert.Contains(ProjectText(), "<Page Include=\"Pages\\HomePage.xaml\"");
        }

        [TestMethod]
        public void A_saved_login_opens_the_library()
        {
            Assert.AreEqual("/Pages/HomePage.xaml", StartRoute.For(true));
            Assert.AreEqual("/MainPage.xaml", StartRoute.For(false));
        }

        [TestMethod]
        public void Every_project_page_and_source_exists()
        {
            string project = ProjectText();
            var pages = Regex.Matches(project, "<Page Include=\"([^\"]+)\"").Cast<Match>().ToList();
            Assert.IsTrue(pages.Count >= 9, "expected the Silverlight pages in the project, found " + pages.Count.ToString());

            var pageNames = new List<string>();
            foreach (var page in pages)
            {
                string relative = page.Groups[1].Value.Replace("\\", "/");
                string name = relative.Substring(relative.LastIndexOf('/') + 1);
                pageNames.Add(name);
                Assert.IsTrue(File.Exists(Path.Combine(BaseDir, "Xaml", name)),
                    "the project lists " + relative + " but the page file is missing");
            }
            foreach (var file in XamlFiles())
            {
                string name = Path.GetFileName(file);
                Assert.IsTrue(pageNames.Contains(name), name + " exists but the project never compiles it");
            }

            foreach (Match item in Regex.Matches(project, "<Compile Include=\"([^\"]+)\">"))
            {
                string relative = item.Groups[1].Value.Replace("\\", "/");
                // Wildcards are covered by the build; the generated AssemblyInfo is not copied.
                if (relative.Contains("*") || relative.StartsWith("My Project/")) continue;
                string name = relative.Substring(relative.LastIndexOf('/') + 1);
                Assert.IsTrue(File.Exists(Path.Combine(BaseDir, "App", name)),
                    "the project compiles " + relative + " but the source file is missing");
            }
        }

        [TestMethod]
        // A Windows Phone 8 project has one manifest: the Silverlight 8.1 upgrade is
        // what adds the appx package manifest, and this target does not have it.
        public void The_phone_manifest_carries_the_product_identity_and_version()
        {
            string phone = ReadManifest("WMAppManifest.xml");

            var phoneIdentity = Regex.Match(phone, "ProductID=\"\\{([^}]+)\\}\"");
            // A bare "Version=" would also match the root's AppPlatformVersion="8.0".
            var phoneVersion = Regex.Match(phone, "\\sVersion=\"([^\"]+)\"");
            Assert.IsTrue(phoneIdentity.Success, "WMAppManifest.xml must carry a ProductID");
            Assert.IsTrue(phoneVersion.Success, "WMAppManifest.xml must carry a version");

            StringAssert.Contains(phone, "AppPlatformVersion=\"8.0\"");
            Assert.AreEqual("1.2.1.4", phoneVersion.Groups[1].Value,
                "the user-approved version must survive the port");
        }

        [TestMethod]
        public void The_phone_manifest_declares_networking_and_a_start_page()
        {
            string phone = ReadManifest("WMAppManifest.xml");

            StringAssert.Contains(phone, "ID_CAP_NETWORKING", "the app talks to the server and discovery needs networking");
            StringAssert.Contains(phone, "RuntimeType=\"Silverlight\"");
            StringAssert.Contains(phone, "<DefaultTask Name=\"_default\" NavigationPage=\"MainPage.xaml\" />");
        }
    }
}
