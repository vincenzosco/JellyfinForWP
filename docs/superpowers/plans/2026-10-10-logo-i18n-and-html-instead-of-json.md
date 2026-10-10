# Logo, EN/IT localisation and HTML-instead-of-JSON handling — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the Jellyfin logo left of the "Jellyfin" wordmark, make every user-visible string English or Italian following the device language (English default), give the repository an English default README plus an Italian one, and stop the app from printing a server's HTML page (or a raw `SerializationException`) when an address does not answer with Jellyfin JSON.

**Architecture:** Failures get a *classification* in the shared, WinRT-free layer (`JellyfinErrorKind`) and a *localized sentence* in the app layer, so the same classified exception drives both the user message and the tests. Localisation is the standard Windows resource pipeline (`.resw` + `x:Uid` + `ResourceLoader`), with the manifest declaring `en-US`/`it-IT`; automated tests compare the two resource files, the XAML and the code against each other so a half-translated UI cannot pass. Discovery keeps manual control: a UDP candidate is only written into the address field after `ValidateConnectionAsync` accepts it.

**Tech Stack:** VB.NET / WinRT XAML, Windows Phone 8.1, MSBuild 12 (VS2013), MSTest v1 (.NET 4.5), `.resw` resources, `Windows.ApplicationModel.Resources.ResourceLoader`.

**Spec:** `docs/superpowers/specs/2026-10-10-logo-i18n-and-html-instead-of-json.md`

## Global Constraints

- Language: VB.NET, `Option Strict Off` (project default), `Option Explicit On`. No new NuGet packages; no new references in the app project (`JellyfinPlayer.vbproj`).
- Only APIs present in the Windows Phone 8.1 metadata winmd
  (`C:\Program Files (x86)\Windows Phone Kits\8.1\References\CommonConfiguration\Neutral\Windows.winmd`).
  Verified present: `ResourceLoader`, `ResourceLoader.GetForViewIndependentUse`, `GetForCurrentView`,
  `Windows.ApplicationModel.Resources`, `ResourceManager`, `QualifierValues`, `ApplicationLanguages`,
  `PrimaryLanguageOverride`, `UserProfile.GlobalizationPreferences`. Verified **absent**:
  `ResourceManager.CreateDefaultContext` — do not use it.
- Everything under `JellyfinPlayer/Shared/` is linked into the test project, which has no Windows
  metadata and no WinRT: `Shared/` code must stay pure .NET 4.5 (no `Windows.*` types).
- VB is case-insensitive: never name a variable/parameter/type so that it collides with an imported
  type or module (`Path`, `Strings`, `Json`, `Text`). A module named `Strings` would collide with
  `Microsoft.VisualBasic.Strings`, hence `Localized`.
- Default language `en-US` (`<DefaultLanguage>` already set); supported languages are exactly
  `en-US` and `it-IT`.
- Test commands, both run from the repository root in bash:

```bash
# test project build
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj \
  -p:Configuration=Debug -p:Platform=AnyCPU -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# run (the testsettings keeps MSTest from copying the assembly away from test.settings.local)
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings
```

- App build: `"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal` must end with
  `Your package has been successfully created.` Use the `-p:` prefix, never `/p:`.
- Commits are Conventional Commits, one per task, no footer (repository style).
- Never commit `JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local` or the Jellyfin
  password. Integration tests never mutate server state.
- Baseline before this plan: 59 tests pass (52 unit + 7 integration) on the reference machine.

## Review Focus

1. **An address whose port answers with an HTML page** (another device on 7359, a web UI, a wrong
   port). Expected: a short sentence naming HTTP status and "web page, not the Jellyfin API" — never
   the page's markup, never a bare `SerializationException` message. Pinned by
   `FailurePathTests` and `ResponseFailuresTests` in Task 1.
2. **A device language that is neither English nor Italian** (e.g. `fr-FR`). Expected: a complete
   English UI, no blank or missing strings. Pinned by the manifest check and by the EN/IT key-parity
   check in Task 3 (an English default with no missing key in either file).
3. **A UI string present in English but not in Italian, or vice versa.** Expected: caught before it
   ships, not by a user. Pinned by
   `Italian_and_English_resources_have_the_same_keys` in Task 3.
4. **An untranslated string left behind after the localisation pass** (a hard-coded XAML literal or a
   `Text = ex.Message` left in code-behind). Expected: caught by
   `No_maintained_XAML_literal_text_remains` and `No_page_shows_a_raw_exception_message` in Task 3.
5. **Discovery finds a device on UDP 7359 that is not a Jellyfin server.** Expected: that address is
   never written into the address field; a message asks for manual entry. Pinned by
   `ServerProbeTests` in Task 4.

---

### Task 1: Classify and shorten server failures

