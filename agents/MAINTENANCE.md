# Maintenance

## Add an API endpoint

1. Add the URL to `Shared/JellyfinUrls.vb` (append `api_key` via `WithKey` if a
   `BitmapImage` will fetch it).
2. Add the DTO to `Shared/Models.vb` (`<DataContract>` + `<DataMember>` on every
   field to read).
3. Add a method to `Shared/JellyfinApiClient.vb` that calls
   `_transport.GetStringAsync`/`PostJsonAsync` and `Json.Deserialize`.
4. Add a test in `JellyfinPlayer.Core.Tests/` using `FakeTransport`; verify the
   recorded URL and the parsed result.

## Add a page

1. Create `Pages/<Name>.xaml` (+ `.xaml.vb`), `x:Class="JellyfinPlayer.<Name>"`.
2. Register it in `JellyfinPlayer.vbproj`: a `<Page>` entry under the app-definition
   `ItemGroup` and a `<Compile ... DependentUpon>` entry under the pages `ItemGroup`.
3. Navigate with `NavigationService.Navigate(GetType(<Name>), parameter)`.
4. Set `ScrollViewer.HorizontalScrollMode`/vertical as needed; remember WinRT
   `StackPanel` has **no `Padding`** — use child margins or a `Border`.

## Tests

See [`TESTING.md`](TESTING.md). Add a unit test for any pure logic in `Shared/`
(the test project links it automatically via `*.vb`); add an integration test only
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

Edit both:
- `JellyfinPlayer/JellyfinPlayer/My Project/AssemblyInfo.vb`
  (`AssemblyVersion`, `AssemblyFileVersion`),
- `JellyfinPlayer/JellyfinPlayer/Package.appxmanifest`
  (`<Identity ... Version="1.0.0.0">`).

## Gotchas

- `Resume` and `Default` are VB keywords — avoid them as member names.
- VB auto-property `ReadOnly` needs VB14; use a backing field + `Get`.
- `TextBox` has no `QuerySubmitted` on WP8.1 — handle `KeyDown` + `Enter`.
- The test project needs VB `<Import Include="JellyfinPlayer.Shared" />` and the
  MSTest import, or `TestClass`/`Assert` are "not declared".

## Known limitations

- No Live TV, collections management, downloads or offline sync.
- External subtitles are not selectable in the UI; only burned-in transcoding.
- Audio track selection is not exposed (the server's default track is used).
- HTTPS with self-signed certificates is not trusted; use plain HTTP or a
  valid certificate.
- WP8.1 only; not portable to UWP without reworking `LocalSettings`, navigation
  and playback.
- Autodiscovery relies on UDP broadcast, which does not cross subnets/VLANs; VPN
  and guest Wi-Fi often block it. Manual entry always remains available.
