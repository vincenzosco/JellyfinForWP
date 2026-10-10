# JellyfinPlayer for Windows Phone 8.1

A Jellyfin client for **Windows Phone 8.1** (WinRT XAML), written in **VB.NET** and built
with **Visual Studio 2013 / MSBuild 12**. It talks to a Jellyfin server on your local
network. The interface is available in **English and Italian** and follows the device
language.

Read this in [Italian](README.it.md).

## Features

- **Server discovery on launch**: a UDP probe on port 7359 fills in the address — and only
  after that address has answered as a Jellyfin server. Manual entry always works.
- Sign-in against `/Users/AuthenticateByName`.
- Home: Continue watching, Next up, and the library list.
- Paged library grid, series page with season picker, item detail with Play/Resume,
  Favorite and Watched.
- Search across movies, series and episodes.
- Playback: direct play when the server says the file is directly playable, otherwise a
  progressive HTTP MP4 transcode. Progress is reported back so resume works.
- Settings: maximum streaming bitrate, sign out.
- English and Italian UI selected by the device language, English by default.

## Requirements

- Visual Studio 2013 with the Windows Phone 8.1 SDK: MSBuild 12, the WindowsXaml v12.0
  targets, and `Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll` in the VS2013
  `Common7/IDE/PublicAssemblies` folder.
- A running Jellyfin server the phone can reach: same network, and on Windows the
  firewall must allow the server's port (8096 by default).
- No package restore: the build needs no NuGet packages.

## Build

Run from the repository root, in bash (Git Bash on Windows):

```bash
# Debug, x86 (emulator)
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# Release, ARM (phone)
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Both end with `Your package has been successfully created.`, under
`JellyfinPlayer/JellyfinPlayer/AppPackages/`. Use the `-p:` prefix — Git Bash mangles the
`/p:` form. Deploy with Visual Studio (**Deploy**), or with the `Add-AppDevPackage.ps1`
inside the `AppPackages/..._Test` folder.

## Tests

```bash
# build the test project first
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj \
  -p:Configuration=Debug -p:Platform=AnyCPU -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# then run it
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings
```

A passing run prints `N/N test(s) Passed`. Some tests talk to a real server: copy
`JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local.example` to
`test.settings.local`, fill in the password, and **never commit it**. Without a configured
server those integration tests report `Inconclusive`.

## Documentation

- [`agents/README.md`](agents/README.md) — what the app does and how it is laid out.
- [`agents/BUILD.md`](agents/BUILD.md) — every build and test command.
- [`agents/TESTING.md`](agents/TESTING.md) — unit vs. integration tests, and the manual
  on-device checklist (language switch, logo, autodiscovery).
- [`agents/ARCHITECTURE.md`](agents/ARCHITECTURE.md),
  [`agents/CONVENTIONS.md`](agents/CONVENTIONS.md),
  [`agents/JELLYFIN_API.md`](agents/JELLYFIN_API.md),
  [`agents/MAINTENANCE.md`](agents/MAINTENANCE.md) — the internals.
- [README in italiano](README.it.md).
