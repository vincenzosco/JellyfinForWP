# Windows Phone Silverlight 8.1 port design

## Status

Approved by the user in chat on 2026-11-12.

## Goal

Replace the current Windows Phone 8.1 WinRT/XAML app with a Windows Phone Silverlight 8.1 app, keeping the Jellyfin workflows, Italian and English UI, saved accounts, local-network discovery, and native phone playback wherever the Silverlight platform supports them.

## Current state

The app is currently a VB.NET Windows Phone 8.1 Store/WinRT app. Its `.vbproj` uses `AppContainerExe`, `NETFX_CORE`, `WindowsXaml` build targets, `Package.appxmanifest`, WinRT `Application`/`Page`, `Frame`, `Windows.Storage.ApplicationData`, `Windows.Networking.Sockets.DatagramSocket`, and WinRT media/display APIs. Eight XAML screens and the login page use WinRT-specific controls and resource lookup.

The platform-independent Jellyfin API, DTOs, URL construction, account-store rules, playback URL decisions, stream fallback, seek bounds, ticks and test fakes are in `JellyfinPlayer/Shared/`. The desktop MSTest project links those files and targets .NET Framework 4.5. Keep that test seam.

The working tree already contains a user edit changing `Package.appxmanifest` version from `1.1.2.3` to `1.2.1.4`. Preserve that version and identity while porting; do not discard or silently replace the pending edit.

## Product behavior to retain

- Manual Jellyfin server URL entry and automatic Jellyfin server discovery on UDP port 7359.
- Login through `/Users/AuthenticateByName`, saved multiple accounts, account switching/removal, re-login for a refused token, and resume to the last active account.
- Home sections for Continue watching, Next up, and libraries; paged library browsing; season/episode selection; item details; search; Favorite and Watched actions.
- Direct playback when the source is compatible, otherwise progressive HTTP MP4 transcode, server playback start/progress/stop reporting, one automatic direct-to-transcode retry, and the direct-stream fallback action when available.
- Player controls for play/pause, 10-second backward and 30-second forward seek, mute, fit/fill, position/duration, buffering, fullscreen-like presentation, and Back behavior.
- Bitrate preference and sign-out settings.
- English and Italian UI, chosen by device UI culture with English fallback.
- Tests for shared API/playback decisions and a manual phone/emulator checklist for platform UI and media.

## Platform and project design

Convert the existing app project in place; do not add a second, parallel WinRT app. Retarget the app project and solution to the Visual Studio 2013 Windows Phone Silverlight 8.1 project system and its native application/page model. The app starts at a `PhoneApplicationFrame` with `PhoneApplicationPage` screens, a Windows Phone Silverlight manifest (`WMAppManifest.xml`), and the matching Windows Phone 8.1 Store package identity/manifest where required by the SDK. Preserve the existing product identity and the user-edited version `1.2.1.4` in all relevant manifest/version metadata.

Use native Windows Phone shell and controls. Replace WinRT page classes and controls with Silverlight equivalents. Use the phone's normal hardware Back navigation and per-page Back handling. Use built-in controls only; do not add the Windows Phone Toolkit or any NuGet dependency. Where there is no built-in grid/list control equivalent, prefer a native vertical `LongListSelector`/`ListBox` presentation over introducing a custom or third-party layout package. Preserve item selection and paging even if the arrangement changes from a WinRT poster grid to native list rows.

The Silverlight `MediaElement` remains the player. Set its URI to Jellyfin's progressive HTTP MP4 or a directly playable URL and retain the current `api_key` query-string support for media/image requests, since the media element and XAML image sources cannot attach the Jellyfin authorization header. Keep player decision logic behind `IPlayerShell`; implement the phone-specific shell using `PhoneApplicationPage.SupportedOrientations`, `Microsoft.Phone.Shell.SystemTray`, and the Silverlight phone idle-detection API. Silverlight for Windows Phone does not provide WinRT's full-screen media mode; fullscreen-like behavior means hiding the System Tray, sizing the video to the page, and allowing landscape orientation. The page Back key exits this mode before navigating away.

Use `ProgressBar IsIndeterminate="True"` or an equivalent built-in phone indicator instead of WinRT `ProgressRing`. Replace WinRT-only properties/events such as `Tapped`, `PlaceholderText`, `ThemeResource`, `x:Uid`, `IsItemClickEnabled`, and `SelectionMode` with Silverlight controls, supported phone input events, styles, and resource bindings. All displayed text remains localized; no user-facing strings are added as literals to code or markup except brand name, numerical seek labels, and the example URL.

