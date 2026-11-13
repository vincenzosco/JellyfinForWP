# Player tracks, next episode and library sorting

## Status

Requested in chat on 2026-11-13: "implement the remaining features a real Jellyfin
client has". Scope agreed by the shape of the app: what the Windows Phone
Silverlight surface and the Jellyfin API can actually deliver on this device, not
a copy of the web client.

## Why these three

A Jellyfin client is recognisable by a short list of things this app still cannot
do. The list was checked against the API client and the screens:

| Missing | Verdict |
| --- | --- |
| Choosing audio and subtitle tracks | **In scope.** The server already returns `MediaStreams` in `PlaybackInfo`; the app ignored them, so a multi-audio file always played the default and subtitles were never selectable. |
| Continuing to the next episode | **In scope.** An episode ends and the app stops; every real client moves on to the next one. |
| Sorting a library | **In scope.** The URL builder already accepted `SortBy`/`SortOrder`; the page hard-coded name order. |
| Offline downloads, Live TV, casting, collections editing | Out of scope. Each is a subsystem of its own, and none fits a WP8.1 app without a second design. |
| Subtitle rendering over direct play | Out of scope. The Silverlight `MediaElement` cannot draw a separate subtitle stream; subtitles are burned in by a server transcode, which is what the device profile already declares. |

## Audio and subtitle tracks

- `PlaybackInfo` already returns each source's `MediaStreams` (`Index`, `Type`,
  `Language`, `DisplayTitle`, `IsDefault`). `Shared/TrackOptions.vb` turns them
  into the picker's rows and decides which audio index counts as "the default".
- The player gets a tracks button and a panel listing **Audio** and **Subtitles**
  (with an explicit *Off* first).
- Picking an audio track other than the default, or any subtitle, restarts the
  same item at the current position through a **progressive MP4 transcode** with
  `AudioStreamIndex` / `SubtitleStreamIndex` (and `SubtitleMethod=Encode`).
  Direct play is kept when the default audio and no subtitles are chosen, so the
  common case does not pay for a transcode.
- Picking *Off* for subtitles returns to direct play when the track is the default
  audio.
- The selection is not persisted: it belongs to the playback it was made for.

## Next episode

- When an episode ends, the player asks for the series' episodes of the current
  season, then for later seasons, and starts the next one in place (same page, so
  Back does not walk into a finished player).
- `Shared/NextEpisode.vb` owns the ordering rule: by season, then by episode
  number, ignoring the episode that just ended and anything before it.
- If there is no next episode, or the request fails, playback simply stays at the
  end as it does today. A failure here must never replace the video with an error.
- A movie or any non-episode item keeps the current behaviour.

## Library sorting

- The library page gets a sort button cycling **Name** and **Date added**.
- `GetItemsAsync` exposes the `SortBy`/`SortOrder` the URL builder already had;
  switching sort reloads from the first page.

## Constraints

- VB 12, Windows Phone Silverlight 8.1, no NuGet, no new controls beyond the
  built-in `ListBox`/`Button`.
- All new user-visible text goes into `Resources.resx` and `Resources.it-IT.resx`.
- Shared, testable logic stays in `Shared/`; pages keep only wiring.
- Every new string used from code exists in both languages; `LocalizationTests`
  enforces it.

## Test plan

- `TrackOptionsTests`: audio/subtitle filtering, "Off" first, label fallbacks,
  default-audio detection, passing `Nothing` for the default.
- `NextEpisodeTests`: next in the same season, first of the next season, nothing
  after the last episode, nothing without numbers, ordering by season then number.
- `UrlsTests`: transcode URL carries `AudioStreamIndex`, `SubtitleStreamIndex` and
  `SubtitleMethod=Encode` only when asked; the profile declares image subtitles.
- `BrowseTests`: the sort parameters reach the request URL.
- Device checklist: tracks switch mid-playback, subtitle burn-in, next episode,
  sort cycling.

## What cannot be verified here

The `MediaElement` itself: whether the phone decodes the chosen audio track and
whether burned-in subtitles appear are manual checks, listed in
`agents/TESTING.md`. The unit tests cover the URLs and the decisions.
