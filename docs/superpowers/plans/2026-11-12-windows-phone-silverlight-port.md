# Windows Phone Silverlight 8.1 Port Implementation Plan

> **For agentic workers:** Execute this plan task-by-task in the current user-authorized session. Keep each test/build checkpoint visible. Do not stage or commit unrelated worktree changes.

**Goal:** Replace JellyfinPlayer's WinRT Windows Phone 8.1 app with a native Windows Phone Silverlight 8.1 app while retaining Jellyfin browsing, accounts, playback, localization, discovery fallback, and shared logic.

**Architecture:** Retarget the phone app and solution to the VS2013 Windows Phone Silverlight 8.1 project system. Keep platform-independent API and decision code linked into desktop tests; replace its HttpClient dependency with a cross-target HttpWebRequest transport. Replace WinRT pages, storage, discovery, media shell and resources with Windows Phone Silverlight APIs and native controls.

**Tech Stack:** VB.NET 12, Windows Phone Silverlight 8.1, VS2013/MSBuild 12, .NET Framework 4.5 MSTest v1, `HttpWebRequest`, `System.Net.Sockets.Socket`, `PhoneApplicationFrame`, `PhoneApplicationPage`, `LongListSelector`/native Silverlight controls, `MediaElement`, `.resx`.

**Spec:** `docs/superpowers/specs/2026-11-12-windows-phone-silverlight-port-design.md`

## Global Constraints

- Preserve the product/phone identity and the currently user-edited version `1.2.1.4`; do not silently discard the pending manifest edit.
- No NuGet packages and no package restore; use the existing VS2013 and Windows Phone 8.1 SDK installation.
- Keep Jellyfin request contracts and `IHttpTransport` behavior; never downgrade ordinary API authentication from the `Authorization: MediaBrowser ...` header to query-string credentials.
- Keep `JellyfinPlayer/Shared/*.vb` testable from the desktop .NET 4.5 MSTest project.
- Use only VB 12 syntax and the Windows Phone Silverlight 8.1 API surface in the app project.
- Keep English and Italian resources aligned, with English fallback.
- Treat UDP broadcast, cross-project settings continuity, Authorization headers and real-device codec support as unverified until target evidence exists.
- Do not commit `bin`, `obj`, `.vs`, generated XAP/APPX packages, test-result files, local server credentials or unrelated working-tree edits.

---

## File and boundary map

- `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`, `JellyfinPlayer/JellyfinPlayer.sln`, and both manifests define target project system, resources, capabilities, identity and packaging.
- `JellyfinPlayer/Shared/HttpTransport.vb`, `IHttpTransport.vb`, `FailureText.vb`, and shared tests define the HTTP/API test seam and error contract.
- `JellyfinPlayer/JellyfinPlayer/App.xaml*`, all `Pages/*`, and page-specific XAML define lifecycle, navigation and user workflows.
- `JellyfinPlayer/JellyfinPlayer/Services/AppSession.vb`, `LocalAccountStorage.vb`, `Localized.vb`, `NavigationService.vb`, `PosterItem.vb`, `UdpServerDiscovery.vb`, and `WinRtPlayerShell.vb` bind shared decisions to the phone runtime.
- `Strings/en-US/Resources.resw`, `Strings/it-IT/Resources.resw`, and localization tests define all user-visible text.
- `JellyfinPlayer/JellyfinPlayer.Core.Tests/*.vb` verifies shared rules and statically checks the app's markup/resources.
- `README.md`, `README.it.md`, and `agents/README.md` describe the app and build workflow.