**Files:**
- Create: `JellyfinPlayer/Shared/JellyfinErrorKind.vb`
- Create: `JellyfinPlayer/Shared/ResponseFailures.vb`
- Modify: `JellyfinPlayer/Shared/JellyfinException.vb`
- Modify: `JellyfinPlayer/Shared/HttpTransport.vb`
- Modify: `JellyfinPlayer/Shared/JellyfinApiClient.vb` (12 `Json.Deserialize` call sites: lines 41, 46, 62, 81, 86, 94, 99, 104, 109, 117, 122, 130)
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/ResponseFailuresTests.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/StubHttpServer.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/FailurePathTests.vb`

**Interfaces:**
- Produces: `JellyfinPlayer.Shared.JellyfinErrorKind` with members `Unknown = 0`, `HttpFailure`, `NonJsonResponse`, `Timeout`, `Unreachable`.
- Produces: `JellyfinException.New(message As String, statusCode As Integer, Optional kind As JellyfinErrorKind = JellyfinErrorKind.Unknown, Optional innerException As Exception = Nothing)` and `ReadOnly Property Kind As JellyfinErrorKind`.
- Produces: `ResponseFailures.LooksLikeHtml(body As String) As Boolean`,
  `ResponseFailures.DescribeHttpFailure(statusCode As Integer, contentType As String, body As String) As String`,
  `ResponseFailures.Parse(Of T)(body As String) As T`.
- Produces: `StubHttpServer` (test-only) with `New(statusLine As String, contentType As String, body As String)`, `ReadOnly Property BaseUrl As String`, `Implements IDisposable`.
- Consumed by: Task 3 (`Localized.DescribeError` switches on `Kind`), Task 4 (`ServerProbe` relies on the classified exceptions to skip a bad candidate).

- [ ] **Step 1: Write the failing pure tests**

`ResponseFailuresTests.vb`:

```vb
<TestClass>
Public Class ResponseFailuresTests
    <TestMethod> Public Sub Html_bodies_are_detected()
        Assert.IsTrue(ResponseFailures.LooksLikeHtml("<!DOCTYPE html><html></html>"))
        Assert.IsTrue(ResponseFailures.LooksLikeHtml("  " & vbCrLf & "<html><body>nginx</body></html>"))
        Assert.IsTrue(ResponseFailures.LooksLikeHtml("<h1>404</h1>"))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml(""))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("  "))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("{}"))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("[1,2]"))
        Assert.IsFalse(ResponseFailures.LooksLikeHtml("null"))
    End Sub

    <TestMethod> Public Sub DescribeHttpFailure_does_not_include_the_html_page()
        Dim message = ResponseFailures.DescribeHttpFailure(500, "text/html; charset=utf-8",
            "<html><head><title>500 Internal Server Error</title></head><body>nginx/1.2</body></html>")
        Assert.AreEqual("The server answered HTTP 500. It returned a web page instead of the Jellyfin API - check the address and the port.", message)
        Assert.IsFalse(message.Contains("<"), message)
    End Sub

    <TestMethod> Public Sub DescribeHttpFailure_truncates_a_long_json_body()
        Dim message = ResponseFailures.DescribeHttpFailure(400, "application/json", New String("x"c, 500))
        Assert.IsTrue(message.StartsWith("The server answered HTTP 400."), message)
        Assert.IsTrue(message.Length < 200, message)
        Assert.IsTrue(message.EndsWith("..."), message)
    End Sub

    <TestMethod> Public Sub Parse_throws_a_classified_error_for_html()
        Try
            ResponseFailures.Parse(Of ServerInfoDto)("<!DOCTYPE html><html>hi</html>")
            Assert.Fail("expected a JellyfinException")
        Catch ex As JellyfinException
            Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message)
            Assert.IsFalse(ex.Message.Contains("<"), ex.Message)
        End Try
    End Sub

    <TestMethod> Public Sub Parse_returns_the_object_for_json()
        Dim info = ResponseFailures.Parse(Of ServerInfoDto)("{""ServerName"":""Home"",""Id"":""abc""}")
        Assert.AreEqual("Home", info.ServerName)
        Assert.AreEqual("abc", info.Id)
    End Sub

    <TestMethod> Public Sub MapException_classifies_timeout_unreachable_and_unknown()
        Assert.AreEqual(JellyfinErrorKind.Timeout, HttpTransport.MapException(New TaskCanceledException()).Kind)
        Assert.AreEqual(JellyfinErrorKind.Unreachable, HttpTransport.MapException(New HttpRequestException("dns")).Kind)
        Assert.IsTrue(HttpTransport.MapException(New HttpRequestException("dns")).Message.Contains("Could not reach"), "existing message kept")
        Assert.AreEqual(JellyfinErrorKind.Unknown, HttpTransport.MapException(New InvalidOperationException("?""")).Kind)
    End Sub
End Class
```

`StubHttpServer.vb` — a tiny loopback HTTP server, so the transport runs against a real socket
without needing admin rights (`HttpListener` would) and without touching the LAN:

```vb
Imports System
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading.Tasks

' Answers every request with one canned response. Test-only.
Public Class StubHttpServer
    Implements IDisposable

    Private ReadOnly _listener As TcpListener
    Private ReadOnly _response As Byte()
    Private ReadOnly _task As Task

    Public Sub New(statusLine As String, contentType As String, body As String)
        _response = Encoding.UTF8.GetBytes(
            "HTTP/1.1 " & statusLine & vbCrLf &
            "Content-Type: " & contentType & vbCrLf &
            "Content-Length: " & Encoding.UTF8.GetByteCount(body).ToString() & vbCrLf &
            "Connection: close" & vbCrLf & vbCrLf & body)
        _listener = New TcpListener(IPAddress.Loopback, 0)
        _listener.Start()
        Port = CType(_listener.LocalEndpoint, IPEndPoint).Port
        _task = Task.Run(AddressOf Serve)
    End Sub

    Public ReadOnly Property Port As Integer
    Public ReadOnly Property BaseUrl As String
        Get
            Return "http://127.0.0.1:" & Port.ToString()
        End Get
    End Property

    Private Sub Serve()
        Do While True
            Dim client As TcpClient = Nothing
            Try
                client = _listener.AcceptTcpClient()
                Using stream = client.GetStream()
                    ' Read the request headers, answer once, close.
                    Dim buffer(1023) As Byte
                    Dim text As New StringBuilder()
                    Do While Not text.ToString().Contains(vbCrLf & vbCrLf)
                        Dim read = stream.Read(buffer, 0, buffer.Length)
                        If read <= 0 Then Exit Do
                        text.Append(Encoding.UTF8.GetString(buffer, 0, read))
                    Loop
                    stream.Write(_response, 0, _response.Length)
                End Using
            Catch
                Exit Do
            Finally
                If client IsNot Nothing Then client.Close()
            End Try
        Loop
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Try
            _listener.Stop()
        Catch
        End Try
    End Sub