## Shared HTTP and API boundary

Replace the `System.Net.Http` implementation in `Shared/HttpTransport.vb` with a `System.Net.HttpWebRequest` implementation. It must compile both against desktop .NET 4.5 for MSTest and the Windows Phone Silverlight 8.1 reference assemblies, require no restore, implement the existing `IHttpTransport` interface, and preserve JSON request bodies, Jellyfin `Authorization: MediaBrowser ...` headers, 15-second timeout behavior, HTTP error/body classification, and the shared error kinds/messages.

Use the asynchronous request APIs (`BeginGetRequestStream` / `BeginGetResponse`, wrapped as `Task` if the target supports that API) rather than blocking the UI thread. Keep response disposal, status/media-type/body handling and network/timeout classification covered by tests. Verify on the phone target that Authorization and JSON Content-Type headers are actually sent; Silverlight's HTTP stacks have documented header restrictions that vary by platform. If the native target rejects a required header, stop and resolve that platform constraint before shipping; do not silently move tokens to query strings for ordinary Jellyfin API calls.

Keep API request behavior and the test `IHttpTransport` seam unchanged. Keep `Shared/*.vb` linked to the desktop test project. Do not remove useful tests just to make target compilation pass.

## Server discovery

Replace the WinRT `DatagramSocket` binding with a Windows Phone `System.Net.Sockets.Socket` UDP implementation using `SocketAsyncEventArgs`, the existing Jellyfin probe payload, IPv4 broadcast to UDP port 7359, response parsing through `ServerDiscoveryParser`, and candidate verification through `ServerProbe`. Bound the collection wait (the current screen waits 2.5 seconds), tolerate malformed packets and socket/network failures, and dispose the socket on every path. Preserve manual URL entry as the reliable fallback.

Windows Phone Silverlight socket documentation and historical samples distinguish UDP unicast/multicast from generic desktop socket support. Confirm that the phone SDK/runtime permits both broadcast send and receiving Jellyfin's unicast replies before treating autodiscovery as retained behavior. If broadcast reception is blocked or differs on actual device/emulator, keep the app functional with manual entry, expose the discovery limitation in localized text/docs, and do not make login depend on it.

## Storage and upgrade compatibility

Keep the existing `AccountStore` JSON format, device id, bitrate key and legacy single-account migration logic. Use the Windows Phone 8.1 Silverlight-supported `Windows.Storage.ApplicationData.Current.LocalSettings` API if the installed target reference assemblies and runtime allow it, because it is the current app's storage boundary. Before relying on it, verify that the Silverlight app can read the existing Store app's settings under the preserved package/product identity. If it cannot, use a serialized, tested `IsolatedStorageSettings.ApplicationSettings` adapter and a one-time, non-destructive migration only where Silverlight can access the prior values. Serialize all settings access (the Silverlight settings collection is not thread-safe). Never delete source values until the migrated account document has been read back and validated. If platform identity prevents automatic settings continuity, document that limitation and show an explicit login recovery path; do not claim or assume upgrade preservation.

## Localization and resources

Convert the two existing `.resw` files to Windows Phone Silverlight resource files (`.resx`) for `en-US` and `it-IT`. Retain all existing string keys and placeholder sets. Replace WinRT `x:Uid` lookups with Silverlight resource binding/code lookup (strongly typed generated resources or a small `LocalizedStrings` wrapper around `ResourceManager`). Select culture from the device UI culture; use `en-US` as fallback. Update desktop localization tests to compare the Silverlight resource files, ensure every localized markup/code key exists in both languages, verify all required placeholders match, and reject untranslated maintained UI literals.

## Build, package, and compatibility

- Target Windows Phone Silverlight 8.1 using VS2013 / MSBuild 12 and installed Windows Phone 8.1 Silverlight SDK targets.
- Keep the .NET 4.5 desktop MSTest project and no-NuGet/no-restore requirement.
- Update the solution/project GUIDs/configurations and include the Silverlight app resources, assets, pages, manifests, and generated resource class required by that project type.
- Preserve the existing phone product ID, publisher identity, package identity and version `1.2.1.4` from the current pending manifest edit. Avoid changing account-format or API contracts.
- Do not commit generated `bin`, `obj`, `TestResults`, `.xap`, `.appx`, or package output unless the repository already intentionally tracks a release artifact and the user requests a release.

