<TestClass>
Public Class TestServerSettingsTests
    <TestMethod>
    Public Sub ParseSettings_reads_keys_and_ignores_comments_and_blanks()
        Dim text = "# comment" & vbCrLf & "url=http://h:8096" & vbCrLf & "user=bob" & vbCrLf & "password=p w" & vbCrLf & vbCrLf & "garbage" & vbCrLf
        Dim d = TestServerSettings.ParseSettings(text)
        Assert.AreEqual("http://h:8096", d("url"))
        Assert.AreEqual("bob", d("user"))
        Assert.AreEqual("p w", d("password"))
        Assert.AreEqual(3, d.Count)
    End Sub

    <TestMethod>
    Public Sub ParseSettings_trims_key_whitespace()
        Dim d = TestServerSettings.ParseSettings("  URL = http://h  ")
        Assert.AreEqual("http://h", d("url"))
    End Sub

    <TestMethod>
    Public Sub IsConfigured_requires_all_three_values()
        Dim s As New TestServerSettings()
        s.Url = "http://h"
        s.User = "u"
        s.Password = ""
        Assert.IsFalse(s.IsConfigured)
        s.Password = "p"
        Assert.IsTrue(s.IsConfigured)
    End Sub

    <TestMethod>
    Public Sub Load_uses_the_local_defaults_when_the_environment_is_unset()
        If Environment.GetEnvironmentVariable("JELLYFIN_TEST_URL") IsNot Nothing Then Assert.Inconclusive("environment already configured")
        Dim s = TestServerSettings.Load()
        Assert.AreEqual("http://192.168.0.108:8096", s.Url)
        Assert.AreEqual("vincenzo", s.User)
    End Sub
End Class
