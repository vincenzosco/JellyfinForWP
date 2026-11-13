# Maintenance

## Add an API endpoint

1. Add the URL to `Shared/JellyfinUrls.cs` (append `ApiKey` via `WithKey` if a
   `BitmapImage` or `MediaElement` will fetch it — never the removed `api_key`).
2. Add the DTO to `Shared/Models.cs` (`[DataContract]` + `[DataMember]` on every
   field to read).
3. Add a method to `Shared/JellyfinApiClient.cs` that calls
   `_transport.GetStringAsync`/`PostJsonAsync` and `Json.Deserialize`.
4. Add a test in `JellyfinPlayer.Core.Tests/` using `FakeTransport`; verify the
   recorded URL and the parsed result.

## Add a page

1. Create `Pages/<Name>.xaml` (+ `.xaml.cs`), `x:Class="JellyfinPlayer.<Name>"`.
2. Register it in `JellyfinPlayer.csproj`: a `<Page>` entry under the app-definition
   `ItemGroup` and the `.xaml.cs` is picked up by `<Compile Include="Pages\*.xaml.cs">`.
3. Derive from `phone:PhoneApplicationPage` and navigate with
   `NavigationService.Navigate(typeof(<Name>), parameter)`; handle the Back key with
   `BackKeyPress`.
4. Remember Silverlight `StackPanel` has **no `Padding`** — use child margins or a
   `Border`; a poster grid is a `LongListSelector` or `ListBox`, not a WinRT `GridView`.

## Tests

See [`TESTING.md`](TESTING.md). Add a unit test for any pure logic in `Shared/`
(the test project links it automatically via `*.cs`); add an integration test only
for behaviour that needs a real server, with `<TestCategory("Integration")>` on the
method.

## Regenerate logo/splash

```bash
python tools/generate_assets.py                          # default source (remove.bg logo)
python tools/generate_assets.py path/to/logo.png outDir  # explicit
```

It writes the six `.scale-240.png` files into
`JellyfinPlayer/JellyfinPlayer/Assets/`. Sizes are fixed:
`Logo` 360x360, `SmallLogo` 106x106, `Square71x71Logo` 170x170, `WideLogo` 744x360,
`StoreLogo` 120x120, `SplashScreen` 1152x1920 (solid `#101517`). The manifest
references the un-suffixed names (`Assets\Logo.png`); the build resolves the
`scale-240` variants automatically.

## Bump the version

Edit all three, keeping the numbers identical:
- `JellyfinPlayer/JellyfinPlayer/Properties/AssemblyInfo.cs`
  (`AssemblyVersion`, `AssemblyFileVersion`),
- `JellyfinPlayer/JellyfinPlayer/Properties/WMAppManifest.xml`
  (`<App ... Version="…">`),
- `JellyfinPlayer/JellyfinPlayer/Package.appxmanifest`
  (`<Identity ... Version="…">`).

## Gotchas

- Only C# 5 syntax: no null-conditional operator, `nameof`, string interpolation,
  expression-bodied members or auto-property initializers (`LangVersion 5`).
- Inside a page, `NavigationService` resolves to the framework property; the app's
  own helper is `Services.NavigationService`.
- `TextBox` has no `QuerySubmitted` on WP8.0 — handle `KeyDown` + `Enter`.
- The test project needs a reference to `Microsoft.VisualStudio.QualityTools.UnitTestFramework`,
  or `TestClass`/`Assert` are "not declared".

## Known limitations

- No Live TV, collections management, downloads or offline sync.
- External subtitles are not selectable in the UI; only burned-in transcoding.
- Audio track selection is not exposed (the server's default track is used).
- HTTPS with self-signed certificates is not trusted; use plain HTTP or a
  valid certificate.
- Windows Phone 8 Silverlight only; not portable to UWP without reworking
  `IsolatedStorageSettings`, navigation and playback.
- "Fullscreen" hides the System Tray and allows landscape; Silverlight for Windows
  Phone has no platform fullscreen video mode.
- Logins saved by the older WinRT package cannot be read: IsolatedStorageSettings is a
  separate store, so upgrading asks for the password once.
- Autodiscovery relies on UDP broadcast, which does not cross subnets/VLANs; VPN
  and guest Wi-Fi often block it. Manual entry always remains available.
