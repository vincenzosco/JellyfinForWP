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
`Shared/JellyfinAuthHeader.vb`. The device id is generated once and stored in
`LocalSettings`.

> **Why not `X-Emby-Authorization` / `X-Emby-Token`?** Jellyfin 12 ships with
> `ServerConfiguration.EnableLegacyAuthorization` **off** (and force-disables it
> on upgrade), so those two headers are rejected by default: the server never
> learns the client name, and login fails with
> `ArgumentNullException (Parameter 'request.App')` → HTTP 400 while every later
> call answers 401. The `Authorization: MediaBrowser ...` scheme is not gated by
> that setting and works on 10.x **and** 12.x, so it is the only form this client
> sends — no server-version detection.

**Image URLs** instead append `?api_key=<token>` (a `BitmapImage` cannot set
headers, and `MediaElement` cannot either), so `JellyfinUrls` adds `api_key` to
every media URL.

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

## Ticks

1 second = **10,000,000** ticks. Use `Ticks.ToTimeSpan` / `Ticks.FromTimeSpan` /
`Ticks.Format`. `PlaybackPositionTicks` on `UserData` is the resume position.

## Playback: direct vs transcode

`GetPlaybackInfoAsync` posts `DeviceProfileBuilder.DefaultProfile()`:

- **Direct play:** `mp4,mkv` container, `h264` video, `aac,mp3` audio.
- **Transcode:** `mp4` container over **`http`** (progressive), `h264`+`aac`.
- **Subtitles:** `srt`/`ass` with `Method="Encode"` (burned in by the transcode).

`PlaybackService.PrepareAsync` picks the first source with `SupportsDirectPlay`
and a `DirectStreamUrl`; otherwise the first with `SupportsTranscoding` and a
`TranscodingUrl`. Both are played with `MediaElement.Source`.

> **Why not HLS?** WP8.1 has no `AdaptiveMediaSource` (verified: it is absent from
> the WP8.1 `Windows.winmd`), so HLS cannot be played. A progressive MP4 transcode
> is the fallback that `MediaElement` plays natively.