## Playback compatibility and limits

Target standard Windows Phone-supported MP4/H.264 playback and the Jellyfin progressive MP4 transcode. Configure the device profile not to claim MKV direct playback unless the exact target device's native player is verified to play the selected container/codec combination. A direct source is usable only when the server and device can play it; retain a transcode fallback when available. Verify format/extension behavior, playback errors, seeking and progress reporting on target hardware or emulator.

Silverlight for Windows Phone has no equivalent of the WinRT fullscreen media/status-bar behavior. The design retains a fullscreen-like in-app player by hiding the System Tray, allowing landscape and using the full page surface. External subtitle selection, audio track selection, downloads, Live TV, offline sync, and collections management remain outside this port.

## Test and acceptance criteria

1. The solution builds using VS2013/MSBuild 12 for a Windows Phone Silverlight 8.1 emulator target and ARM/device target, with no NuGet restore.
2. The desktop shared test suite builds and all non-integration tests pass. Local Jellyfin integration tests remain opt-in and are run only with explicit test configuration.
3. Unit tests verify HTTP method/URL/headers/body, request/response error mapping, timeout behavior, URL construction, API models, account migration rules, discovery parser and candidate verification, playback URL selection/fallback, seek bounds and ticks.
4. Automated static tests verify the app project no longer references WinRT XAML build targets, `NETFX_CORE`, `Windows.UI.Xaml` page/control types, `DatagramSocket`, `ResourceLoader`, `ProgressRing`, or the old `x:Uid` resource mechanism. The new project/manifests and all local file/resource references are valid.
5. Manual phone/emulator checklist verifies first launch/login, saved/reused accounts, server discovery and manual fallback, browsing/search/favorite/watched, playback/transcode/fallback/progress, player orientation/Back/idle behavior, both UI cultures, and upgrade/re-login behavior.
6. No unverified claim is made about physical-device discovery, cross-version data migration, fullscreen, HTTPS/self-signed certificates, or codecs. If hardware/runtime evidence is unavailable, the limitation is recorded rather than claimed as passed.
7. README files describe the resulting Silverlight app, SDK prerequisites, actual build/deploy workflow, media/network limits, and tested scenarios in English and Italian.
8. The final commit subject is short and human; no AI attribution, generated-with marker, or trailer is included. Push only the intended changes, without overwriting unrelated working-tree edits.

## Snippet/source notes

These sources were consulted to choose the Silverlight APIs; implementation must be checked against the installed WP8.1 reference assemblies and tested on target:

- Microsoft, [Windows Phone navigation: the basics](https://learn.microsoft.com/en-us/archive/msdn-magazine/2011/march/msdn-magazine-mobile-matters-windows-phone-navigation-the-basics): `PhoneApplicationFrame`, `PhoneApplicationPage`, hardware Back and page navigation.
- Microsoft, [UDP socket client for Windows Phone 8](https://learn.microsoft.com/en-us/previous-versions/windows/apps/hh202864(v=vs.105)): native `System.Net.Sockets.Socket` and `SocketAsyncEventArgs` pattern. Verify broadcast/reply behavior separately.
- Microsoft, [Media in Silverlight for Windows Phone](https://learn.microsoft.com/en-us/previous-versions/windows/silverlight/dotnet-windows-silverlight/ff426928(v=vs.95)): `System.Windows.Controls.MediaElement`, supported media constraints, and no platform fullscreen mode.
- Microsoft, [Supported media codecs for Windows Phone 8](https://learn.microsoft.com/en-us/previous-versions/windows/apps/ff462087(v=vs.105)): use MP4/H.264 as the baseline, not MKV.
- Microsoft, [IsolatedStorageSettings](https://learn.microsoft.com/en-us/previous-versions/windows/silverlight/dotnet-windows-silverlight/cc626492(v=vs.95)): Silverlight key/value app settings; instance access is not thread-safe, so serialize settings operations.
- Microsoft, [HttpWebRequest.Headers](https://learn.microsoft.com/en-us/previous-versions/windows/silverlight/dotnet-windows-silverlight/s71167ye(v=vs.95)): custom header behavior and documented Silverlight network restrictions; verify Jellyfin Authorization on-device.
- Microsoft, [Upgrade Windows Phone 8 apps to Silverlight 8.1](https://learn.microsoft.com/en-us/previous-versions/windows/apps/dn642081(v=vs.105)): identity/package requirements and upgrade data preservation scenarios.
