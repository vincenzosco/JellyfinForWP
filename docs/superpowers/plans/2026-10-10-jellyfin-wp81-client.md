# Jellyfin Windows Phone 8.1 Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working VB.NET Jellyfin client for Windows Phone 8.1 (WinRT XAML): connect/login, browse libraries (movies/series/seasons/episodes), search, item details, favorites/watched, and video playback (direct play + HLS transcode fallback), with the Jellyfin logo/splash applied and an `agents/` maintenance guide.

**Architecture:** A WP8.1 WinRT app project holds the XAML pages and WinRT-only services (playback via `AdaptiveMediaSource`, image loading, navigation). All platform-agnostic logic (DTOs, JSON, URL building, tick math, device profile, and a transport-injected API client) lives in a `Shared/` folder of `.vb` files that is *linked* into both the WP8.1 app and a desktop **MSTest** project, so the logic can be unit-tested with MSBuild 12.

**Tech Stack:** VB.NET; Windows Phone 8.1 WinRT XAML (`AppContainerExe`, `TargetPlatformVersion 8.1`); VS2013 / MSBuild 12; `System.Net.Http`; `System.Runtime.Serialization.Json` (DataContractJsonSerializer); `Windows.Media.Streaming.Adaptive` (HLS); MSTest (`Microsoft.VisualStudio.QualityTools.UnitTestFramework`); Python + Pillow for asset generation.

**Spec:** the user request captured in this conversation (no separate spec file). Confirmed decisions: **keep VB.NET**, **broad core client**, **direct play + HLS transcode fallback**.

**Image inspection (already done, OCR requirement):** the provided images were inspected with `pytesseract`. Tesseract was not installed, so a portable copy was extracted with 7-Zip from the UB-Mannheim 5.4.0 installer (no admin needed) and pointed at via `pytesseract.pytesseract.tesseract_cmd`. OCR returned no readable text, which proves the images are **logos, not UI pages**: `images.png` and `images(1).png` are the logo on a flat grey background, `proton.png.png` is an unrelated transparent logo, and **`images_1_-removebg-preview.png` (447x447, transparent, blue→purple gradient) is the remove.bg Jellyfin logo** used by Task 18. Reproduce with `python tools/generate_assets.py` (Task 18) and the OCR snippet is not needed again.

## Global Constraints

- Language: **VB.NET**. Follow the existing project options (`Option Strict Off`, `Option Explicit On`, `Option Infer On`); write explicit types anyway.
- Platform: Windows Phone 8.1 **WinRT XAML** app. Do not migrate to Silverlight or to a newer OS.
- Build with **VS2013 / MSBuild 12.0**. Build command (bash-safe, note the `-p:` form — `/p:` gets mangled by Git Bash path conversion):
  `"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal`
- **No NuGet packages and no package restore.** Framework-only APIs, so the solution builds with a bare `msbuild` invocation.
- Jellyfin **REST API v10**. Base URL is user-supplied, e.g. `http://192.168.1.10:8096`. Server JSON is PascalCase.
- Auth: `POST /Users/AuthenticateByName` carries header `X-Emby-Authorization: MediaBrowser Client="JellyfinPlayer", Device="WindowsPhone", DeviceId="<guid>", Version="1.0.0"` and returns `AccessToken`. All later calls carry header `X-Emby-Token: <token>`. **Image URLs** use query `api_key=<token>` (BitmapImage cannot set headers).
- Ticks: **1 second = 10,000,000 ticks**.
- Asset sizes (all `*.scale-240.png`): `Logo` 360x360, `SmallLogo` 106x106, `Square71x71Logo` 170x170, `WideLogo` 744x360, `StoreLogo` 120x120, `SplashScreen` 1152x1920. Tile background `#101517`, `ForegroundText` light.
- Tests run with: `"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll`
- UI text: English (matches existing template). Code comments: English.

## Review Focus

Failure modes a real user will hit that the happy-path tests do not exercise. Each one is pinned by a test/check in the owning task.

1. **Server unreachable / wrong URL / self-signed HTTPS** → must show a readable error and never hang (Task 5 tests the timeout+error mapping).
2. **Login failure vs. passwordless account** → must distinguish 401 (bad password, stay on page) from success and never persist or log the password/token (Task 5, Task 8).
3. **Unsupported codec when the server refuses to transcode** (transcoding disabled / bitrate 0) → player must surface an error and offer direct play, not a silent black screen (Task 16).
4. **Item with no Primary image / image 404** → placeholder shown, no layout crash or exception (Task 4 builds the URL; Task 15 handles the failure event).
5. **Very large library (>5000 items)** → list must page via `StartIndex`/`Limit` and stay responsive (Task 12).

---

### Task 1: Test project + `Shared/` scaffolding

**Files:**
- Create: `JellyfinPlayer/Shared/Placeholder.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/PlaceholderTests.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer.sln`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: nothing.
- Produces: `Shared/` linked into both projects; test runner wiring; the `Namespace JellyfinPlayer.Shared` convention for all shared files.

