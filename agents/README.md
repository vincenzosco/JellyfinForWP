# JellyfinPlayer

A Jellyfin client for **Windows Phone 8.1** (WinRT XAML), VB.NET, built with
VS2013 / MSBuild 12.

## Features

- Connect to a Jellyfin server by URL and sign in (`POST /Users/AuthenticateByName`).
- Home: Continue watching (`/Items/Resume`), Next up (`/Shows/NextUp`), and the library list (`/Users/{id}/Views`).
- Library grid with `StartIndex`/`Limit` paging.
- Series page with season picker and episode list.
- Item detail with Play/Resume, Favorite and Watched toggles.
- Search across movies, series and episodes.
- Playback: direct play when the server says the file is directly playable,
  otherwise a progressive HTTP MP4 transcode. Progress is reported back to the
  server (`/Sessions/Playing*`) so resume works.
- Settings: max streaming bitrate, sign out.
- Logo and splash taken from the remove.bg Jellyfin logo.

## Layout

```
JellyfinPlayer/
  Shared/                 platform-agnostic logic (linked into both projects)
  JellyfinPlayer/         the WP8.1 app (XAML pages + WinRT services)
  JellyfinPlayer.Core.Tests/  desktop MSTest suite over Shared/
tools/generate_assets.py  regenerates the .scale-240 assets from a source logo
```
