# Jellyfin API usage

Jellyfin REST API v10. Base URL is user-supplied, e.g. `http://192.168.1.10:8096`.
All server JSON is PascalCase.

## Authentication

Login sends:

```
POST /Users/AuthenticateByName
Authorization: MediaBrowser Client="JellyfinPlayer", Device="WindowsPhone", DeviceId="<guid>", Version="1.0.0"
{ "Username": "<user>", "Pw": "<password>" }
```

The response gives `AccessToken`, `User.Id` and `ServerId`. Every later request
carries `Authorization: MediaBrowser Token="<AccessToken>", Client="...",
Device="...", DeviceId="...", Version="..."` — all built by
`Shared/JellyfinAuthHeader.cs`. The device id is generated once and stored in
`IsolatedStorageSettings`.

> **Why not `X-Emby-Authorization` / `X-Emby-Token`?** Jellyfin 12 ships with
> `ServerConfiguration.EnableLegacyAuthorization` **off** (and force-disables it
> on upgrade), so those two headers are rejected by default: the server never
> learns the client name, and login fails with
> `ArgumentNullException (Parameter 'request.App')` → HTTP 400 while every later
> call answers 401. The `Authorization: MediaBrowser ...` scheme is not gated by
> that setting and works on 10.x **and** 12.x, so it is the only form this client
> sends — no server-version detection.
>
> Checked against a live 12.2 server: login, browsing and image fetches with
> `ApiKey` answer 200, while the same calls with `api_key` answer 401.

**Image and media URLs** append `?ApiKey=<token>` (a `BitmapImage` cannot set
headers, and `MediaElement` cannot either), so `JellyfinUrls.WithKey` adds `ApiKey`
to every URL a control fetches itself. The legacy `api_key` parameter is gone in
Jellyfin 12: `12.0` reads only `ApiKey` and the `Authorization` header, and the
earlier spelling answers 401. `ApiKey` works on 10.x too, so no version detection
is needed. `PlaybackUrlResolver.EnsureKey` leaves a URL alone when the server
already signed it with either spelling.

## Endpoints used

| Purpose | Request |
| --- | --- |
| Server info | `GET /System/Info/Public` |
| Public users | `GET /Users/Public` |
| Own user record | `GET /Users/{userId}` (fills `User.Name` for the account picker) |
| Login | `POST /Users/AuthenticateByName` |
| Libraries | `GET /Users/{userId}/Views` |
| Items (paged) | `GET /Users/{userId}/Items?ParentId=&Recursive=true&IncludeItemTypes=&SortBy=SortName&SortOrder=Ascending&StartIndex=&Limit=&Fields=Overview,PrimaryImageAspectRatio` |
| Item detail | `GET /Users/{userId}/Items/{itemId}` |
| Seasons | `GET /Shows/{seriesId}/Seasons?UserId=` |
| Episodes | `GET /Shows/{seriesId}/Episodes?UserId=&SeasonId=` |
| Search | `GET /Users/{userId}/Items?Recursive=true&IncludeItemTypes=Movie,Series,Episode&Limit=50&SearchTerm=` |
| Continue watching | `GET /Users/{userId}/Items/Resume?MediaTypes=Video&Limit=` |
| Next up | `GET /Shows/NextUp?UserId=&Limit=` |
| Playback info | `POST /Items/{itemId}/PlaybackInfo?UserId=` |
| Playback start/progress/stop | `POST /Sessions/Playing` · `/Sessions/Playing/Progress` · `/Sessions/Playing/Stopped` |
| Watched | `POST`/`DELETE /Users/{userId}/PlayedItems/{itemId}` |
| Favorite | `POST`/`DELETE /Users/{userId}/FavoriteItems/{itemId}` |
| Primary image | `GET /Items/{itemId}/Images/Primary?maxHeight=&quality=90` |
| Backdrop | `GET /Items/{itemId}/Images/Backdrop/0?maxWidth=&quality=90` |
| Genres | `GET /Genres?UserId=&ParentId=` (the library's own genres) |
| Playlists | `GET /Users/{userId}/Items?IncludeItemTypes=Playlist&Recursive=true` |
| Playlist entries | `GET /Playlists/{playlistId}/Items?UserId=` (keeps playlist order) |

## Ticks

1 second = **10,000,000** ticks. Use `Ticks.ToTimeSpan` / `Ticks.FromTimeSpan` /
`Ticks.Format`. `PlaybackPositionTicks` on `UserData` is the resume position.

## Playback: direct vs transcode

`GetPlaybackInfoAsync` posts `DeviceProfileBuilder.DefaultProfile()`:

- **Direct play:** `mp4` container, `h264` video, `aac,mp3` audio. Matroska is
  deliberately not claimed: the Windows Phone Silverlight player is only verified
  for MP4, and an over-claimed container suppresses the transcode fallback.
- **Transcode:** `mp4` container over **`http`** (progressive), `h264`+`aac`.
- **Subtitles:** text and image formats (`srt`, `subrip`, `ass`, `ssa`, `vtt`,
  `webvtt`, `mov_text`, `pgssub`, `dvdsub`, `dvbsub`) with `Method="Encode"`, so
  the server burns the chosen track into the transcode.

`PlaybackService.PrepareAsync` picks the first source with `SupportsDirectPlay`
and a `DirectStreamUrl`; otherwise the first with `SupportsTranscoding` and a
`TranscodingUrl`. Both are played with `MediaElement.Source`.

**Library filter.** `Items` accepts `GenreIds=` (one id is enough); the client
resets paging when the genre or the sort changes.

**Offline copy.** A download asks for the same `PlaybackInfo` at the preferred
bitrate and stores the progressive MP4 transcode URL's body. Only that URL is
used: any other container the server might offer is not guaranteed to play on the
phone.

**Track selection.** `PlaybackInfo` carries `AudioStreamIndex` and
`SubtitleStreamIndex` when the player asked for them. Choosing the default audio
and no subtitle is sent as `Nothing`, so the item still direct-plays. Any other
choice goes through the progressive MP4 URL with `AudioStreamIndex` and
`SubtitleStreamIndex` (plus `SubtitleMethod=Encode` for subtitles): a static file
cannot remux an audio track or draw a subtitle over itself. `Shared/TrackOptions`
owns which index counts as the default, and the player restarts at the same
position when a choice changes.

> **Why not HLS?** The Windows Phone Silverlight `MediaElement` has no adaptive
> streaming, so HLS cannot be played. A progressive MP4 transcode is the fallback
> that `MediaElement` plays natively.