- [ ] **Step 1: Create `Shared/Placeholder.vb`**

```vb
Namespace JellyfinPlayer.Shared
    Public Module Placeholder
        Public Function Add(a As Integer, b As Integer) As Integer
            Return a + b
        End Function
    End Module
End Namespace
```

- [ ] **Step 2: Create the MSTest project `JellyfinPlayer.Core.Tests.vbproj`**

`OutputType` `Library`, `TargetFrameworkVersion` `v4.5`, `RootNamespace` `JellyfinPlayer.Core.Tests`. Link the shared files:

```xml
<ItemGroup>
  <Compile Include="..\Shared\*.vb">
    <Link>Shared\%(Filename)%(Extension)</Link>
  </Compile>
  <Compile Include="PlaceholderTests.vb" />
</ItemGroup>
<ItemGroup>
  <Reference Include="System" />
  <Reference Include="Microsoft.VisualStudio.QualityTools.UnitTestFramework">
    <HintPath>$(MSBuildProgramFiles32)\Microsoft Visual Studio 12.0\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll</HintPath>
  </Reference>
</ItemGroup>
```

- [ ] **Step 3: Write the failing test `PlaceholderTests.vb`**

```vb
<TestClass>
Public Class PlaceholderTests
    <TestMethod>
    Public Sub Add_sums_two_integers()
        Assert.AreEqual(5, JellyfinPlayer.Shared.Placeholder.Add(2, 3))
    End Sub
End Class
```

- [ ] **Step 4: Link `..\Shared\*.vb` into the app project too**, in `JellyfinPlayer.vbproj`:

```xml
<Compile Include="..\Shared\*.vb">
  <Link>Shared\%(Filename)%(Extension)</Link>
</Compile>
```

- [ ] **Step 5: Add the test project to the solution** (project GUID `{B1A2C3D4-0001-4000-8000-000000000001}`, `Debug|Any CPU` / `Release|Any CPU` only).

- [ ] **Step 6: Build and run the test to verify it passes**

Run the Global Constraints build command, then the mstest command.
Expected: build succeeds (app `.appx` + `JellyfinPlayer.Core.Tests.dll`); test output reports `Passed! - 1 test`.

- [ ] **Step 7: Commit**

```bash
git add JellyfinPlayer/Shared JellyfinPlayer/JellyfinPlayer.Core.Tests JellyfinPlayer/JellyfinPlayer.sln JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj
git commit -m "test: add desktop test project linking shared logic"
```

---

### Task 2: DTOs and JSON helper

**Files:**
- Create: `JellyfinPlayer/Shared/Models.vb`
- Create: `JellyfinPlayer/Shared/Json.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/JsonTests.vb`

**Interfaces:**
- Consumes: nothing.
- Produces (all in `Namespace JellyfinPlayer.Shared`):
  - Classes with `<DataContract>` + `<DataMember(Name:="...")>`: `ServerInfoDto` (`ServerName`, `Version`, `Id`), `PublicUserDto` (`Id`, `Name`, `HasPassword`), `AuthenticateResultDto` (`User` As `UserDto`, `AccessToken`, `ServerId`), `UserDto` (`Id`, `Name`), `QueryResultDto(Of T)` (`Items` As `List(Of T)`, `TotalRecordCount`), `BaseItemDto` (`Id`, `Name`, `Type`, `CollectionType`, `Overview`, `RunTimeTicks` As Long?, `IndexNumber` As Integer?, `ParentIndexNumber` As Integer?, `SeriesName`, `ProductionYear` As Integer?, `UserData` As `UserDataDto`, `ImageTags` As `Dictionary(Of String,String)`, `BackdropImageTags` As `List(Of String)`, `MediaSources` As `List(Of MediaSourceInfoDto)`, `ChildCount` As Integer?), `UserDataDto` (`PlaybackPositionTicks` As Long?, `Played` As Boolean, `IsFavorite` As Boolean), `MediaSourceInfoDto` (`Id`, `Path`, `Container`, `Protocol`, `SupportsDirectPlay`, `SupportsTranscoding` As Boolean, `DirectStreamUrl`, `TranscodingUrl`, `TranscodingSubProtocol`, `RunTimeTicks` As Long?, `MediaStreams` As `List(Of MediaStreamDto)`), `MediaStreamDto` (`Index` As Integer, `Type`, `Codec`, `Language`, `DisplayTitle`, `IsDefault` As Boolean, `IsExternal` As Boolean, `Height` As Integer?, `Width` As Integer?, `Channels` As Integer?), `PlaybackInfoResponseDto` (`MediaSources` As `List(Of MediaSourceInfoDto)`, `PlaySessionId`), `PlaybackProgressDto` (`ItemId`, `MediaSourceId`, `PlaySessionId`, `PositionTicks` As Long?, `IsPaused` As Boolean, `CanSeek` As Boolean, `PlayMethod`), `DeviceProfileDto` (`MaxStreamingBitrate` As Integer, `TranscodingProfiles` As `List(Of TranscodingProfileDto)`, `DirectPlayProfiles` As `List(Of DirectPlayProfileDto)`, `SubtitleProfiles` As `List(Of SubtitleProfileDto)`), `PlaybackInfoRequestDto` (`UserId`, `MaxStreamingBitrate` As Integer, `StartTimeTicks` As Long, `AudioStreamIndex` As Integer?, `SubtitleStreamIndex` As Integer?, `AutoOpenLiveStream` As Boolean, `DeviceProfile` As `DeviceProfileDto`).
  - `Public Module Json` with `Function Deserialize(Of T)(json As String) As T` and `Function Serialize(value As Object) As String`, both using `DataContractJsonSerializer`.

