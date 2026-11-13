using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    // Guards the English/Italian resources against the failure modes a user would
    // otherwise meet: a missing translation, an empty value, a placeholder that
    // survives translation differently, an untranslated XAML literal, and a page
    // that prints a raw .NET exception message. The files are copied next to the
    // test assembly by the project file, so the tests read the real ones.
    [TestClass]
    public class LocalizationTests
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        private static Dictionary<string, string> Resources(string language)
        {
            var doc = XDocument.Load(Path.Combine(BaseDir, "Localization", language, "Resources.resx"));
            return doc.Descendants("data").ToDictionary(
                d => d.Attribute("name").Value,
                d => d.Element("value").Value);
        }

        private static readonly Dictionary<string, string> English = Resources("en-US");
        private static readonly Dictionary<string, string> Italian = Resources("it-IT");

        private static string Placeholders(string value)
        {
            return string.Join(",", Regex.Matches(value, "{([0-9]+)}").Cast<Match>()
                                      .Select(m => m.Value).Distinct().OrderBy(s => s).ToArray());
        }

        private static string[] XamlFiles()
        {
            return Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml");
        }

        private static string[] AppSources()
        {
            return Directory.GetFiles(Path.Combine(BaseDir, "App"), "*.cs");
        }

        [TestMethod]
        public void Italian_and_English_resources_have_the_same_keys()
        {
            var missingInItalian = English.Keys.Except(Italian.Keys).OrderBy(k => k).ToArray();
            var missingInEnglish = Italian.Keys.Except(English.Keys).OrderBy(k => k).ToArray();
            Assert.AreEqual(0, missingInItalian.Length, "missing in it-IT: " + string.Join(", ", missingInItalian));
            Assert.AreEqual(0, missingInEnglish.Length, "missing in en-US: " + string.Join(", ", missingInEnglish));
            Assert.IsTrue(English.Count > 40, "only " + English.Count.ToString() + " resources in en-US");
        }

        [TestMethod]
        public void No_resource_value_is_empty()
        {
            foreach (var pair in new[] { Tuple.Create("en-US", English), Tuple.Create("it-IT", Italian) })
            {
                foreach (var entry in pair.Item2)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value), pair.Item1 + " has an empty value for " + entry.Key);
                }
            }
        }

        [TestMethod]
        public void Format_placeholders_match_between_languages()
        {
            foreach (var key in English.Keys)
            {
                Assert.AreEqual(Placeholders(English[key]), Placeholders(Italian[key]), "placeholder mismatch for " + key);
            }
        }

        [TestMethod]
        public void Every_key_referenced_from_XAML_or_code_has_a_resource_in_both_languages()
        {
            var allXaml = XamlFiles();
            Assert.IsTrue(allXaml.Length >= 6, "expected the page XAML files, found " + allXaml.Length.ToString());
            var referenced = new HashSet<string>();
            foreach (var xamlFile in allXaml)
            {
                string text = File.ReadAllText(xamlFile);
                // Silverlight resolves labels from code via Localized.Text("key"); a
                // {StaticResource} is a theme brush, never a string key.
                foreach (Match m in Regex.Matches(text, "Localized\\.Text\\(\\s*\"([^\"]+)\"\\)"))
                    referenced.Add(m.Groups[1].Value);
            }
            foreach (var sourceFile in AppSources())
            {
                string text = File.ReadAllText(sourceFile);
                foreach (Match m in Regex.Matches(text, "Localized\\.(?:Text|Format)\\(\\s*\"([^\"]+)\""))
                    referenced.Add(m.Groups[1].Value);
            }
            foreach (var key in referenced)
            {
                Assert.IsTrue(English.ContainsKey(key), "en-US is missing " + key);
                Assert.IsTrue(Italian.ContainsKey(key), "it-IT is missing " + key);
            }
        }

        [TestMethod]
        public void Every_key_used_from_code_exists()
        {
            var used = new List<string>();
            foreach (var sourceFile in AppSources())
            {
                string text = File.ReadAllText(sourceFile);
                foreach (Match m in Regex.Matches(text, "Localized\\.(?:Text|Format)\\(\\s*\"([^\"]+)\""))
                    used.Add(m.Groups[1].Value);
            }
            Assert.IsTrue(used.Count > 10, "expected localized call sites in the app, found " + used.Count.ToString());
            foreach (var key in used.Distinct())
            {
                Assert.IsTrue(English.ContainsKey(key), "en-US is missing " + key);
                Assert.IsTrue(Italian.ContainsKey(key), "it-IT is missing " + key);
            }
        }

        [TestMethod]
        public void Manifest_declares_english_and_italian()
        {
            // A Windows Phone 8 project declares its languages this way: the neutral
            // language in the phone manifest, the satellites in the project file.
            string text = File.ReadAllText(Path.Combine(BaseDir, "Manifest", "WMAppManifest.xml")).Replace(" />", "/>");
            Assert.IsTrue(text.Contains("code=\"en-US\""), text);

            var stream = typeof(LocalizationTests).Assembly.GetManifestResourceStream("JellyfinPlayer.csproj");
            Assert.IsNotNull(stream, "the app project must be embedded into the test assembly");
            string project;
            using (var reader = new StreamReader(stream))
            {
                project = reader.ReadToEnd();
            }
            StringAssert.Contains(project, "<SupportedCultures>it-IT</SupportedCultures>");
        }

        [TestMethod]
        public void No_page_shows_a_raw_exception_message()
        {
            foreach (var sourceFile in AppSources())
            {
                string text = File.ReadAllText(sourceFile);
                Assert.IsFalse(Regex.IsMatch(text, "\\.Text\\s*=\\s*ex\\.Message"),
                    Path.GetFileName(sourceFile) + " prints ex.Message directly; use Localized.DescribeError(ex)");
            }
        }

        [TestMethod]
        public void No_maintained_XAML_literal_text_remains()
        {
            // The brand name and the example URL are deliberately not translated;
            // "10" and "30" are the numeric badges over the skip icons - digits, not prose.
            var allowed = new[] { "Jellyfin", "http://192.168.1.10:8096", "10", "30" };
            foreach (var xamlFile in XamlFiles())
            {
                string text = File.ReadAllText(xamlFile);
                foreach (Match m in Regex.Matches(text, "\\s(?:Text|Content|PlaceholderText)=\"([^\"{][^\"]*)\""))
                {
                    string value = m.Groups[1].Value;
                    Assert.IsTrue(allowed.Contains(value),
                        Path.GetFileName(xamlFile) + ": literal \"" + value + "\" is not localized");
                }
            }
        }
    }
}
