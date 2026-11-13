<p align="center">
  <img src="JellyfinPlayer/JellyfinPlayer/Assets/BrandMark.png" width="128" alt="JellyfinPlayer logo">
</p>

<h1 align="center">JellyfinPlayer</h1>

<p align="center">
  <strong>Browse and play your Jellyfin library on a Windows Phone 8.1 device.</strong><br>
  The app is written in VB.NET and uses the phone's built-in Silverlight XAML and media player.
</p>

<p align="center">
  <a href="#install">Install</a> ·
  <a href="#use-the-app">Use the app</a> ·
  <a href="#troubleshooting">Troubleshooting</a> ·
  <a href="agents/README.md">Developer guide</a> ·
  <a href="README.it.md">Italiano</a>
</p>

JellyfinPlayer connects to a Jellyfin server from a Windows Phone 8.1 device. It is a native Windows Phone Silverlight 8.1 app (`.xap`), not a Windows Phone Store/WinRT app. It needs a reachable Jellyfin server; it does not include one.

This repository has no `LICENSE` file, so reuse and redistribution terms are not stated here.

## What it does

- Finds Jellyfin servers on the local network using UDP discovery on port `7359`. You can also enter a server address manually.
- Signs in through `/Users/AuthenticateByName` and can keep multiple accounts on one phone.
- Shows Continue watching, Next up, libraries, paged library items, series seasons, item details, and search results for movies, series, and episodes.
- Plays directly when the server offers a compatible source. Otherwise it asks Jellyfin for a progressive MP4 transcode. Playback progress is sent back to the server.
- If direct playback fails, tries transcoding once. If transcoding fails, the player can offer the direct stream as a fallback.
- Provides full-page playback (system tray hidden, landscape allowed), seek back 10 seconds / forward 30 seconds, mute, fit/fill, and playback progress controls.
- Includes English and Italian UI text, selected using the device language.

**Good for:** browsing and playing a Jellyfin library from an older Windows Phone 8.1 device.

**Not for:** Live TV, offline downloads, offline sync, collections management, selecting audio tracks, or choosing external subtitles. Subtitles can be burned into a server transcode. See [known limitations](agents/MAINTENANCE.md#known-limitations).

## Requirements

- A Windows Phone 8.1 device or emulator.
- Visual Studio 2013 with the Windows Phone 8.1 SDK (Silverlight) to build and deploy from source.
- A Jellyfin server reachable from the phone. Port `8096` is the default for HTTP.

Windows Phone 8.1 is an old platform. This project targets that platform; it is not a current Windows, Android, or iOS app.

## Install

There is no verified store listing or release download linked from this repository. To build and deploy the app, use a Windows machine with Visual Studio 2013 and the Windows Phone 8.1 SDK.

Open `JellyfinPlayer/JellyfinPlayer.sln` in Visual Studio 2013, select the `x86` emulator or `ARM` device target, then build and deploy the `JellyfinPlayer` project.

To build from Git Bash with MSBuild 12:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

For an ARM release build:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

The generated Silverlight app package (`.xap`) is written under `JellyfinPlayer/JellyfinPlayer/bin/<platform>/Debug/`, for example `JellyfinPlayer_Debug_x86.xap`. Deploy from Visual Studio or use the generated package.

> In Git Bash, use `-p:` for MSBuild properties. The `/p:` form can be converted into a path and break the command.

## Set up the server connection

1. Start the app on the phone.
2. If Jellyfin discovery works on the local network, the server address is filled in automatically. Otherwise, enter the server URL, for example `http://192.168.1.20:8096`.
3. Sign in with a Jellyfin username and password.
4. Open a library, choose an item, and select Play or Resume.

The phone and server need network connectivity to each other. Server discovery uses UDP broadcast, which may not cross subnets or VLANs; manual address entry is available when discovery cannot reach the server.

## Use the app

- **Home:** continue watching, next episodes, and libraries.
- **Library:** scroll through the paged item grid.
- **Series:** choose a season to view its episodes.
- **Item details:** play or resume, and toggle Favorite or Watched.
- **Accounts:** switch between saved logins without re-entering each password.
- **Player:** use the on-screen controls to seek, mute, change fit/fill, or enter fullscreen. Pressing the phone's Back button exits fullscreen before leaving the player page.
- **Settings:** change the maximum streaming bitrate or sign out.

## Troubleshooting

<details>
<summary>The app does not find my Jellyfin server</summary>

Discovery sends a UDP broadcast on port `7359`; it may be blocked by the network or may not cross a subnet/VLAN. Enter the server address manually, and check that the phone can reach the Jellyfin server's HTTP port (usually `8096`).
</details>

<details>
<summary>Jellyfin does not accept the server address</summary>

Enter the base URL, such as `http://192.168.1.20:8096`, not a library or API endpoint. The server must be reachable from the phone. For HTTPS, the server needs a certificate trusted by Windows Phone; self-signed certificates are not trusted by this app.
</details>

<details>
<summary>A video will not play</summary>

The phone's built-in player supports a limited set of formats. JellyfinPlayer asks the server for a direct stream or a progressive MP4 transcode, and retries once using a transcode after a direct-stream failure. If both options fail, check the server's playback/transcoding configuration and try the offered direct-play fallback where available.
</details>

<details>
<summary>The build fails in Git Bash at an MSBuild property</summary>

Use `-p:Configuration=Debug`, not `/p:Configuration=Debug`. Git Bash may rewrite arguments that start with `/` as filesystem paths.
</details>

## Build and test

Build the shared-logic test project with the VS2013 toolchain, then run the unit tests with MSTest v1:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj \
  -p:Configuration=Debug -p:Platform=AnyCPU -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

MSYS_NO_PATHCONV=1 "/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings \
  /category:"!Integration" /noresults
```

`MSYS_NO_PATHCONV=1` keeps Git Bash from rewriting MSTest's `/testcontainer:` arguments. The command above skips integration tests; see [testing instructions](agents/TESTING.md) for the local server settings needed to run them.

## How it is built

The app's Silverlight services and XAML pages are in `JellyfinPlayer/JellyfinPlayer/`. The Jellyfin API client and other platform-independent logic are in `JellyfinPlayer/Shared/`, which is linked into both the phone app and the desktop test project.

More detail:

- [Developer entry point](agents/README.md)
- [Architecture](agents/ARCHITECTURE.md)
- [Build commands](agents/BUILD.md)
- [Testing and device checklist](agents/TESTING.md)
- [Jellyfin API usage](agents/JELLYFIN_API.md)
- [Maintenance and limitations](agents/MAINTENANCE.md)
- [Coding conventions](agents/CONVENTIONS.md)