- [ ] **Step 1: Write the failing tests `JsonTests.vb`**

Assert: deserializing `{"ServerName":"Home","Version":"10.9.10","Id":"abc"}` into `ServerInfoDto` yields those values; deserializing `{"Items":[],"TotalRecordCount":0}` into `QueryResultDto(Of BaseItemDto)` gives an empty `Items` and `TotalRecordCount = 0`; `Serialize` of a `PlaybackProgressDto` with `PositionTicks = 5` contains `"PositionTicks":5`; deserializing an unknown field does not throw.

- [ ] **Step 2: Run tests to verify they fail**

Run the mstest command. Expected: FAIL — type `ServerInfoDto` not defined.

- [ ] **Step 3: Implement `Models.vb` and `Json.vb`** with the exact class/field names above. Match JSON PascalCase with `Name:=` on every `DataMember` whose VB name differs in case. Do not add `[EmitDefaultValue]` — the server tolerates extra nulls.

- [ ] **Step 4: Run tests to verify they pass**

Run the mstest command. Expected: `Passed!`.

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/Shared/Models.vb JellyfinPlayer/Shared/Json.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/JsonTests.vb
git commit -m "feat: add Jellyfin DTOs and JSON helper"
```

---

### Task 3: Ticks and time formatting

**Files:**
- Create: `JellyfinPlayer/Shared/Ticks.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/TicksTests.vb`

**Interfaces:**
- Produces: `Public Module Ticks` with `Const TicksPerSecond As Long = 10000000`, `Function ToTimeSpan(ticks As Long) As TimeSpan`, `Function FromTimeSpan(value As TimeSpan) As Long`, `Function Format(ticks As Long?) As String` (empty for null, `h:mm:ss` when ≥ 1 hour, else `m:ss`), `Function FormatDuration(ticks As Long?) As String` (same, for runtime display).

- [ ] **Step 1: Write failing tests** — `ToTimeSpan(10_000_000)` = `TimeSpan.FromSeconds(1)`; round-trip `FromTimeSpan(ToTimeSpan(x)) = x`; `Format(Nothing)` = `""`; `Format(900_000_000)` = `"1:30"`; `Format(3_600_000_000)` = `"1:00:00"`.

- [ ] **Step 2: Run tests to verify they fail** (mstest). Expected: FAIL — `Ticks` not defined.

- [ ] **Step 3: Implement `Ticks.vb`.**

- [ ] **Step 4: Run tests to verify they pass.** Expected: `Passed!`.

- [ ] **Step 5: Commit** — `git commit -m "feat: add ticks helpers"` (stage `Ticks.vb` + test).

---

### Task 4: URL builder and device profile

**Files:**
- Create: `JellyfinPlayer/Shared/JellyfinUrls.vb`
- Create: `JellyfinPlayer/Shared/DeviceProfileBuilder.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/UrlsTests.vb`

**Interfaces:**
- Consumes: `Models.vb` (`DeviceProfileDto`).
- Produces:
  - `Public Class JellyfinUrls` constructed with `New(baseUrl As String, token As String)`; `BaseUrl` normalized (no trailing slash). Methods (all return `String`): `SystemInfoPublic`, `PublicUsers`, `AuthenticateByName`, `Views(userId)`, `Items(userId, parentId, includeItemTypes, startIndex, limit, sortBy, sortOrder)`, `Item(userId, itemId)`, `Seasons(seriesId, userId)`, `Episodes(seriesId, userId, seasonId)`, `Search(userId, term)`, `Resume(userId, limit)`, `NextUp(userId, limit)`, `PlaybackInfo(itemId, userId)`, `PlayedItems(userId, itemId)`, `FavoriteItems(userId, itemId)`, `SessionsPlaying`, `SessionsProgress`, `SessionsStopped`, `PrimaryImage(itemId, maxHeight)`, `BackdropImage(itemId, maxWidth)`.
  - `Public Module DeviceProfileBuilder` with `Function Default() As DeviceProfileDto` (HLS transcode profile: `Container="ts"`, `VideoCodec="h264"`, `AudioCodec="aac"`, `Protocol="hls"`, `MaxAudioChannels="2"`; direct-play `Container="mp4,mkv"`, `VideoCodec="h264"`, `AudioCodec="aac,mp3"`; subtitle profiles `srt`/`ass` with `Method="Encode"`) and `Function ToRequest(userId As String, startTicks As Long) As PlaybackInfoRequestDto` (`MaxStreamingBitrate = 8000000`, `AutoOpenLiveStream = True`).

- [ ] **Step 1: Write failing tests** — with `New JellyfinUrls("http://host:8096/", "TOK")`: `SystemInfoPublic` = `"http://host:8096/System/Info/Public"`; `PrimaryImage("id1", 300)` equals `"http://host:8096/Items/id1/Images/Primary?maxHeight=300&quality=90&api_key=TOK"`; `Items` includes `Recursive=true`, `StartIndex=`, `Limit=`, `api_key=TOK`; `DeviceProfileBuilder.Default().TranscodingProfiles(0).Protocol` = `"hls"`.

- [ ] **Step 2: Run tests to verify they fail** (mstest). Expected: FAIL.

- [ ] **Step 3: Implement `JellyfinUrls.vb` and `DeviceProfileBuilder.vb`.** Every URL that is fetched by `BitmapImage` must append `api_key`; API calls use the `X-Emby-Token` header instead. URL-encode the search term with `Uri.EscapeDataString`.

- [ ] **Step 4: Run tests to verify they pass.** Expected: `Passed!`.

- [ ] **Step 5: Commit** — `git commit -m "feat: add URL builder and device profile"`.

---

### Task 5: Transport and API client (auth + system info)

**Files:**
- Create: `JellyfinPlayer/Shared/IHttpTransport.vb`
- Create: `JellyfinPlayer/Shared/HttpTransport.vb`
- Create: `JellyfinPlayer/Shared/JellyfinApiClient.vb`
- Create: `JellyfinPlayer/Shared/JellyfinException.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/AuthTests.vb`

**Interfaces:**
- Consumes: `Models.vb`, `Json.vb`, `JellyfinUrls.vb`.
- Produces:
  - `Public Interface IHttpTransport` with `Function GetStringAsync(url As String, headers As IDictionary(Of String, String)) As Task(Of String)` and `Function PostJsonAsync(url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String)`.
  - `Public Class HttpTransport` implementing it over `System.Net.Http.HttpClient` with `Timeout = TimeSpan.FromSeconds(15)`; non-2xx → throw `JellyfinException` with the status code and response body.
  - `Public Class JellyfinException Inherits Exception` with `StatusCode As Integer` and `Sub New(message, statusCode)`.
  - `Public Class JellyfinApiClient` constructed with `New(transport As IHttpTransport, urls As JellyfinUrls, deviceId As String)`; properties `AccessToken`, `UserId`, `ServerId`. Methods: `Function GetServerInfoAsync() As Task(Of ServerInfoDto)`, `Function GetPublicUsersAsync() As Task(Of List(Of PublicUserDto))`, `Function AuthenticateAsync(username As String, password As String) As Task(Of AuthenticateResultDto)` (sets `AccessToken`/`UserId`/`ServerId` on success; on HTTP 401 throws `JellyfinException` with `StatusCode = 401`), `Async Function ValidateConnectionAsync() As Task(Of Boolean)`.

- [ ] **Step 1: Write failing tests `AuthTests.vb`** using a `FakeTransport` (a private class recording the last URL + headers and returning canned JSON):
  - `Authenticate_sets_token_and_user` — canned `{"User":{"Id":"u1","Name":"bob"},"AccessToken":"T","ServerId":"s1"}` → client.`AccessToken` = `"T"`, `UserId` = `"u1"`, `ServerId` = `"s1"`, and the recorded header `X-Emby-Authorization` contains `DeviceId="dev1"`.
  - `Authenticate_with_401_throws_401` — fake transport throws `JellyfinException("nope", 401)` → assert thrown with `StatusCode = 401`.
  - `GetString_timeout_is_reported` — fake throws `TaskCanceledException` → assert `JellyfinException` with a message containing `"timeout"` (transport maps it).

- [ ] **Step 2: Run tests to verify they fail** (mstest). Expected: FAIL.

- [ ] **Step 3: Implement** the four files. `AuthenticateByName` body: `{"Username":"<u>","Pw":"<p>"}`; auth header exactly `MediaBrowser Client="JellyfinPlayer", Device="WindowsPhone", DeviceId="<deviceId>", Version="1.0.0"`.

- [ ] **Step 4: Run tests to verify they pass.** Expected: `Passed!`.

- [ ] **Step 5: Commit** — `git commit -m "feat: add HTTP transport and API client auth"`.

---

### Task 6: API client — browsing

**Files:**
- Modify: `JellyfinPlayer/Shared/JellyfinApiClient.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/BrowseTests.vb`

**Interfaces:**
- Consumes: `JellyfinApiClient` from Task 5.
- Produces (added to `JellyfinApiClient`, all `Task(Of ...)`): `GetViewsAsync(userId) As Task(Of List(Of BaseItemDto))`, `GetItemsAsync(userId, parentId, includeItemTypes, startIndex, limit) As Task(Of QueryResultDto(Of BaseItemDto))`, `GetItemAsync(userId, itemId) As Task(Of BaseItemDto)`, `GetSeasonsAsync(seriesId, userId) As Task(Of List(Of BaseItemDto))`, `GetEpisodesAsync(seriesId, userId, seasonId) As Task(Of List(Of BaseItemDto))`, `SearchAsync(userId, term) As Task(Of QueryResultDto(Of BaseItemDto))`, `GetResumeAsync(userId, limit) As Task(Of List(Of BaseItemDto))`, `GetNextUpAsync(userId, limit) As Task(Of List(Of BaseItemDto))`.

- [ ] **Step 1: Write failing tests `BrowseTests.vb`** with `FakeTransport`:
  - `GetViews_parses_items_and_collection_type` — canned `{"Items":[{"Id":"v1","Name":"Movies","CollectionType":"movies"}],"TotalRecordCount":1}` → one item, `CollectionType = "movies"`.
  - `GetItems_passes_parent_and_paging` — assert the recorded URL contains `ParentId=v1`, `StartIndex=100`, `Limit=50`, `Recursive=true`.
  - `Search_encodes_term` — search `"the office"` → URL contains `SearchTerm=the%20office`.
  - `GetItem_tolerates_missing_optional_fields` — canned item with only `Id`/`Name` → no exception, `RunTimeTicks` is `Nothing`.

- [ ] **Step 2: Run tests to verify they fail.** Expected: FAIL.

- [ ] **Step 3: Implement** the eight methods, each calling `GetStringAsync` on the matching `JellyfinUrls` method with the `X-Emby-Token` header and deserializing via `Json.Deserialize`.

- [ ] **Step 4: Run tests to verify they pass.** Expected: `Passed!`.

- [ ] **Step 5: Commit** — `git commit -m "feat: add browsing API methods"`.

---

### Task 7: API client — playback info, progress, user data

**Files:**
- Modify: `JellyfinPlayer/Shared/JellyfinApiClient.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/PlaybackApiTests.vb`

**Interfaces:**
- Consumes: Tasks 2, 4, 5.
- Produces (added to `JellyfinApiClient`): `GetPlaybackInfoAsync(userId, itemId, startTicks) As Task(Of PlaybackInfoResponseDto)` (POSTs `DeviceProfileBuilder.ToRequest(userId, startTicks)`), `ReportPlaybackStartAsync(progress As PlaybackProgressDto) As Task`, `ReportPlaybackProgressAsync(progress As PlaybackProgressDto) As Task`, `ReportPlaybackStoppedAsync(progress As PlaybackProgressDto) As Task`, `SetPlayedAsync(userId, itemId, played As Boolean) As Task`, `SetFavoriteAsync(userId, itemId, favorite As Boolean) As Task`.

- [ ] **Step 1: Write failing tests `PlaybackApiTests.vb`**:
  - `GetPlaybackInfo_posts_device_profile_and_returns_source` — canned `{"MediaSources":[{"Id":"m1","TranscodingUrl":"http://h/x.m3u8","Container":"ts"}],"PlaySessionId":"ps1"}` → `MediaSources(0).TranscodingUrl` = `"http://h/x.m3u8"`, and the posted JSON body contains `"Protocol":"hls"`.
  - `SetFavorite_true_uses_POST_and_false_uses_DELETE` — assert the recorded HTTP method per call.
  - `ReportProgress_sends_position_ticks` — body contains `"PositionTicks":30000000`.

- [ ] **Step 2: Run tests to verify they fail.** Expected: FAIL.

- [ ] **Step 3: Implement.** Extend `IHttpTransport` with `Function SendAsync(method As String, url As String, json As String, headers As IDictionary(Of String, String)) As Task(Of String)` (used for POST/DELETE) and implement it in `HttpTransport` and `FakeTransport`.

- [ ] **Step 4: Run tests to verify they pass.** Expected: `Passed!`.

- [ ] **Step 5: Commit** — `git commit -m "feat: add playback info, progress and user-data APIs"`.

---

### Task 8: App session and settings persistence

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Services/AppSession.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj` (add the file)