End Class
```

`FailurePathTests.vb` — reproduces the reported emulator failure end to end through the real
`HttpTransport`:

```vb
<TestClass>
Public Class FailurePathTests
    Private Function ClientFor(server As StubHttpServer) As JellyfinApiClient
        Return New JellyfinApiClient(New HttpTransport(), New JellyfinUrls(server.BaseUrl), "test-device")
    End Function

    <TestMethod> Public Sub Html_page_on_a_successful_response_is_classified_as_not_json()
        Using server As New StubHttpServer("200 OK", "text/html; charset=utf-8", "<!DOCTYPE html><html><body>nginx</body></html>")
            Try
                ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(JellyfinErrorKind.NonJsonResponse, ex.Kind, ex.Message)
                Assert.IsFalse(ex.Message.Contains("<"), ex.Message)
                Assert.IsTrue(ex.Message.Length < 200, ex.Message)
            End Try
        End Using
    End Sub

    <TestMethod> Public Sub Html_page_on_a_failed_response_is_classified_as_an_http_failure()
        Using server As New StubHttpServer("404 Not Found", "text/html", "<html><body>Not Found</body></html>")
            Try
                ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind, ex.Message)
                Assert.AreEqual(404, ex.StatusCode)
                Assert.IsTrue(ex.Message.Contains("HTTP 404"), ex.Message)
                Assert.IsFalse(ex.Message.Contains("<"), ex.Message)
            End Try
        End Using
    End Sub

    <TestMethod> Public Sub Json_error_detail_is_kept_short()
        Using server As New StubHttpServer("500 Internal Server Error", "application/json", "{""Message"":""disk full""}")
            Try
                ClientFor(server).GetServerInfoAsync().GetAwaiter().GetResult()
                Assert.Fail("expected a JellyfinException")
            Catch ex As JellyfinException
                Assert.AreEqual(JellyfinErrorKind.HttpFailure, ex.Kind, ex.Message)
                Assert.IsTrue(ex.Message.Contains("disk full"), ex.Message)
            End Try
        End Using
    End Sub

    <TestMethod> Public Sub Closed_port_is_classified_as_unreachable()
        Try
            New JellyfinApiClient(New HttpTransport(), New JellyfinUrls("http://127.0.0.1:9"), "test-device") _
                .GetServerInfoAsync().GetAwaiter().GetResult()
            Assert.Fail("expected a JellyfinException")
        Catch ex As JellyfinException
            Assert.AreEqual(JellyfinErrorKind.Unreachable, ex.Kind, ex.Message)
        End Try
    End Sub
End Class
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the two commands in Global Constraints.
Expected: compile error — `JellyfinErrorKind`, `ResponseFailures` and `StubHttpServer` do not exist.

- [ ] **Step 3: Add the classification**

`JellyfinPlayer/Shared/JellyfinErrorKind.vb`:

```vb
Namespace Global.JellyfinPlayer.Shared
    ' Why a request failed, so the app can pick a localized sentence without
    ' inspecting the message. Unknown = 0 keeps every existing 2-argument
    ' JellyfinException call site meaning "unspecified".
    Public Enum JellyfinErrorKind As Integer
        Unknown = 0
        HttpFailure = 1
        NonJsonResponse = 2
        Timeout = 3
        Unreachable = 4
    End Enum
End Namespace
```

`JellyfinException.vb`: keep `New(message, statusCode)` working; add the two optional parameters and
the `Kind` property. Take the `kind` before the `innerException` parameter.

- [ ] **Step 4: Add the failure descriptions**

`JellyfinPlayer/Shared/ResponseFailures.vb` — `LooksLikeHtml` is
`body.TrimStart().StartsWith("<", StringComparison.Ordinal)` on a null-safe body.
`DescribeHttpFailure` returns `"The server answered HTTP " & statusCode & "."`, then appends
`" It returned a web page instead of the Jellyfin API - check the address and the port."` when
`LooksLikeHtml(body)` **or** the content type contains `html` (ordinal, case-insensitive);
otherwise, when the body is not blank, appends a single-collapsed snippet of the body truncated to
120 characters and ending in `"..."` when truncated. `Parse(Of T)` throws
`New JellyfinException("The address answered with a web page instead of Jellyfin JSON.", 0, JellyfinErrorKind.NonJsonResponse)`
when `LooksLikeHtml(body)`, and otherwise wraps `Json.Deserialize(Of T)` in `Try/Catch ex As Exception`
re-throwing the same way (with `ex` as the inner exception) so a `SerializationException` never
escapes. Keep `Json.Deserialize` itself unchanged — `ServerDiscoveryParser.TryParse` relies on it
throwing for malformed datagrams.

- [ ] **Step 5: Use them in the transport and the client**

`HttpTransport.SendAsync`: read
`Dim contentType = If(response.Content.Headers.ContentType Is Nothing, "", response.Content.Headers.ContentType.MediaType)`
before the status check, and replace the non-2xx throw with
`Throw New JellyfinException(ResponseFailures.DescribeHttpFailure(CInt(response.StatusCode), contentType, body), CInt(response.StatusCode), JellyfinErrorKind.HttpFailure)`.
`MapException`: add `, JellyfinErrorKind.Timeout` to the timeout branch, `, JellyfinErrorKind.Unreachable`
to the `HttpRequestException` branch; the messages stay exactly as they are (they are asserted by
`AuthTests`). In `JellyfinApiClient.vb`, replace all 12 `Json.Deserialize(Of X)(body)` calls with
`ResponseFailures.Parse(Of X)(body)`.

- [ ] **Step 6: Run the tests to verify they pass**

Run both commands. Expected: `N/N test(s) Passed` with no failures; the new tests are listed as
Passed. The 7 integration tests stay green or `Inconclusive` (no server) — never Failed.

- [ ] **Step 7: Build the app and commit**

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
git add JellyfinPlayer/Shared JellyfinPlayer/JellyfinPlayer.Core.Tests
git commit -m "fix: report a non-Jellyfin answer as a short classified error"
```

---

### Task 2: Logo left of the wordmark

**Files:**
- Modify: `tools/generate_assets.py:17-24` (TARGETS) and `main()` (asset loop)
- Create (generated): `JellyfinPlayer/JellyfinPlayer/Assets/logo.png`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj:111-116` (Content group)
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml:13`
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml:17`

