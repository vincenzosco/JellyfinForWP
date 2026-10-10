# Jellyfin WP8.1 — Local-Server Tests, Autodiscovery and GitHub Repo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add integration tests that exercise the app's API layer against the real local Jellyfin server, add UDP autodiscovery of the server when the app opens, and publish the project to a private GitHub repo `JellyfinForWP`.

**Architecture:** Integration tests run in the *existing* desktop MSTest project (`JellyfinPlayer.Core.Tests`), which already links `Shared/*.vb`; they use the real `HttpTransport` instead of `FakeTransport` and read their connection settings from environment variables or a git-ignored local file. Autodiscovery adds a pure, unit-tested response parser in `Shared/` plus a WinRT `DatagramSocket` probe in the app, wired into `MainPage` so the server field is filled automatically.

**Tech Stack:** VB.NET, WP8.1 WinRT, VS2013 / MSBuild 12, MSTest (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`), `Windows.Networking.Sockets.DatagramSocket`, `gh` CLI 2.102.0.

**Spec:** the user request captured in this conversation (no separate spec file). Confirmed inputs: server `http://192.168.0.108:8096`, user `vincenzo`, password given in chat, private repo `JellyfinForWP`.

## Global Constraints

- **Do not add** any NuGet package or DTO field that changes an existing interface. Everything builds with a bare `msbuild` (no restore).
- Build: `"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal` (use `-p:`, never `/p:`).
- Unit tests: `"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll`. Integration tests are additionally selected with `/category:Integration`.
- **Server test settings (exact values):** URL `http://192.168.0.108:8096`, user `vincenzo`, password `<PASSWORD>`. The default URL and user may live in code; **the password must never appear in a tracked file** — it comes from `JELLYFIN_TEST_PASSWORD` or the git-ignored `test.settings.local`.
- Integration tests carry `[TestCategory("Integration")]` and are read-only (no `SetPlayed`/`SetFavorite`/progress posts) so they never mutate the server.
- Jellyfin autodiscovery: UDP **port 7359**, probe payload `who is JellyfinServer?`, response JSON `{"Address":"...","Id":"...","Name":"..."}`.
- `Package.appxmanifest` must declare `privateNetworkClientServer` (LAN/UDP is blocked without it).
- GitHub repo: **private**, name `JellyfinForWP`, owner `vincenzosco`, default branch `main`.
- VB rules already in force: `Namespace Global.JellyfinPlayer.Shared` for `Shared/`; app services are `JellyfinPlayer.Services`; no VB14 features; avoid VB keywords as identifiers.

## Review Focus

Failure modes a real user hits that these tests do not fully exercise. Each is pinned by a check in its owning task.

1. **Nothing discoverable / server offline at launch** → the app must still show the manual URL field and stay usable (Task 6 check).
2. **Wrong credentials in the integration config** → tests must fail with a clear 401 message, not a timeout (Task 2 `Login_...` test).
3. **Empty library / no video** → the playback integration test must skip cleanly rather than fail (Task 3 `Inconclusive` guard).
4. **Several Jellyfin servers on the LAN** → discovery must not silently pick an arbitrary one: one result auto-fills, several are counted and listed in the status text (Task 4 multi-response test, Task 6 check).
5. **Password leaking into the repository** → the private repo must contain no password (Task 8 leak check before `gh repo create`).

---

### Task 1: Test server settings + git-ignored local config

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/TestServerSettings.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/TestServerSettingsTests.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local.example`
- Modify: `.gitignore`
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj`

**Interfaces:**
- Consumes: nothing.
- Produces: `Public Class TestServerSettings` (in the test project's root namespace) with properties `Url As String`, `User As String`, `Password As String`; `Shared Function Load() As TestServerSettings`; `Shared Function ParseSettings(text As String) As Dictionary(Of String, String)`; `ReadOnly Property IsConfigured As Boolean` (all three non-empty). `Load()` precedence: environment (`JELLYFIN_TEST_URL`, `JELLYFIN_TEST_USER`, `JELLYFIN_TEST_PASSWORD`) → file at `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.settings.local")` if it exists → defaults `http://192.168.0.108:8096` / `vincenzo` / `""`.

- [ ] **Step 1: Write the failing test `TestServerSettingsTests.vb`**

`ParseSettings` — `"# c\nurl=http://h:8096\nuser=bob\npassword=p w\n"` yields `url`→`http://h:8096`, `user`→`bob`, `password`→`p w`; keys are lower-cased; `#` lines and blank lines are ignored; a line without `=` is ignored. `Load()` with no env vars and no file returns `Url = "http://192.168.0.108:8096"`, `User = "vincenzo"`, `IsConfigured = False`.

- [ ] **Step 2: Run the test to verify it fails**

Run the unit-test command. Expected: build error — `TestServerSettings` not defined.

- [ ] **Step 3: Implement `TestServerSettings.vb`** as specified above (`ParseSettings` splits on the first `=`, trims, skips `#`/blank lines; `Load` is as in Interfaces).

- [ ] **Step 4: Add the git-ignored config and the example**

`.gitignore`: append `test.settings.local`. `test.settings.local.example` holds the three keys with a placeholder password. `JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj` gains:

```xml
<ItemGroup>
  <None Include="test.settings.local" Condition="Exists('test.settings.local')">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the unit-test command. Expected: all tests pass (existing 36 + the new ones).

- [ ] **Step 6: Verify mstest's behaviour on an unconfigured run**

Run the unit-test command with no `test.settings.local` and no env vars. Record in `agents/TESTING.md` (Task 7) whether `Inconclusive` leaves the exit code at 0. If it does not, document that the integration subset is run with `/category:Integration` only.

- [ ] **Step 7: Commit**

```bash
git add .gitignore JellyfinPlayer/JellyfinPlayer.Core.Tests
git commit -m "test: add configurable integration-test settings"
```

---

### Task 2: Integration tests — reachability, auth, browsing, search

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/LocalServerIntegrationTests.vb`

**Interfaces:**
- Consumes: `TestServerSettings` (Task 1), `HttpTransport`, `JellyfinUrls`, `JellyfinApiClient`, `JellyfinException` (existing `Shared/`).
- Produces: `[TestCategory("Integration")]` class `LocalServerIntegrationTests`; a private `MakeClient() As JellyfinApiClient` using `New HttpTransport()` and device id `integration-test`; `Private Function RequireConfigured() As Boolean` returning `False` (after `Assert.Inconclusive("Jellyfin test server not configured.")`) when `Not _settings.IsConfigured`.

- [ ] **Step 1: Write the failing tests**

Each test starts with `If Not RequireConfigured() Then Return`. Assertions:
- `Server_is_reachable_and_identifies_itself` — `GetServerInfoAsync().GetAwaiter().GetResult().Id` is non-empty.
- `Login_with_configured_credentials_returns_a_token` — `AuthenticateAsync(user, password)`; on `JellyfinException` → `Assert.Fail("Authentication failed (HTTP " & ex.StatusCode & "): " & ex.Message)`; otherwise `client.AccessToken` and `client.UserId` are non-empty.
- `Views_are_readable` — after auth, `GetViewsAsync(client.UserId)` is not `Nothing` (may be empty for an empty server).
- `Items_load_for_the_first_library` — if a view exists, `GetItemsAsync(userId, view.Id, "Movie,Series,Episode,Video,BoxSet", 0, 5)` returns a non-`Nothing` `Items` and `TotalRecordCount >= 0`; if no view exists, `Assert.Inconclusive`.
- `Search_returns_results_for_an_existing_item_name` — if an item exists, `SearchAsync(userId, item.Name)` returns at least one item; else `Assert.Inconclusive`.

- [ ] **Step 2: Run the tests to observe the pre-configuration state**

Run the `/category:Integration` command. Expected: every integration test reports `Inconclusive` ("Jellyfin test server not configured.") because `test.settings.local` does not exist yet. That is the failing state Step 4 removes; these tests assert against a live server, so they cannot have a compile-time RED.

- [ ] **Step 3: Implement the test class** as specified. Use `GetAwaiter().GetResult()` (MSTest v1 does not run `Async Task` methods reliably); never log the password.

- [ ] **Step 4: Create the git-ignored local settings and run the integration tests**

Create `JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local` with the exact values from Global Constraints. Run:

```
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll /category:Integration
```

Expected: all integration tests pass against `192.168.0.108`. If the server is unreachable, the run reports the mapped timeout message — record the outcome honestly rather than weakening the asserts.

- [ ] **Step 5: Confirm the password is not tracked**

Run `git grep -n <PASSWORD>` at the repo root. Expected: no match (exit 1).

- [ ] **Step 6: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer.Core.Tests/LocalServerIntegrationTests.vb
git commit -m "test: add integration tests against the local Jellyfin server"
```

---

### Task 3: Integration tests — playback info and resume list

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/LocalServerIntegrationTests.vb`

**Interfaces:**
- Consumes: Task 2's class and `MakeClient`; `GetPlaybackInfoAsync`, `GetResumeAsync` (existing `Shared/`).
- Produces: two more `[TestMethod]`s in the same class. Playback info is called with `startTicks = 0`.

- [ ] **Step 1: Write the failing tests**

- `Playback_info_returns_a_playable_source_for_the_first_video` — iterate the views; for the first view, load up to 10 items with `IncludeItemTypes = "Movie,Episode,Video"`; take the first item; `GetPlaybackInfoAsync(userId, item.Id, 0)`; assert `MediaSources` is non-empty and that the first source has a non-empty `DirectStreamUrl` **or** `TranscodingUrl`. If no video item exists → `Assert.Inconclusive("No video item available to test playback info.")`.
- `Resume_list_is_readable` — `GetResumeAsync(userId, 5)` returns a non-`Nothing` list (empty is acceptable).

- [ ] **Step 2: Run the tests to verify they fail**

Run `/category:Integration`. Expected: PASS if a video exists, `Inconclusive` on an empty library. If it fails because no source is returned, that is a real finding about the device profile — report it, do not relax the assert.

- [ ] **Step 3: Implement** the two tests as specified.

- [ ] **Step 4: Run the integration tests to verify they pass**

Run `/category:Integration`. Expected: pass (or `Inconclusive` for the playback test on a library with no video — record which).

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer.Core.Tests/LocalServerIntegrationTests.vb
git commit -m "test: add playback-info integration coverage"
```

---

### Task 4: Discovery response parsing (shared, pure)

**Files:**
- Create: `JellyfinPlayer/Shared/ServerDiscoveryParser.vb`
- Modify: `JellyfinPlayer/Shared/Models.vb` (add `DiscoveredServerDto`)
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/ServerDiscoveryParserTests.vb`

**Interfaces:**
- Consumes: `Json` (existing).
- Produces: `Namespace Global.JellyfinPlayer.Shared`; `<DataContract> Public Class DiscoveredServerDto` with `Address As String`, `Id As String`, `Name As String`; `Public Module ServerDiscoveryParser` with `Const ProbePayload As String = "who is JellyfinServer?"`, `Const DiscoveryPort As Integer = 7359`, `Function TryParse(json As String, ByRef server As DiscoveredServerDto) As Boolean` (returns `False` and leaves `server` as `Nothing` for malformed JSON or a blank/absent `Address`).

- [ ] **Step 1: Write the failing test `ServerDiscoveryParserTests.vb`**

- valid: `{"Address":"http://192.168.0.108:8096","Id":"abc","Name":"Home"}` → `True`, fields match.
- extra keys: `{"Address":"http://h:8096","Id":"i","Name":"n","Extra":1}` → `True` (unknown keys ignored).
- missing address: `{"Id":"i","Name":"n"}` → `False`.
- malformed: `"not json"` → `False` (no exception).
- empty string `""` → `False`.

- [ ] **Step 2: Run the test to verify it fails**

Run the unit-test command. Expected: build error — `ServerDiscoveryParser` not defined.

- [ ] **Step 3: Implement** `DiscoveredServerDto` and `ServerDiscoveryParser` (wrap `Json.Deserialize(Of DiscoveredServerDto)` in `Try/Catch`, return `False` on any failure).

- [ ] **Step 4: Run the tests to verify they pass**

Run the unit-test command. Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/Shared/ServerDiscoveryParser.vb JellyfinPlayer/Shared/Models.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/ServerDiscoveryParserTests.vb
git commit -m "feat: add Jellyfin discovery response parser"
```

---

### Task 5: WinRT UDP discovery service + LAN capability

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Services/UdpServerDiscovery.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Package.appxmanifest`

**Interfaces:**
- Consumes: `ServerDiscoveryParser` (Task 4).
- Produces: `Namespace Services` (i.e. `JellyfinPlayer.Services`); `Public Module UdpServerDiscovery` with `Function DiscoverAsync(timeoutMs As Integer) As Task(Of List(Of DiscoveredServerDto))`. Behaviour: bind a `DatagramSocket`, wire `MessageReceived` to read the datagram as UTF-8, send `ProbePayload` to `255.255.255.255:7359`, collect parsed responses until the timeout elapses, then close the socket; never throws — returns an empty list when the network is unavailable.

- [ ] **Step 1: Add the capability to `Package.appxmanifest`**

Add `<Capability Name="privateNetworkClientServer" />` inside the existing `<Capabilities>` element (next to `internetClientServer`).

- [ ] **Step 2: Implement `UdpServerDiscovery.vb`**

Use `Windows.Networking.Sockets.DatagramSocket`, `Windows.Networking.HostName("255.255.255.255")`, `socket.MessageReceived` + `DataReader` (`InputStreamOptions.Partial`) to read the payload, and `Task.Delay(timeoutMs)` for the collection window. Guard every socket call in `Try/Catch` and always `Close()` in a `Finally`. This file is WinRT-only, so it is verified by compilation, not MSTest.

- [ ] **Step 3: Build to verify it compiles**

Run the build command. Expected: `Your package has been successfully created.`

- [ ] **Step 4: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Services/UdpServerDiscovery.vb JellyfinPlayer/JellyfinPlayer/Package.appxmanifest
git commit -m "feat: add UDP Jellyfin server discovery"
```

---

### Task 6: Auto-detect the server when the app opens

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml` (add a `DiscoveryStatus` `TextBlock`)
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb`

**Interfaces:**
- Consumes: `UdpServerDiscovery.DiscoverAsync` (Task 5), `AppSession` (existing).
- Produces: no new public API; `MainPage` fills `ServerBox` automatically.

- [ ] **Step 1: Add a status `TextBlock`** `x:Name="DiscoveryStatus"` under the server field in `MainPage.xaml`.

- [ ] **Step 2: Wire discovery into `OnNavigatedTo`**

If `AppSession.Current.BaseUrl` is empty, show `"Looking for a Jellyfin server…"`, `Await UdpServerDiscovery.DiscoverAsync(2500)`, then: 1 result → set `ServerBox.Text` to `result(0).Address` and the status to the server `Name`; more than 1 → set `ServerBox.Text` to the first `Address` and the status to `count & " servers found — using " & first.Name`; 0 results → status `"No server found automatically. Enter the address."` and leave the field editable. Never block the UI thread; wrap in `Try/Catch`. When a saved `BaseUrl` exists, skip discovery and show `"Using saved server."`.

- [ ] **Step 3: Build to verify it compiles**

Run the build command. Expected: success.

- [ ] **Step 4: Manual check on the emulator (record the result)**

On a network with the server running: opening the app fills the address within ~3 s. Review Focus #1: with the server stopped, the page shows the "No server found" message and manual entry still works. Review Focus #4: with two servers, the status reports the count (no silent wrong pick).

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/MainPage.xaml JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb
git commit -m "feat: auto-detect the Jellyfin server on launch"
```

---

### Task 7: Document testing and discovery

**Files:**
- Create: `agents/TESTING.md`
- Modify: `agents/AGENTS.md`, `agents/README.md`, `agents/MAINTENANCE.md`

**Interfaces:** documentation only.

- [ ] **Step 1: Write `agents/TESTING.md`** — the unit command, the `/category:Integration` command, how to configure `test.settings.local` / the three env vars, the mstest `Inconclusive` behaviour recorded in Task 1 Step 6, the statement that WP8.1 has no UI automation so app-level verification is the integration suite plus a manual emulator checklist, and the manual checklist (login, home, library paging, series, detail toggles, playback, settings, autodiscovery).

- [ ] **Step 2: Update the other docs** — `AGENTS.md` gains the integration-test command; `README.md` gains the autodiscovery and integration-test bullets; `MAINTENANCE.md`'s "Known limitations" drops any stale note and links `TESTING.md`.

- [ ] **Step 3: Verify the documented commands run** — execute the unit command and the `/category:Integration` command once each and confirm the stated results.

- [ ] **Step 4: Commit**

```bash
git add agents
git commit -m "docs: document integration testing and autodiscovery"
```

---

### Task 8: Leak check and private GitHub repo `JellyfinForWP`

**Files:**
- Modify: `.gitignore` (only if the leak check finds a gap)

**Interfaces:** no code.

> This task creates an external resource (a private repository) and pushes to it. The user explicitly requested it, which is the authorization to proceed.

- [ ] **Step 1: Leak check (must pass before creating the repo)**

Run, and require the expected results:
- `git check-ignore -v JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local` → prints the `.gitignore` rule (the file is ignored).
- `git grep -n <PASSWORD>` → no match (ignore files, `bin/`, `obj/`).
- `git status --porcelain` → clean.

If any check fails, fix `.gitignore` or remove the file from tracking first, then re-run all three.

- [ ] **Step 2: Standardise the default branch**

`git branch -M main` (the current branch is `jellyfin-client`; the new repo should default to `main`).

- [ ] **Step 3: Create the private repo and push**

```
gh repo create JellyfinForWP --private --source=. --remote=origin --push
```

Requires the authenticated account `vincenzosco` (verified: `gh auth status` shows `repo` scope). If `origin` already exists, use `gh repo create JellyfinForWP --private` then `git remote add origin`/`git push -u origin main`.

- [ ] **Step 4: Verify the result**

`gh repo view JellyfinForWP --json name,visibility,url,defaultBranchRef` → `"visibility":"PRIVATE"`, `defaultBranchRef.name` = `main`. Also confirm the pushed tree contains no `test.settings.local`: `gh api repos/vincenzosco/JellyfinForWP/contents/JellyfinPlayer/JellyfinPlayer.Core.Tests` and check the listing.

- [ ] **Step 5: Report** the repository URL.

---

## Self-Review

**1. Spec coverage**
- "test dell'app contro un server locale 192.168.0.108 / vincenzo / password" → Tasks 1–3 (config + reachability/auth/browse/search + playback), run explicitly against the real server.
- "se non c'è già, autodetect del server all'apertura" → confirmed absent today; Tasks 4–6 add parser + UDP probe + `MainPage` wiring, plus the `privateNetworkClientServer` capability.
- "repo privato GitHub `JellyfinForWP`" → Task 8, with a leak check as the first step.
No gap.

**2. Step scan** — every step names a file, a signature/assertion, an exact command, or an expected result. No "handle edge cases"/TBD lines. Task 5's body-level guidance exists only because `DatagramSocket` usage is not determined by the interface.

**3. Type consistency** — `DiscoveredServerDto` is the single name used by Tasks 4, 5, 6; `ServerDiscoveryParser.ProbePayload`/`DiscoveryPort` are defined in Task 4 and consumed in Task 5; `TestServerSettings.Load/ParseSettings/IsConfigured` are defined in Task 1 and consumed in Tasks 2–3. `JellyfinApiClient`/`JellyfinUrls`/`HttpTransport`/`JellyfinException` names match the shipped code.

**4. Review Focus** — #1 → Task 6 Step 4; #2 → Task 2 `Login_...`; #3 → Task 3 `Inconclusive` guard; #4 → Task 4 extra-keys/absent-address tests + Task 6 Step 4; #5 → Task 8 Step 1.

**5. Proportion** — the plan pins settings, endpoints, values and asserts; it contains no method bodies except Task 5's socket outline, which its signature and tests cannot determine.