**Interfaces:**
- Consumes: Task 5 client.
- Produces: `Public Class AppSession` with `Shared Property Current As AppSession`, properties `BaseUrl`, `AccessToken`, `UserId`, `ServerId`, `DeviceId`, `PreferredBitrate` (`Integer`, default `8000000`), and `Sub Shared Function CreateClient() As JellyfinApiClient`, `Sub Save()`, `Sub Load()`, `Sub Clear()`. Persist with `Windows.Storage.ApplicationData.Current.LocalSettings` under keys `JellyfinBaseUrl`, `JellyfinAccessToken`, `JellyfinUserId`, `JellyfinServerId`, `JellyfinDeviceId`, `JellyfinBitrate`. Generate `DeviceId` once as `Guid.NewGuid().ToString("N")` if absent.

- [ ] **Step 1: Implement `AppSession`** using `LocalSettings`. `Clear()` removes every key **except** `JellyfinDeviceId`. `Load()` is called from `App` constructor; `Save()` after a successful login.
- [ ] **Step 2: Build the app** with the Global Constraints command. Expected: build succeeds. (LocalSettings is WinRT-only, so this file is verified by compilation, not by MSTest — it is not linked into the test project.)
- [ ] **Step 3: Manual check** — run in the emulator, log in, force-close, relaunch: `AppSession.Load()` returns the saved URL/token (verified again once Task 10 exists).
- [ ] **Step 4: Commit** — `git commit -m "feat: add session and settings persistence"`.

