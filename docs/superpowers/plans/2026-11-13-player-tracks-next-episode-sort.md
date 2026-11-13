# Player tracks, next episode and library sorting — Implementation Plan

**Goal:** finish the three client features a Jellyfin user notices are missing:
audio/subtitle selection, playing the next episode, and sorting a library.

**Spec:** `docs/superpowers/specs/2026-11-13-player-tracks-next-episode-sort-design.md`

**Architecture:** the decisions live in `Shared/` (`TrackOptions`, `NextEpisode`,
URL builders) and are covered by the desktop MSTest project; the pages only wire
buttons to them. The player restarts the current item through a transcode when a
track other than the default is chosen, and starts the next episode in place.

## Global Constraints

- VB 12, Windows Phone Silverlight 8.1, VS2013/MSBuild 12, no NuGet.
- No new user-visible literal: every string comes from both `.resx` files.
- Keep direct play for the default audio with no subtitles.
- No device-only API in `Shared/`; it must keep compiling for .NET 4.5 MSTest.
- Commit subjects short and human, no attribution.

## Task 1: Track options (Shared, tested)

**Files:** `Shared/TrackOptions.vb`, `JellyfinPlayer.Core.Tests/TrackOptionsTests.vb`

- [ ] `TrackOption` (`Index As Integer?`, `Label As String`), `Audio(streams)`,
      `Subtitles(streams, offLabel)`, `DefaultAudioIndex(streams)`,
      `AudioSelection(streams, index)`, `Describe(stream)`.
- [ ] Tests: only audio/subtitle streams; Off first; DisplayTitle wins, then
      language + codec, then "Track n"; default flagged or first; the default
      index maps to `Nothing`.
- [ ] Run the suite.

## Task 2: Next episode (Shared, tested)

**Files:** `Shared/NextEpisode.vb`, `NextEpisodeTests.vb`, `Shared/Models.vb`

- [ ] `BaseItemDto` gains `SeriesId`, `SeasonId`, `ParentId`.
- [ ] `NextEpisode.Find(current, episodes)` returns the first episode after the
      current one by season then number, or `Nothing`.
- [ ] Tests: same season, next season, last episode, missing numbers, the current
      item never returned.

## Task 3: Playback request and URLs carry the chosen tracks (Shared, tested)

**Files:** `Shared/Models.vb`, `Shared/DeviceProfileBuilder.vb`,
`Shared/JellyfinUrls.vb`, `Shared/PlaybackUrlResolver.vb`,
`Shared/JellyfinApiClient.vb`, `UrlsTests.vb`, `PlaybackUrlResolverTests.vb`,
`BrowseTests.vb`

- [ ] `PlaybackInfoRequestDto` gains `AudioStreamIndex`/`SubtitleStreamIndex`;
      `ToRequest` and `GetPlaybackInfoAsync` pass them through.
- [ ] `VideoTranscode` and `TranscodeUrl` add `AudioStreamIndex`,
      `SubtitleStreamIndex` and `SubtitleMethod=Encode` only when given.
- [ ] Subtitle profiles also cover image subtitles (`pgssub`, `dvdsub`, `dvbsub`)
      and the common text formats, so the server can burn in what it is offered.
- [ ] `GetItemsAsync` exposes `sortBy`/`sortOrder` with the current defaults.
- [ ] Tests for each URL and for the sort parameters.

## Task 4: Player tracks UI

**Files:** `Pages/PlayerPage.xaml`, `PlayerPage.xaml.vb`,
`Services/PlaybackService.vb`

- [ ] `PlaybackService.PrepareAsync(item, ticks, audioIndex, subtitleIndex)`:
      keeps direct play when both are `Nothing`; otherwise picks a transcodable
      source and builds the URL with the chosen indices.
- [ ] `PlaybackPlan` carries `Tracks`, `AudioStreamIndex`, `SubtitleStreamIndex`.
- [ ] Player: tracks button, panel with two lists, *Off* first, selection restarts
      at the current position, panel closes when the controls hide.
- [ ] Strings in both languages.

## Task 5: Next episode in the player

**Files:** `Pages/PlayerPage.xaml.vb`, `Services/PlaybackService.vb`

- [ ] `PlaybackService.NextEpisodeAsync(client, userId, current)` tries the
      current season, then later seasons; swallows failures.
- [ ] `Media_Ended` reports stop, then starts the next episode in place; no next
      episode keeps today's behaviour.

## Task 6: Library sorting

**Files:** `Pages/LibraryPage.xaml`, `LibraryPage.xaml.vb`

- [ ] Sort button in the header, cycling name/date added, reload from page 0.

## Task 7: Docs and full verification

- [ ] READMEs (feature lists), `agents/JELLYFIN_API.md` (new parameters),
      `agents/TESTING.md` (device checks), architecture note.
- [ ] Build Debug|x86 and Release|ARM; full MSTest; localization gates.
- [ ] Bug hunt over the new code, fix what it finds, record the report.

## Verification the automated tests cannot do

Tracks switching and burned-in subtitles on a device, the next episode actually
starting after the credits, and the sort button's effect on a real library.
