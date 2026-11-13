using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    // Guards the XAML against the two mistakes that make a whole page fail to load on
    // a device. A page that cannot load is the worst failure this app has: the button
    // that opened it looks dead, because Frame.Navigate returns false and nothing is
    // shown.
    //
    // 1. An object element whose type XAML cannot create (the abstract base types:
    //    Geometry, Brush, Transform...). A Geometry value needs a derived type.
    // 2. A {StaticResource} with no matching x:Key in the same file.
    [TestClass]
    public class XamlResourceTests
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        // XAML base types that markup cannot instantiate: a resource or object element
        // has to name a derived, activatable type instead.
        private static readonly string[] NotCreatableTypes =
        {
            "Geometry",
            "Brush",
            "Transform",
            "Shape",
            "PathSegment",
            "ImageSource",
            "Timeline",
            "EasingFunctionBase"
        };

        private static string[] XamlFiles()
        {
            return Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml");
        }

        [TestMethod]
        public void No_XAML_element_uses_a_type_that_XAML_cannot_create()
        {
            var files = XamlFiles();
            Assert.IsTrue(files.Length >= 6, "expected the page XAML files, found " + files.Length.ToString());
            foreach (var file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string typeName in NotCreatableTypes)
                {
                    // "<Geometry ", "<Geometry>" or "<Geometry/>", but not a property
                    // element such as <Geometry.Transform>.
                    var m = Regex.Match(text, "<\\s*" + typeName + "(?![A-Za-z.])");
                    Assert.IsFalse(m.Success,
                        Path.GetFileName(file) + " creates <" + typeName + "> as an object element; use a derived type (for a path, PathGeometry or the Path's own Data attribute)");
                }
            }
        }

        [TestMethod]
        public void Every_ApplicationBar_item_declared_in_XAML_has_text()
        {
            // The bar is built before the page's constructor body runs, and an item
            // rejects an empty Text right there: the whole page then fails to load. An
            // item whose text is localized has to be added in code instead.
            foreach (var file in XamlFiles())
            {
                string text = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(text, "<shell:ApplicationBar(MenuItem|Button)\\b[^>]*>"))
                {
                    var value = Regex.Match(m.Value, "Text=\"([^\"]*)\"");
                    Assert.IsTrue(value.Success && value.Groups[1].Value.Trim().Length > 0,
                        Path.GetFileName(file) + " declares an application bar item without Text; add it in code with the localized string");
                }
            }
        }

        [TestMethod]
        public void Every_StaticResource_has_an_xKey_in_the_same_file()
        {
            // Windows Phone supplies its theme brushes/fonts (Phone* keys) at runtime;
            // only app-local resources need a matching x:Key in the same file.
            foreach (var file in XamlFiles())
            {
                string text = File.ReadAllText(file);
                var defined = Regex.Matches(text, "x:Key=\"([^\"]+)\"").Cast<Match>()
                                 .Select(m => m.Groups[1].Value).ToList();
                foreach (Match m in Regex.Matches(text, "StaticResource\\s+([^}\\s]+)"))
                {
                    string key = m.Groups[1].Value;
                    if (key.StartsWith("Phone", StringComparison.Ordinal)) continue;
                    Assert.IsTrue(defined.Contains(key),
                        Path.GetFileName(file) + " uses {StaticResource " + key + "} but defines no x:Key for it");
                }
            }
        }
    }
}