---

### Task 9: App bootstrap, navigation service, back button

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/App.xaml.vb`
- Create: `JellyfinPlayer/JellyfinPlayer/Services/NavigationService.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Package.appxmanifest` (set `BackgroundColor` to `#101517`, keep `ForegroundText="light"`)

**Interfaces:**
- Consumes: `AppSession` (Task 8).
- Produces: `Public Module NavigationService` with `Sub Navigate(pageType As Type, parameter As Object)`, `Sub GoBack()`, `ReadOnly Property CanGoBack As Boolean`; `App` calls `AppSession.Load()` in its constructor and registers `HardwareButtons.BackPressed` to call `NavigationService.GoBack()` when `CanGoBack`.

- [ ] **Step 1: Implement** the NAV service over `Window.Current.Content` (the root `Frame`).
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check** — on the emulator, hardware Back leaves the app on the home page and goes back within the app elsewhere.
- [ ] **Step 4: Commit** — `git commit -m "feat: add app bootstrap and navigation service"`.

---

### Task 10: Server connect / login page

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml`, `MainPage.xaml.vb`
- Create: `JellyfinPlayer/JellyfinPlayer/Services/ImageHelper.vb`

**Interfaces:**
- Consumes: `JellyfinApiClient` (Task 5), `JellyfinUrls` (Task 4), `AppSession` (Task 8), `NavigationService` (Task 9).
- Produces: `Public Module ImageHelper` with `Function ImageUrl(baseUrl, token, itemId, maxHeight) As Uri` (wraps `JellyfinUrls.PrimaryImage`) and `Function BackdropUrl(baseUrl, token, itemId, maxWidth) As Uri`.

- [ ] **Step 1: Build `MainPage`** with a `TextBox` (server URL, placeholder `http://192.168.1.10:8096`), `TextBox` username, `PasswordBox` password, a `Button` "Connect", and a `TextBlock` status/error. On Connect: validate the URL, call `GetServerInfoAsync` then `AuthenticateAsync`, `AppSession.Save()`, `NavigationService.Navigate(GetType(HomePage), Nothing)`. Show `JellyfinException.Message` in the status text on failure; on 401 show "Wrong username or password". Disable the button while the call is in flight.
- [ ] **Step 2: Implement `ImageHelper`.**
- [ ] **Step 3: Build.** Expected: success.
- [ ] **Step 4: Manual check** — enter a bogus URL → readable error, no hang; enter valid credentials → navigates and the app reloads logged-in after a restart.
- [ ] **Step 5: Commit** — `git commit -m "feat: add server connect and login page"`.