## Task 1: Prove the Silverlight build surface and create the target app skeleton

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`
- Modify: `JellyfinPlayer/JellyfinPlayer.sln`
- Create/modify: Silverlight app entry XAML/code-behind and `Properties/WMAppManifest.xml` as required by the native project system
- Test: Silverlight app target build

- [ ] Inspect installed files under `MSBuild/Microsoft/WindowsPhone/v8.1`, the Windows Phone v8.1 reference assemblies and Visual Studio 2013's native phone project template. Record the exact VB target import, project type GUIDs, XAP packaging properties, Silverlight version and capabilities in the project itself (not as a separate report).
- [ ] Create the smallest Silverlight 8.1 app skeleton with a `PhoneApplicationFrame`, one `PhoneApplicationPage`, product identity, network capability and root navigation page. Keep the old AppX manifest version/identity values; set `WMAppManifest.xml` and `Package.appxmanifest` versions consistently to `1.2.1.4`.
- [ ] Add the Silverlight project type and platform configurations to the solution and remove the WinRT `WindowsXaml` target import and `NETFX_CORE` definition from the app project.
- [ ] Build the skeleton with MSBuild 12 for the actual emulator and ARM target. Do not move on until the target produces a Silverlight XAP successfully.

## Task 2: Lock down the shared HTTP transport contract with failing tests

**Files:**
- Modify: `JellyfinPlayer/Shared/HttpTransport.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/AuthTests.vb`, `FailurePathTests.vb`, or a focused new `HttpTransportTests.vb`
- Modify if needed: `JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj`

- [ ] Add an injected request factory/transport seam around the Silverlight-compatible request implementation so method, URI, headers, JSON body and timeout behavior are testable without network access.
- [ ] Add tests proving GET uses the MediaBrowser header and no body; POST JSON sets content type and body; HTTP errors preserve status/media type/body classification; timeouts map to `JellyfinErrorKind.Timeout`; unreachable errors map to `Unreachable`.
- [ ] Build and run the focused MSTest tests; confirm the new contract tests fail for the current `HttpClient` implementation for the expected API/target incompatibility.
- [ ] Implement an asynchronous `HttpWebRequest` transport compatible with both desktop .NET 4.5 and the WP Silverlight 8.1 assemblies, using begin/end request stream and response operations. Enforce a 15-second timeout without blocking the UI thread.
- [ ] Re-run focused tests and the entire non-integration MSTest suite. If desktop and phone `HttpWebRequest` differ, keep platform-specific code behind a small transport implementation boundary, not `#If` branches spread through the API client.
- [ ] Add a target-device verification checklist item for actual Authorization header arrival before considering Jellyfin authentication verified.

## Task 3: Replace account persistence and application lifecycle

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/AppSession.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/LocalAccountStorage.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/App.xaml`, `App.xaml.vb`
- Test: account store/app-session tests and static migration checks

- [ ] Add failing tests for active account restoration, bitrate/device-id persistence, legacy single-session migration and idempotent migration/source retention.
- [ ] Check the WP8.1 Silverlight reference assemblies for `Windows.Storage.ApplicationData` before choosing a storage backend. Prefer the existing LocalSettings container only if target compilation and a runtime readback prove it is usable under the preserved identity.
- [ ] Otherwise implement a serialized `IsolatedStorageSettings.ApplicationSettings` adapter. Protect every read/write/save with one shared lock and persist the existing account JSON as a single value. Do not access the settings object from parallel HTTP callbacks.
- [ ] Implement migration as a read/validate/write/read-back sequence. Remove legacy keys only after successful round-trip validation. If old LocalSettings are inaccessible, preserve any usable Silverlight settings and show a normal manual login recovery path rather than claiming seamless upgrade.
- [ ] Replace WinRT `Application.OnLaunched` and suspension events with the Silverlight application constructor, `RootFrame`, startup URI, and `PhoneApplicationService` lifecycle hooks. Load the account store before selecting Login, Account Picker or Home as the initial route.
- [ ] Build app and tests; run the account-focused MSTest suite.

## Task 4: Replace navigation and the phone service adapters

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/NavigationService.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/WinRtPlayerShell.vb` (rename to a Silverlight-specific service file and project entry)
- Modify: `JellyfinPlayer/Shared/IPlayerShell.vb`, `PlayerSurfaceController.vb` only if a test requires a contract adjustment
- Test: `PlayerSurfaceTests.vb`

- [ ] Add failing tests for the player shell contract: fullscreen hides tray/allows landscape; exit restores tray/portrait; playing disables user idle timeout; pause/navigation releases it; all operations are idempotent.
- [ ] Wrap `PhoneApplicationFrame.NavigationService` with the existing `Navigate(Type, Object)`, `GoBack`, `CanGoBack`, `ClearBackStack` and player Back interception contract. Pass parameters through Silverlight query-string-safe state or a validated in-memory navigation context; do not serialize full DTOs into URLs.
- [ ] Implement `IPlayerShell` with `PhoneApplicationPage.SupportedOrientations`, `SystemTray.SetIsVisible`, and `PhoneApplicationService.Current.UserIdleDetectionMode`. Restore original values on every exit path. Do not set `ApplicationIdleDetectionMode` to disabled globally.
- [ ] Replace `HardwareButtons.BackPressed` app-wide handling with the native page `BackKeyPress` event, retaining the player rule that Back exits fullscreen before leaving the page.
- [ ] Run player-surface tests and build the app.

## Task 5: Port account/login/home/catalog pages to native Windows Phone controls