**Interfaces:**
- Produces: `Assets/logo.png`, 144x144 RGBA, **unqualified** (no `.scale-*` variant), so it loads on every scale instead of falling back to a missing candidate. Displayed at 44x44 (login) and 34x34 (home header), which stays crisp up to 240 scale (44 * 2.4 = 105.6 < 144).
- Consumes: nothing from Task 1.

- [ ] **Step 1: Add the UI asset target**

In `TARGETS` append `("logo.png", 144, 144, "ui")`, and in the `main()` loop add a branch before the
generic one:

```python
        elif background == "ui":
            # Inline UI mark: fill the canvas, no tile-style padding.
            canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            place(canvas, fit(logo, w, h, 0.92))
```

- [ ] **Step 2: Generate it and verify the file**

```bash
python tools/generate_assets.py
python -c "from PIL import Image; i=Image.open('JellyfinPlayer/JellyfinPlayer/Assets/logo.png'); print(i.size, i.mode, i.getchannel('A').getbbox())"
```

Expected: `wrote .../logo.png (144, 144)`, then `(144, 144) RGBA` and a non-empty alpha bounding box
(the glyph is not an empty canvas). The generator rewrites the other six assets as well: if
`git status` lists any of them as modified, restore them (`git checkout -- <file>`) — the tile and
splash art is out of scope here and `logo.png` must be the only new file.

- [ ] **Step 3: Ship it with the app**

Add `<Content Include="Assets\logo.png" />` to the `Content` `ItemGroup` in
`JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`.

- [ ] **Step 4: Put it beside the title**

`MainPage.xaml` line 13 — replace the single `TextBlock` with:

```xml
            <StackPanel Orientation="Horizontal">
                <Image Source="ms-appx:///Assets/logo.png" Width="44" Height="44" Margin="0,0,12,0"/>
                <TextBlock Text="Jellyfin" FontSize="34" FontWeight="Bold" Foreground="#00A4DC" VerticalAlignment="Center"/>
            </StackPanel>
```

`HomePage.xaml` line 17 — same pattern inside the existing header `Grid` cell, `Width`/`Height` 34
and `Margin="0,0,10,0"`, `VerticalAlignment="Center"` on both. The "Jellyfin" text stays a literal:
it is the brand and is not translated.

- [ ] **Step 5: Build and commit**

Run the app build; expected `Your package has been successfully created.` and
`ls JellyfinPlayer/JellyfinPlayer/AppPackages/*_Test/AppX/Assets/logo.png` present.

```bash
git add tools/generate_assets.py JellyfinPlayer/JellyfinPlayer/Assets/logo.png JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj JellyfinPlayer/JellyfinPlayer/MainPage.xaml JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml
git commit -m "feat: show the Jellyfin logo next to the app title"
```

---