---

### Task 11: Home page (views + resume + next up)

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml`, `HomePage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `GetViewsAsync`, `GetResumeAsync`, `GetNextUpAsync` (Task 6), `ImageHelper` (Task 10).
- Produces: navigation contract `Navigate(GetType(LibraryPage), viewId)` and `Navigate(GetType(ItemDetailPage), itemId)` consumed by Tasks 12–15.

- [ ] **Step 1: Build the page** with a header + "Search" action, a horizontal `ListView` of views (library tiles with `CollectionType`), and horizontal lists for "Continue watching" (`GetResumeAsync`) and "Next up" (`GetNextUpAsync`). Tapping a view opens `LibraryPage`; tapping an item opens `ItemDetailPage`. Hide a section when its list is empty.
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check** — with a live server, views render and tapping navigates.
- [ ] **Step 4: Commit** — `git commit -m "feat: add home page"`.

---

### Task 12: Library page (paged grid)

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/LibraryPage.xaml`, `LibraryPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `GetItemsAsync` (Task 6).
- Produces: paged item list; tapping an item → `ItemDetailPage` (Task 14); tapping a Series item → `SeriesPage` (Task 13).

- [ ] **Step 1: Build the page** with a `GridView` of poster items. Load the first page with `limit = 50`, then load the next page on scroll near the end using `StartIndex` (page counter). Show a progress ring while loading. Route `Type = "Series"` to `SeriesPage`, everything else to `ItemDetailPage`.
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check (Review Focus #5)** — with a library of 5000+ items, scrolling loads further pages and the list stays responsive.
- [ ] **Step 4: Commit** — `git commit -m "feat: add paged library page"`.

---

### Task 13: Series page (seasons + episodes)

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/SeriesPage.xaml`, `SeriesPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `GetItemAsync`, `GetSeasonsAsync`, `GetEpisodesAsync` (Task 6).
- Produces: tapping an episode → `ItemDetailPage` (Task 14).

- [ ] **Step 1: Build the page** with a series header (name, overview, backdrop), a season `ComboBox`, and an episode `ListView` (`{Episodenumber}. {Name}` + runtime via `Ticks.FormatDuration`). Selecting a season reloads episodes.
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check** — a multi-season series lists seasons and switches episodes correctly.
- [ ] **Step 4: Commit** — `git commit -m "feat: add series page"`.

---

### Task 14: Item detail page

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/ItemDetailPage.xaml`, `ItemDetailPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `GetItemAsync`, `SetPlayedAsync`, `SetFavoriteAsync` (Tasks 6, 7).
- Produces: `Play` action → `PlayerPage` (Task 16) passed the loaded `BaseItemDto`.

- [ ] **Step 1: Build the page** with poster, backdrop, title, year, runtime, overview, a `Play`/`Resume` button (label "Resume" when `UserData.PlaybackPositionTicks` is set), a `ToggleButton` for favorite, and one for played. Toggles call the API and update local state.
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check** — play/resume opens the player; favorite/played toggles persist after reopening the page.
- [ ] **Step 4: Commit** — `git commit -m "feat: add item detail page"`.

---

### Task 15: Search page

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/SearchPage.xaml`, `SearchPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `SearchAsync` (Task 6), `ImageHelper` (Task 10).
- Produces: results navigate to `ItemDetailPage` / `SeriesPage`.

- [ ] **Step 1: Build the page** with a `TextBox` with `QuerySubmitted` handler, a results `ListView` showing poster + name + type, and an empty-state text. Show a placeholder image via the `BitmapImage.ImageFailed` handler (never crash on a missing poster).
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check (Review Focus #4)** — an item with no poster shows the placeholder and no exception is thrown.
- [ ] **Step 4: Commit** — `git commit -m "feat: add search page"`.

---

### Task 16: Player page (direct play + HLS transcode + progress)

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Services/PlaybackService.vb`
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml`, `PlayerPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `GetPlaybackInfoAsync`, `ReportPlaybackStartAsync`, `ReportPlaybackProgressAsync`, `ReportPlaybackStoppedAsync` (Task 7), `AppSession` (Task 8).
- Produces: `Public Class PlaybackService` with `Function PrepareAsync(item As BaseItemDto, startTicks As Long) As Task(Of PlaybackPlan)` where `PlaybackPlan` has `Uri As Uri` (direct stream URL or HLS URL), `UseHls As Boolean`, `MediaSourceId As String`, `PlaySessionId As String`, `StartPosition As TimeSpan`, `SubtitleStreamIndex As Integer?`, `AudioStreamIndex As Integer?`.

- [ ] **Step 1: Implement `PlaybackService`.** Call `GetPlaybackInfoAsync`; choose the first `MediaSource` with `SupportsDirectPlay` + a `DirectStreamUrl` → direct (`UseHls = False`); otherwise a source with a `TranscodingUrl` → HLS (`UseHls = True`). If neither exists, throw `JellyfinException("This video cannot be played on this device.")`. Append `api_key` to whichever URL lacks it. Map network/file errors to a readable message and mark timeouts as `"timeout"` (Review Focus #1, #3).
- [ ] **Step 2: Build `PlayerPage`** with a `MediaElement`, a slider (seek), play/pause, and a `TextBlock` error area. For HLS: `Dim ams = Await AdaptiveMediaSource.CreateFromUriWithRequestMessageAsync(request)` where the request carries `X-Emby-Token`, then `MediaElement.SetMediaStreamSource(ams)`. For direct: set `MediaElement.Source` to the direct URL. Seek to `StartPosition`. Start reporting `ReportPlaybackStartAsync`, a 10-second `DispatcherTimer` for `ReportPlaybackProgressAsync`, and `ReportPlaybackStoppedAsync` on stop/back. On failure show the error and a "Play direct" retry button (Review Focus #3).
- [ ] **Step 3: Build.** Expected: success.
- [ ] **Step 4: Manual check** — an H.264/AAC MP4 direct-plays; a file the server must transcode plays via HLS; stopping returns to the detail page and progress is saved.
- [ ] **Step 5: Commit** — `git commit -m "feat: add player page with direct play and HLS"`.

---

### Task 17: Settings page

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml`, `SettingsPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml` (add a Settings action)
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`

**Interfaces:**
- Consumes: `AppSession` (Task 8).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Build the page** showing server URL, username/server id, a max-bitrate `ComboBox` (values 2000000/4000000/8000000/20000000), and a "Sign out" button that calls `AppSession.Clear()` and navigates to `MainPage`.
- [ ] **Step 2: Build.** Expected: success.
- [ ] **Step 3: Manual check** — changing the bitrate persists; Sign out returns to the login page and clears credentials.
- [ ] **Step 4: Commit** — `git commit -m "feat: add settings page"`.

---

### Task 18: App logo and splash from the remove.bg image

**Files:**
- Create: `tools/generate_assets.py`
- Replace: `JellyfinPlayer/JellyfinPlayer/Assets/*.scale-240.png`
- Modify: `JellyfinPlayer/JellyfinPlayer/Package.appxmanifest` (confirm `BackgroundColor="#101517"`, `ForegroundText="light"`)

**Interfaces:**
- Consumes: source image `images_1_-removebg-preview.png` (the remove.bg logo, 447x447, transparent, blue→purple).
- Produces: the six `.scale-240.png` assets at the exact sizes in Global Constraints.

- [ ] **Step 1: Write `tools/generate_assets.py`.** Use Pillow. Load the source (path passed as `argv[1]`, default `~/Downloads/images_1_-removebg-preview.png`), trim to the alpha bounding box, then for each target write the logo centered at ~72% of the shorter dimension on a transparent canvas, except `SplashScreen` (1152x1920) which is a `#101517` filled canvas with the logo centered at ~40% of the width. Output filenames exactly: `Logo.scale-240.png` (360x360), `SmallLogo.scale-240.png` (106x106), `Square71x71Logo.scale-240.png` (170x170), `WideLogo.scale-240.png` (744x360), `StoreLogo.scale-240.png` (120x120), `SplashScreen.scale-240.png` (1152x1920).
- [ ] **Step 2: Run it** into `JellyfinPlayer/JellyfinPlayer/Assets/`.
- [ ] **Step 3: Verify the outputs** — a Python check prints each file's size and that the altered PNGs keep an alpha channel. Expected sizes match exactly; `SplashScreen` is opaque.
- [ ] **Step 4: Rebuild** with the Global Constraints command. Expected: success (the build recompiles the asset resources).
- [ ] **Step 5: Visual check** — the emulator start screen shows the splash logo; the app tile shows the logo.
- [ ] **Step 6: Commit** — `git commit -m "feat: apply Jellyfin logo and splash assets"`.

---

### Task 19: `agents/` maintenance documentation

**Files:**
- Create: `agents/AGENTS.md`, `agents/README.md`, `agents/ARCHITECTURE.md`, `agents/BUILD.md`, `agents/CONVENTIONS.md`, `agents/JELLYFIN_API.md`, `agents/MAINTENANCE.md`

**Interfaces:**
- Consumes: everything above (documents it).
- Produces: nothing.

- [ ] **Step 1: Write `agents/BUILD.md`** with the exact build command (Global Constraints), the mstest command, the x86/ARM Release variants, output locations (`bin/x86/Debug`, `AppPackages/`), and the emulator/deploy note.
- [ ] **Step 2: Write `agents/ARCHITECTURE.md`** — the two-project layout, the `Shared/` link mechanism, the `IHttpTransport` seam, which files are testable vs. WinRT-only, and the data flow (page → `AppSession.CreateClient()` → `JellyfinApiClient` → `IHttpTransport`).
- [ ] **Step 3: Write `agents/CONVENTIONS.md`** — VB style, `Namespace JellyfinPlayer.Shared` for shared files, the "no NuGet / framework-only" rule, one page per folder, navigation via `NavigationService`, error handling via `JellyfinException`.
- [ ] **Step 4: Write `agents/JELLYFIN_API.md`** — the endpoint list, auth headers, ticks rule, image `api_key` rule, and the HLS device profile (from Tasks 4–7).
- [ ] **Step 5: Write `agents/MAINTENANCE.md`** — how to add a page (XAML + code-behind + vbproj entry), how to add an endpoint (URL + client method + test), how to regenerate assets, how to bump the version (`AssemblyInfo.vb` + `Package.appxmanifest`), and known limitations (external subtitles, no Live TV, WP8.1 only).
- [ ] **Step 6: Write `agents/AGENTS.md`** — a short root entry point linking the other files and restating the build/test commands.
- [ ] **Step 7: Verify** — every command quoted in `agents/BUILD.md` is run once and produces the stated result.
- [ ] **Step 8: Commit** — `git commit -m "docs: add agents maintenance guide"`.

---

### Task 20: Final build and verification

**Files:**
- Modify: none (verification only).

- [ ] **Step 1: Clean + build Debug x86.** Run `-t:Rebuild` with the Global Constraints command. Expected: `Your package has been successfully created.`
- [ ] **Step 2: Build Release ARM** (`-p:Configuration=Release -p:Platform=ARM`). Expected: success.
- [ ] **Step 3: Run the full test suite.** Expected: all tests `Passed`, 0 failed.
- [ ] **Step 4: Walk the Review Focus list** on the emulator against a live Jellyfin server and record each result (unreachable server, bad login, non-transcodable file, missing poster, 5000+ item library).
- [ ] **Step 5: Confirm the acceptance criteria:** solution builds with MSBuild 12 for x86 Debug and ARM Release; tests pass; the app connects/logs in, browses, searches, shows details, plays (direct + HLS), remembers the session writes favorites/watched; the logo/splash are the remove.bg image; `agents/` exists and its commands work.