**Files:**
- Modify: `MainPage.xaml`, `MainPage.xaml.vb`
- Modify: `Pages/AccountsPage.xaml(.vb)`, `HomePage.xaml(.vb)`, `LibraryPage.xaml(.vb)`, `SeriesPage.xaml(.vb)`, `ItemDetailPage.xaml(.vb)`, `SearchPage.xaml(.vb)`, `SettingsPage.xaml(.vb)`
- Modify: `Services/PosterItem.vb`, `Services/ImageHelper.vb`, `Services/ItemNavigator.vb`

- [ ] Create a target control mapping in tests: WinRT `Page` to `PhoneApplicationPage`; `Frame.Navigate` to URI/phone navigation; `GridView` to `LongListSelector` or `ListBox`; `ProgressRing` to indeterminate `ProgressBar`; `Tapped` to Silverlight `Tap`/manipulation; `PlaceholderText` to explicit hint text; `Visibility` to Silverlight values; `BitmapImage` to `System.Windows.Media.Imaging`.
- [ ] Convert each page's XAML namespace and class to `phone:PhoneApplicationPage`, removing WinRT `ThemeResource`, `x:Uid`, `ms-appx`, `using:` namespaces, WinRT-only properties/events and unsupported controls.
- [ ] Keep existing page code-behind request logic and error behavior, adapting event args, page lifecycle overrides, image source setup and navigation route handling to Silverlight APIs.
- [ ] Replace home horizontal poster rails and library/search/episode grids with native phone list templates that retain poster, title/subtitle, tap selection, paging/load-more, season selection and item destination behavior. Avoid new third-party controls.
- [ ] Preserve login manual server input and make autodiscovery failure non-blocking. Preserve account switch/remove, Favorite/Watched, bitrate setting and sign-out behavior.
- [ ] Build after each coherent page group, then run static page/resource checks and the app build. Fix markup-load failures before continuing.

## Task 6: Port discovery to Windows Phone socket APIs

**Files:**
- Modify/replace: `JellyfinPlayer/JellyfinPlayer/Services/UdpServerDiscovery.vb`
- Modify: `JellyfinPlayer/Shared/ServerDiscoveryParser.vb`, `ServerProbe.vb` only when a new tested behavior is required
- Test: `ServerDiscoveryParserTests.vb`, `ServerProbeTests.vb`

- [ ] Add failing tests for broadcast payload/port preservation, malformed datagram rejection, duplicate candidate handling and timeout with no reply.
- [ ] Implement IPv4 UDP using `System.Net.Sockets.Socket` and `SocketAsyncEventArgs` supported by the WP8.1 Silverlight target. Handle synchronous-completion and asynchronous-completion forms for every socket operation, read sender endpoint and dispose on cancellation/failure.
- [ ] Attempt broadcast `255.255.255.255:7359`; wait no longer than 2.5 seconds and collect validated Jellyfin candidates only. Discovery exceptions must not prevent manual URL entry.
- [ ] Build Silverlight app and run pure discovery tests.
- [ ] Verify discovery send/receive on WP8.1 emulator and, if available, a real device. If the network/runtime blocks broadcast or unicast replies, retain manual login and report autodiscovery as unavailable rather than marking it tested.

## Task 7: Replace localization resources with Silverlight `.resx`

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Resources/Resources.resx` and `Resources.it-IT.resx` (or the exact standard satellite resource layout generated by the VS2013 phone project)
- Modify: app project resource includes and generated resource settings
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/Localized.vb`
- Remove/replace: `Strings/en-US/Resources.resw`, `Strings/it-IT/Resources.resw`
- Modify: `LocalizationTests.vb` and the test project linked/copied resource file list

- [ ] Add tests proving both cultures expose the same key set, values are nonempty, format placeholders match and every key used in code/XAML exists.
- [ ] Run tests before migration and confirm the old `.resw` test discovers incompatible resource formats or the missing Silverlight resource source.
- [ ] Migrate all current string ids and values exactly into `.resx`, preserving `{0}` substitutions. Generate the strongly typed resource wrapper using the VS2013/MSBuild resource generator.
- [ ] Implement `Localized.Text`, `Format`, and `DescribeError` over `ResourceManager` and culture fallback. Ensure device culture chooses Italian for `it-*` and English for unknown locales.
- [ ] Update XAML bindings to `LocalizedStrings`/generated resources or set content from code behind. Keep visible strings out of literals and preserve TalkBack/accessibility labels on player controls.
- [ ] Update localization tests to inspect the actual Silverlight `.resx` files, page markup and code-behind. Run localization and full desktop suite.

## Task 8: Port playback and track media compatibility

**Files:**
- Modify: `Pages/PlayerPage.xaml(.vb)`
- Modify: `Services/PlaybackService.vb`
- Modify: `Shared/DeviceProfileBuilder.vb` and `PlaybackUrlResolver.vb` if profile assertions are needed
- Modify: `Shared/PlayerSurfaceController.vb`, `IPlayerShell.vb`, `PlayerSurfaceTests.vb`