### Task 3: English + Italian user interface

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Strings/en-US/Resources.resw`
- Create: `JellyfinPlayer/JellyfinPlayer/Strings/it-IT/Resources.resw`
- Create: `JellyfinPlayer/JellyfinPlayer/Services/Localized.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj` (add `PRIResource` items)
- Modify: `JellyfinPlayer/JellyfinPlayer/Package.appxmanifest:21-23`
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml` and `MainPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml`, `LibraryPage.xaml`, `SeriesPage.xaml`, `ItemDetailPage.xaml`, `SearchPage.xaml`, `PlayerPage.xaml`, `SettingsPage.xaml`
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/*.xaml.vb` (all seven)
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/PlaybackService.vb`, `Services/PosterItem.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/LocalizationTests.vb`
- Modify: `agents/TESTING.md`, `agents/AGENTS.md`

**Interfaces:**
- Consumes: `JellyfinErrorKind`, `JellyfinException.Kind` (Task 1).
- Produces: `Services.Localized.Get(key As String) As String` and
  `Services.Localized.Format(key As String, ParamArray args As Object()) As String`,
  both backed by `Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse()`.
- Produces: `Services.Localized.DescribeError(ex As Exception) As String` — `JellyfinException` →
  `ErrorHttp` (with `ex.StatusCode`), `ErrorNotJson`, `ErrorTimeout`, `ErrorUnreachable`,
  `ErrorGeneric` (with `ex.Message`); any other exception → `ErrorGeneric` with `ex.Message`.
  A 401 keeps its special case at the call site (`StatusWrongCredentials`).

**Resource entries** — identical key sets in both files, in this order. Suffix `.Text`, `.Content`
or `.PlaceholderText` matches the element property `x:Uid` sets.

| Key | English | Italiano |
| --- | --- | --- |
| `ServerAddressLabel.Text` | Server address | Indirizzo del server |
| `UsernameLabel.Text` | Username | Nome utente |
| `PasswordLabel.Text` | Password | Password |
| `ConnectButton.Content` | Connect | Connetti |
| `SearchButton.Content` | Search | Cerca |
| `SettingsButton.Content` | Settings | Impostazioni |
| `ResumeHeader.Text` | Continue watching | Continua a guardare |
| `NextUpHeader.Text` | Next up | Prossimi episodi |
| `LibrariesHeader.Text` | Libraries | Librerie |
| `LoadMoreButton.Content` | Load more | Carica altri |
| `PlayButton.Content` | Play | Riproduci |
| `FavoriteToggle.Content` | Favorite | Preferito |
| `PlayedToggle.Content` | Watched | Visto |
| `PlayPauseButton.Content` | Pause | Pausa |
| `DirectButton.Content` | Play direct | Riproduci diretto |
| `StopButton.Content` | Stop | Interrompi |
| `QueryBox.PlaceholderText` | Search movies, series, episodes | Cerca film, serie, episodi |
| `EmptyText.Text` | Type and press Enter to search. | Digita e premi Invio per cercare. |
| `SettingsHeader.Text` | Settings | Impostazioni |
| `ServerLabel.Text` | Server | Server |
| `AccountLabel.Text` | Account | Account |
| `BitrateLabel.Text` | Max streaming bitrate | Bitrate massimo in streaming |
| `SignOutButton.Content` | Sign out | Esci |
| `StatusUsingSavedServer` | Using saved server. | Uso il server salvato. |
| `StatusEnterAddress` | Enter the server address. | Inserisci l'indirizzo del server. |
| `StatusWrongCredentials` | Wrong username or password. | Nome utente o password errati. |
| `StatusNotJellyfin` | That address is not a Jellyfin server. | Quell'indirizzo non è un server Jellyfin. |
| `DiscoverySearching` | Looking for a Jellyfin server... | Ricerca di un server Jellyfin... |
| `DiscoveryNone` | No server found automatically. Enter the address. | Nessun server trovato automaticamente. Inserisci l'indirizzo. |
| `DiscoveryFound` | Found {0}. | Trovato {0}. |
| `DiscoveryFoundMany` | {0} servers found; using {1}. | Trovati {0} server; uso {1}. |
| `DiscoveryRejected` | The detected address is not a Jellyfin server. Enter the address. | L'indirizzo trovato non è un server Jellyfin. Inserisci l'indirizzo. |
| `DiscoveryFailed` | Automatic detection failed: {0} | Rilevamento automatico non riuscito: {0} |
| `ErrorHttp` | The server answered HTTP {0}. | Il server ha risposto HTTP {0}. |
| `ErrorNotJson` | The address answered with a web page instead of the Jellyfin API. Check the address and the port (usually 8096). | L'indirizzo ha risposto con una pagina web invece dell'API Jellyfin. Controlla indirizzo e porta (di solito 8096). |
| `ErrorTimeout` | The server did not respond in time. Check the address and that the phone is on the same network. | Il server non ha risposto in tempo. Controlla l'indirizzo e che il telefono sia sulla stessa rete. |
| `ErrorUnreachable` | Could not reach the server. Check the address and that the phone is on the same Wi-Fi. | Impossibile raggiungere il server. Controlla l'indirizzo e che il telefono sia sulla stessa Wi-Fi. |
| `ErrorGeneric` | Unexpected error: {0} | Errore imprevisto: {0} |
| `Searching` | Searching... | Ricerca... |
| `NoResults` | No results. | Nessun risultato. |
| `Saved` | Saved. | Salvato. |
| `NotSignedIn` | (not signed in) | (accesso non effettuato) |
| `MbpsSuffix` | Mbps | Mbps |
| `LibraryTitle` | Library | Libreria |
| `SeasonPrefix` | S | S |
| `EpisodeLabel` | Episode | Episodio |
| `ResumeAction` | Resume | Riprendi |
| `PlayAction` | Play | Riproduci |
| `PauseAction` | Pause | Pausa |
| `PlaybackFailed` | Playback failed: {0} | Riproduzione non riuscita: {0} |
| `UnknownError` | unknown error | errore sconosciuto |
| `PlaybackNoDirectStream` | No direct stream is available. | Nessuno stream diretto disponibile. |
| `PlaybackUnavailable` | This video cannot be played on this device. | Questo video non può essere riprodotto su questo dispositivo. |
| `PlaybackNoSource` | This video cannot be played on this device (no playable or transcodable source). | Questo video non può essere riprodotto su questo dispositivo (nessuna sorgente riproducibile o transcodificabile). |

A `.resw` entry looks like this; `resource.resw` files are UTF-8 without BOM:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence><xsd:element name="value" type="xsd:string" minOccurs="0" /></xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence><xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" /></xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="ConnectButton.Content" xml:space="preserve">
    <value>Connect</value>
  </data>
</root>
```

- [ ] **Step 1: Write the failing localisation tests**

In `JellyfinPlayer.Core.Tests.vbproj` add, so the tests can read the real files (never copy them by
hand):

```xml
    <Reference Include="System.Xml.Linq" />
```
in the references `ItemGroup`, and in a new `ItemGroup`:

```xml
    <None Include="..\JellyfinPlayer\Strings\en-US\Resources.resw" Link="Localization\en-US\Resources.resw">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\Strings\it-IT\Resources.resw" Link="Localization\it-IT\Resources.resw">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\MainPage.xaml" Link="Xaml\MainPage.xaml">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\Pages\*.xaml" Link="Xaml\%(Filename)%(Extension)">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\Package.appxmanifest" Link="Manifest\Package.appxmanifest">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\MainPage.xaml.vb" Link="App\MainPage.xaml.vb">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\Pages\*.xaml.vb" Link="App\%(Filename)%(Extension)">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\JellyfinPlayer\Services\*.vb" Link="App\%(Filename)%(Extension)">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
```

`LocalizationTests.vb` — every test reads from `AppDomain.CurrentDomain.BaseDirectory`
(deployment is disabled, so the copied files sit next to the assembly):

```vb
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

<TestClass>
Public Class LocalizationTests
    Private Shared ReadOnly BaseDir As String = AppDomain.CurrentDomain.BaseDirectory

    Private Shared Function Resources(language As String) As Dictionary(Of String, String)
        Dim doc = XDocument.Load(Path.Combine(BaseDir, "Localization", language, "Resources.resw"))
        Return doc.Descendants("data").ToDictionary(
            Function(d) d.Attribute("name").Value,
            Function(d) d.Element("value").Value)
    End Function

    Private Shared ReadOnly En As Dictionary(Of String, String) = Resources("en-US")
    Private Shared ReadOnly It As Dictionary(Of String, String) = Resources("it-IT")

    Private Shared Function Placeholders(value As String) As String
        Return String.Join(",", Regex.Matches(value, "\{\d+\}").Cast(Of Match)().Select(Function(m) m.Value).Distinct().OrderBy(Function(s) s))
    End Function

    <TestMethod> Public Sub Italian_and_English_resources_have_the_same_keys()
        Dim missingInIt = En.Keys.Except(It.Keys).OrderBy(Function(k) k).ToArray()
        Dim missingInEn = It.Keys.Except(En.Keys).OrderBy(Function(k) k).ToArray()
        Assert.AreEqual(0, missingInIt.Length, "missing in it-IT: " & String.Join(", ", missingInIt))
        Assert.AreEqual(0, missingInEn.Length, "missing in en-US: " & String.Join(", ", missingInEn))
        Assert.IsTrue(En.Count > 40, "only " & En.Count.ToString() & " resources")
    End Sub

    <TestMethod> Public Sub No_resource_value_is_empty()
        For Each pair In {Tuple.Create("en-US", En), Tuple.Create("it-IT", It)}
            For Each entry In pair.Item2
                Assert.IsFalse(String.IsNullOrWhiteSpace(entry.Value), pair.Item1 & " empty: " & entry.Key)
            Next
        Next
    End Sub

    <TestMethod> Public Sub Format_placeholders_match_between_languages()
        For Each key In En.Keys
            Assert.AreEqual(Placeholders(En(key)), Placeholders(It(key)), "placeholder mismatch: " & key)
        Next
    End Sub

    <TestMethod> Public Sub Every_xUid_in_XAML_has_a_resource_in_both_languages()
        For Each file In Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml")
            For Each uid In Regex.Matches(File.ReadAllText(file), "x:Uid=""([^""]+)""").Cast(Of Match)().Select(Function(m) m.Groups(1).Value)
                Dim matching = En.Keys.Where(Function(k) k.StartsWith(uid & ".", StringComparison.Ordinal)).ToArray()
                Assert.IsTrue(matching.Length = 1,
                    Path.GetFileName(file) & ": x:Uid=" & uid & " needs exactly one of " & uid & ".Text/.Content/.PlaceholderText (found " & matching.Length.ToString() & ")")
                Assert.IsTrue(It.ContainsKey(matching(0)), "missing it-IT translation for " & matching(0))
            Next
        Next
    End Sub

    <TestMethod> Public Sub Every_key_used_from_code_exists()
        Dim used As New List(Of String)()
        For Each file In Directory.GetFiles(Path.Combine(BaseDir, "App"), "*.vb")
            For Each m In Regex.Matches(File.ReadAllText(file), "Localized\.(?:Get|Format)\(\s*""([^""]+)""").Cast(Of Match)()
                used.Add(m.Groups(1).Value)
            Next
        Next
        Assert.IsTrue(used.Count > 10, "expected localized call sites in the app, found " & used.Count.ToString())
        For Each key In used.Distinct()
            Assert.IsTrue(En.ContainsKey(key), "en-US is missing " & key)
            Assert.IsTrue(It.ContainsKey(key), "it-IT is missing " & key)
        Next
    End Sub

    <TestMethod> Public Sub Manifest_declares_english_and_italian()
        Dim manifest = File.ReadAllText(Path.Combine(BaseDir, "Manifest", "Package.appxmanifest"))
        Assert.IsTrue(manifest.Contains("<Resource Language=""en-US""/>"), manifest)
        Assert.IsTrue(manifest.Contains("<Resource Language=""it-IT""/>"), manifest)
        Assert.IsFalse(manifest.Contains("x-generate"), "declare the supported languages explicitly")
    End Sub

    <TestMethod> Public Sub No_page_shows_a_raw_exception_message()
        For Each file In Directory.GetFiles(Path.Combine(BaseDir, "App"), "*.vb")
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(file), "\.Text\s*=\s*ex\.Message"),
                Path.GetFileName(file) & " prints ex.Message directly; use Localized.DescribeError(ex)")
        Next
    End Sub

    <TestMethod> Public Sub No_maintained_XAML_literal_text_remains()
        ' Brand name and the example URL are deliberately not translated.
        Dim allowed = {"Jellyfin", "http://192.168.1.10:8096"}
        For Each file In Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml")
            For Each m In Regex.Matches(File.ReadAllText(file), "\s(?:Text|Content|PlaceholderText)=""([^""{][^""]*)""").Cast(Of Match)()
                Dim value = m.Groups(1).Value
                Assert.IsTrue(allowed.Contains(value),
                    Path.GetFileName(file) & ": literal " & value & " is not localized")
            Next
        Next
    End Sub
End Class
```

- [ ] **Step 2: Run the tests to verify they fail**

Run both commands. Expected: `FileNotFoundException` for `Localization\en-US\Resources.resw` in every
localisation test (the resw files do not exist yet), and the other tests still passing.

- [ ] **Step 3: Create the two resource files**

Write `Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw` with every key in the table
above (same order, same keys; the `<data name=...>` line is followed by a `<value>` with the entry).

- [ ] **Step 4: Wire the resource files and the manifest**

`JellyfinPlayer.vbproj` — new `ItemGroup`:

```xml
  <ItemGroup>
    <PRIResource Include="Strings\en-US\Resources.resw" />
    <PRIResource Include="Strings\it-IT\Resources.resw" />
  </ItemGroup>
```

`Package.appxmanifest` — replace `<Resource Language="x-generate"/>` with:

```xml
    <Resource Language="en-US"/>
    <Resource Language="it-IT"/>
```

- [ ] **Step 5: Add the accessor**

`JellyfinPlayer/Services/Localized.vb`: module `Localized` in `Namespace Services`, `Imports Windows.ApplicationModel.Resources`,
a `Private ReadOnly` `ResourceLoader.GetForViewIndependentUse()` field, `Get`, `Format`
(`String.Format(Get(key), args)`) and `DescribeError` as described in Interfaces. Do not name
anything `Strings`, `Text` or `Path`.

- [ ] **Step 6: Mark up the XAML**

For every entry in the table whose key ends in `.Text`, `.Content` or `.PlaceholderText`, put the
element's `x:Uid` equal to the key without the suffix, and **delete the literal attribute**. Elements
that need `x:Name` for the existing code-behind keep their `x:Name`; `x:Uid` and `x:Name` may be the
same string. Add `x:Name` where the code-behind or the table needs one and the element has none:
`MainPage.xaml` labels at lines 14/17/19 (`ServerAddressLabel`, `UsernameLabel`, `PasswordLabel`),
`HomePage.xaml` lines 19/20 (`SearchButton`, `SettingsButton`) and line 64 (`LibrariesHeader`),
`SettingsPage.xaml` lines 11/13/16/19 (`SettingsHeader`, `ServerLabel`, `AccountLabel`,
`BitrateLabel`) and the `Sign out` button (`SignOutButton`). `SeriesPage.xaml` has no literal text —
leave it unchanged.

- [ ] **Step 7: Route the code-behind strings through `Localized`**

Replace every literal from the table with `Localized.Get("<key>")` / `Localized.Format("<key>", args)`
in `MainPage.xaml.vb`, `Pages\*.xaml.vb`, `Services\PlaybackService.vb`, `Services\PosterItem.vb`.
Concatenations become format arguments: `"Found " & found(0).Name & "."` →
`Localized.Format("DiscoveryFound", found(0).Name)`; the "Also: " list becomes
`Localized.Format("DiscoveryFoundMany", found.Count, found(0).Name)` (drop the extra name list — the
count and the chosen name are what the user needs). Replace every
`Text = ex.Message` / `StatusText.Text = ex.Message` with `Localized.DescribeError(ex)`, and the
MainPage `JellyfinException` handler with: `If ex.StatusCode = 401 Then Localized.Get("StatusWrongCredentials") Else Localized.DescribeError(ex)`.
Keep the brand "Jellyfin" and the example-URL placeholder literal.

- [ ] **Step 8: Run the tests to verify they pass**

Run both commands. Expected: all 8 localisation tests Passed, everything else unchanged. If
`No_maintained_XAML_literal_text_remains` fails, the reported literal is a string this task missed —
localize it (add the key to **both** resw files).

- [ ] **Step 9: Build, document the manual checks, and commit**

Build the app (`Debug|x86`). Then in `agents/TESTING.md` add to the manual checklist: "launch with
the emulator language set to Italian → every screen reads Italian; set to French → every screen reads
English"; and in `agents/AGENTS.md` add the rule "user-visible strings live in
`Strings/<lang>/Resources.resw`; keep EN and IT in sync — `LocalizationTests` fails the build-time
check otherwise".

```bash
git add JellyfinPlayer/JellyfinPlayer JellyfinPlayer/JellyfinPlayer.Core.Tests agents
git commit -m "feat: localize the app in English and Italian"
```

---

### Task 4: Discovery only fills in a validated server

**Files:**
- Create: `JellyfinPlayer/Shared/ServerProbe.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/ServerProbeTests.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb:36-59`

**Interfaces:**
- Consumes: `DiscoveredServerDto` (`Address`, `Name`), `JellyfinApiClient.ValidateConnectionAsync()`, `Localized.Format`/`Get` (Task 3).
- Produces: `ServerProbe.FindFirstJellyfinAsync(candidates As IEnumerable(Of DiscoveredServerDto), createClient As Func(Of String, JellyfinApiClient)) As Task(Of DiscoveredServerDto)` — probes at most 3 candidates sequentially, returns the first whose address passes `ValidateConnectionAsync`, `Nothing` when none does.

- [ ] **Step 1: Write the failing test**

`ServerProbeTests.vb`:

```vb
<TestClass>
Public Class ServerProbeTests
    Private Shared Function Candidate(address As String) As DiscoveredServerDto
        Return New DiscoveredServerDto With {.Address = address, .Id = "id-" & address, .Name = "Home"}
    End Function

    ' HTML at the first address, Jellyfin JSON at the second: skip and take the second.
    <TestMethod> Public Sub Skips_an_address_that_answers_with_html()
        Dim transports As New Dictionary(Of String, FakeTransport)()
        Dim probe As New FakeTransport()
        probe.Handler = Function(url) If(url.StartsWith("http://127.0.0.1:9/"), "<html>nginx</html>", "{""Id"":""abc"",""ServerName"":""Home""}")
        transports("http://127.0.0.1:9") = probe

        Dim found = ServerProbe.FindFirstJellyfinAsync(
            {Candidate("http://127.0.0.1:9")},
            Function(baseUrl) New JellyfinApiClient(transports(baseUrl), New JellyfinUrls(baseUrl), "dev")).GetAwaiter().GetResult()

        Assert.IsNull(found, "an HTML answer must not validate as Jellyfin")
        Assert.IsTrue(probe.LastUrl.Contains("/System/Info/Public"), probe.LastUrl)
    End Sub

    <TestMethod> Public Sub Returns_the_first_validated_candidate()
        Dim good As New FakeTransport()
        good.Handler = Function(url) "{""Id"":""abc"",""ServerName"":""Home""}"
        Dim bad As New FakeTransport()
        bad.Handler = Function(url) "<html>nginx</html>"

        Dim clients = New Dictionary(Of String, FakeTransport)() From {
            {"http://192.168.0.50:8096", bad}, {"http://192.168.0.108:8096", good}}

        Dim found = ServerProbe.FindFirstJellyfinAsync(
            {Candidate("http://192.168.0.50:8096"), Candidate("http://192.168.0.108:8096")},
            Function(baseUrl) New JellyfinApiClient(clients(baseUrl), New JellyfinUrls(baseUrl), "dev")).GetAwaiter().GetResult()

        Assert.IsNotNull(found)
        Assert.AreEqual("http://192.168.0.108:8096", found.Address)
    End Sub

    <TestMethod> Public Sub An_id_less_answer_is_not_a_server()
        Dim empty As New FakeTransport()
        empty.Handler = Function(url) "{""ServerName"":""Router""}"

        Dim found = ServerProbe.FindFirstJellyfinAsync(
            {Candidate("http://192.168.0.50:8096")},
            Function(baseUrl) New JellyfinApiClient(empty, New JellyfinUrls(baseUrl), "dev")).GetAwaiter().GetResult()

        Assert.IsNull(found)
    End Sub

    <TestMethod> Public Sub A_failing_candidate_does_not_stop_the_probe()
        Dim dead As New FakeTransport()
        dead.ErrorToThrow = New JellyfinException("Could not reach the server", 0, JellyfinErrorKind.Unreachable)
        Dim good As New FakeTransport()
        good.Handler = Function(url) "{""Id"":""abc""}"

        Dim found = ServerProbe.FindFirstJellyfinAsync(
            {Candidate("http://192.168.0.50:8096"), Candidate("http://192.168.0.108:8096")},
            Function(baseUrl) New JellyfinApiClient(If(baseUrl.Contains("50"), dead, good), New JellyfinUrls(baseUrl), "dev")).GetAwaiter().GetResult()

        Assert.IsNotNull(found)
        Assert.AreEqual("http://192.168.0.108:8096", found.Address)
    End Sub
End Class
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: compile error — `ServerProbe` does not exist.

- [ ] **Step 3: Implement the probe**

`JellyfinPlayer/Shared/ServerProbe.vb`: module `ServerProbe` in
`Namespace Global.JellyfinPlayer.Shared`; iterate `candidates.Take(3)`, skip null/blank addresses,
`Await createClient(c.Address).ValidateConnectionAsync()` inside `Try/Catch`, return the candidate on
`True`, and return `Nothing` at the end. `Option Strict` is off in both projects, but keep the
signature exactly as in Interfaces.

- [ ] **Step 4: Run the tests to verify they pass**

Expected: 4 new tests Passed, nothing else changed.

- [ ] **Step 5: Use it from `MainPage`**

In `DiscoverAsync`, replace the direct `found(0).Address` assignment with a probe over the whole
candidate list, then fill the field and set the status only when a server validated:

```vb
            Dim session = AppSession.Current
            Dim server = Await ServerProbe.FindFirstJellyfinAsync(
                found, Function(baseUrl) New JellyfinApiClient(New HttpTransport(), New JellyfinUrls(baseUrl), session.DeviceId))

            If server Is Nothing Then
                DiscoveryStatus.Text = Localized.Get("DiscoveryRejected")
                Return
            End If

            ServerBox.Text = server.Address
            If found.Count = 1 Then
                DiscoveryStatus.Text = Localized.Format("DiscoveryFound", server.Name)
            Else
                DiscoveryStatus.Text = Localized.Format("DiscoveryFoundMany", found.Count, server.Name)
            End If
```

Keep the existing "no candidates" and `Catch` branches, with their messages from `Localized`. The
probe runs on the UI thread after the UDP window; do not increase the 2500 ms discovery timeout.

- [ ] **Step 6: Build, run the full suite, commit**

App build must succeed (`Debug|x86`); the full MSTest run must show no failures.

```bash
git add JellyfinPlayer/Shared/ServerProbe.vb JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/ServerProbeTests.vb
git commit -m "fix: only autofill an address that answers as Jellyfin"
```

---

### Task 5: Repository READMEs (English default, Italian)

**Files:**
- Create: `README.md`
- Create: `README.it.md`
- Modify: `agents/README.md` (add a one-line pointer to the root READMEs)

**Interfaces:**
- Consumes: the verified commands from `agents/BUILD.md` (copy them verbatim; do not invent new ones).
- Produces: nothing other tasks use.

- [ ] **Step 1: Write `README.md` (English, the default)**

Sections, in this order, each short:

1. Title `# JellyfinPlayer for Windows Phone 8.1` + one paragraph: a Jellyfin client for Windows
   Phone 8.1, VB.NET/WinRT XAML, built with Visual Studio 2013.
2. `## Features` — login, automatic server discovery (UDP 7359), home (continue watching, next up,
   libraries), paged library, series/seasons, item detail with favorite/watched, search, video
   player (direct play + transcoded MP4), settings. Note the language: English and Italian,
   following the device language.
3. `## Requirements` — Visual Studio 2013 + Windows Phone 8.1 SDK, a running Jellyfin server, and the
   note that the phone and the server must be on the same network.
4. `## Build` — the two msbuild commands from `agents/BUILD.md` (x86 Debug and ARM Release) and the
   expected last line `Your package has been successfully created.`
5. `## Tests` — the two test commands, the `test.settings.local` setup (copy the `.example`, fill the
   password, never commit it) and the pointer to `agents/TESTING.md`.
6. `## Documentation` — links to `agents/README.md`, `agents/BUILD.md`, `agents/TESTING.md`,
   `agents/MAINTENANCE.md` and to `README.it.md`.

- [ ] **Step 2: Write `README.it.md` (Italian)**

Same six sections in Italian, linking back to `README.md`. Keep commands, paths and identifiers
unchanged (they are literal), translate only the prose.

- [ ] **Step 3: Cross-link and verify**

Add `README.md` and `README.it.md` to `agents/README.md`'s index. Then verify the documented
commands really work from the repository root — run the README's build command and both test
commands; expected: `successfully created`, and `N/N test(s) Passed` with no failures. A command
that does not behave as the README says is a bug in the README: fix the README.

- [ ] **Step 4: Commit**

```bash
git add README.md README.it.md agents/README.md
git commit -m "docs: add English and Italian repository READMEs"
```

---

## Final verification (whole plan)

- [ ] Test project build + full MSTest run: `N/N test(s) Passed`, no `Failed`. Integration tests are
      green against `192.168.0.108` when `test.settings.local` exists, otherwise `Inconclusive`
      (MSTest v1 exits 1 for a run containing `Inconclusive` — that is not a failure of this work,
      but it must be reported as "not run against a server", never as passing).
- [ ] `Debug|x86` build and `Release|ARM` build both end with `Your package has been successfully created.`
- [ ] `git grep -n "<password>"` and `git log -S"<password>"` find nothing; `test.settings.local` is
      still git-ignored and untracked.
- [ ] `git status --short` clean.

## Verification the automated tests cannot do (no emulator here)

Recorded in `agents/TESTING.md` for the next manual run:

- Device language Italian → every screen is Italian, including error and discovery messages.
- Device language neither EN nor IT (e.g. French) → every screen is English.
- Login page and home header show the logo immediately left of the "Jellyfin" wordmark.
- Launch with no saved server on a LAN with Jellyfin running → the field fills within ~3 s.
- Launch with a non-Jellyfin device answering on UDP 7359 (or the server stopped) → the field is
  **not** filled with it and the message asks for manual entry.
- Point the app at an address whose port serves a web page (e.g. `http://192.168.0.108:80`) → a
  short sentence about a web page instead of the Jellyfin API, **not** HTML on screen.
