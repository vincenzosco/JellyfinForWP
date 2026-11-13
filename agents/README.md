<p align="center">
  <img src="../JellyfinPlayer/JellyfinPlayer/Assets/BrandMark.png" width="112" alt="JellyfinPlayer logo">
</p>

<h1 align="center">JellyfinPlayer: developer guide</h1>

<p align="center">
  <strong>Build and maintain the Windows Phone 8.1 Jellyfin client.</strong><br>
  The app uses VB.NET, Windows Phone Silverlight XAML, Visual Studio 2013, and MSBuild 12.
</p>

<p align="center">
  <a href="../README.md">Project README</a> ·
  <a href="BUILD.md">Build</a> ·
  <a href="ARCHITECTURE.md">Architecture</a> ·
  <a href="TESTING.md">Testing</a>
</p>

JellyfinPlayer is a Windows Phone **Silverlight 8.1** app, packaged as a `.xap`. It is not a WinRT/Store app. The phone app uses `PhoneApplicationPage` screens and the platform's built-in `MediaElement`; API and playback decisions that can be tested without the phone live in `JellyfinPlayer/Shared/`.

## Start here

From the repository root, build the solution for the x86 emulator:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Build an ARM release package with:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Use `-p:` in Git Bash; `/p:` can be rewritten as a filesystem path. The required toolchain is Visual Studio 2013, MSBuild 12, the Windows Phone 8.1 SDK, and the VS2013 MSTest v1 assembly for tests.

## Repository map

```text
JellyfinPlayer/
  Shared/                    API client, DTOs, URL and playback logic
  JellyfinPlayer/            Silverlight app, XAML pages, and phone services
  JellyfinPlayer.Core.Tests/ MSTest v1 tests over Shared/
tools/generate_assets.py     Generates phone asset variants from a source logo
agents/                      Build, design, testing, API, and maintenance notes
```

## Working rules

- Do not add NuGet packages. Builds must work without package restore.
- `Shared/*.vb` is linked into the app and test project, rather than referenced as a library. Keep it limited to APIs supported by both .NET 4.5 and Windows Phone 8.1.
- Shared files use `Namespace Global.JellyfinPlayer.Shared`; the `Global.` prefix prevents each project's `RootNamespace` from changing the namespace.
- Use VB 12 syntax. Avoid VB14 features such as string interpolation and `NameOf`.
- Keep user-facing text in both `Resources.resx` and `Resources.it-IT.resx`. Set labels from code with `Localized.Text` / `Localized.Format`: Silverlight has no `x:Uid` resource lookup.
- Add tests for platform-independent API and playback decisions. Windows Phone UI behavior requires the manual device/emulator checklist because this project has no UI automation.

## Guides

- [Architecture and data flow](ARCHITECTURE.md)
- [Build and test commands](BUILD.md)
- [Testing, local server settings, and device checklist](TESTING.md)
- [Jellyfin endpoints and playback behavior](JELLYFIN_API.md)
- [Coding conventions](CONVENTIONS.md)
- [Maintenance, limitations, and asset generation](MAINTENANCE.md)
- [User-facing README](../README.md) · [Italian README](../README.it.md)