- [ ] Add/adjust failing tests for device-profile support of the declared baseline container/codecs, direct/transcode selection, fallback-once, skip bounds, unknown duration and stop/progress reporting.
- [ ] Rebuild the player page with a named native Silverlight `System.Windows.Controls.MediaElement`; preserve HTTP URL source, `MediaOpened`, `MediaFailed`, `MediaEnded`, `CurrentStateChanged`, `Position`, `NaturalDuration`, mute, stretch, and UI dispatcher timer behavior where supported by the WP reference assemblies.
- [ ] Ensure player UI creates one `MediaElement` instance and retains persistent Back/close controls, loading/error state, seek/action names, and controls that stay visible while paused/buffering.
- [ ] Set device profile to native-confirmed baseline only (MP4/H.264/AAC for the target devices); do not report MKV direct play unless device evidence proves the container/codec path. Keep progressive MP4 transcode fallback and current server progress lifecycle.
- [ ] Implement fullscreen-like layout by hiding the System Tray and setting landscape orientation on the player page. Document that this is page-sized video, not platform fullscreen mode.
- [ ] Build and run playback/shared decision tests; run manual playback on target for direct compatible MP4, transcode MP4, direct failure, seek, progress, mute and orientation. Mark untested hardware behaviors explicitly.

## Task 9: Add architecture regression gates and remove WinRT-only project surfaces

**Files:**
- Modify: test project resource-copy rules and app tests
- Modify: `JellyfinPlayer/JellyfinPlayer.vbproj`, solution and manifests as required
- Remove from app project: old WinRT app/page/service/resource inputs and obsolete files only after Silverlight replacements compile

- [ ] Add failing static tests that reject `NETFX_CORE`, `AppContainerExe`, WindowsXaml target imports, `Windows.UI.Xaml`, `Windows.Storage.ApplicationData` only if target configuration chose not to use it, `Windows.Networking.Sockets.DatagramSocket`, `ResourceLoader`, `ProgressRing`, WinRT `x:Uid`, `ThemeResource`, and `ms-appx` in the maintained app files.
- [ ] Add checks that every maintained app page derives from `PhoneApplicationPage`, the Silverlight startup URI exists, both manifests retain matching product identity and version `1.2.1.4`, and no page/localized resource referenced by the project is missing.
- [ ] Run these tests and confirm failure against old WinRT project inputs before removing obsolete app files.
- [ ] Remove WinRT-only code/XAML and `.resw` files from the project after replacements exist. Preserve version-control history through normal Git deletions and do not retain a second WinRT app unless the spec is amended.
- [ ] Run Silverlight emulator/ARM builds, shared test build and the full non-integration MSTest suite.

## Task 10: Update docs, verify impact, commit and push

**Files:**
- Modify: `README.md`, `README.it.md`, `agents/README.md`, `agents/ARCHITECTURE.md`, `agents/BUILD.md`, `agents/TESTING.md`, `agents/CONVENTIONS.md`, `agents/MAINTENANCE.md` where now inaccurate
- Keep unchanged: user-owned changes not required by the port, including the original version bump if already present

- [ ] Replace WinRT/Windows Store requirements and commands with verified Windows Phone Silverlight 8.1 / VS2013 / MSBuild commands. Describe the generated Silverlight package format from build output, not assumptions.
- [ ] Update capabilities, account/storage constraints, native controls, codec profile, fullscreen-like behavior, discovery limitations and manual hardware test steps in both English and Italian docs. Keep technical API inventory in `agents/` docs.
- [ ] Validate all README relative links, images, section anchors, build paths, resource lists and manifest versions; run `git diff --check`.
- [ ] Re-read the complete approved specification and plan; map every acceptance criterion to a command/result or explicitly mark hardware/runtime evidence unverified. Reconcile intended impact with actual diff and ensure no scope drift.
- [ ] Run app build for emulator and ARM; run shared tests; run XAML/resource/project static checks. Run local integration tests only if valid user-provided test configuration is present; do not print or stage local credentials. Manual phone/device checks that cannot be performed must remain listed as unverified.
- [ ] Review `git diff --stat`, `git diff`, and `git status`; confirm no generated packages, test result output, ignored settings, unrelated manifest edits or unrelated README changes were staged.
- [ ] Stage only port-related files. Review repository history and use one concise human commit subject matching the repo's actual convention; include no AI attribution or trailers. Push the committed port to the requested GitHub remote after verifying the branch/remote and commit content.
