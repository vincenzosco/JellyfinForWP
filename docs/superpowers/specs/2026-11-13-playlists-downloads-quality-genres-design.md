# Playlists, offline downloads, quality and genre filters

## Why

The client can browse, play, keep track of where it stopped, pick tracks, continue
to the next episode and sort a library. Four things a Jellyfin client is expected
to do are still missing, and each one is achievable on Windows Phone 8.1
Silverlight without a new dependency:

- **Playlists** exist on the server, but the app never asks for them.
- **Offline downloads** matter most on a phone with a flaky network; Silverlight
  can stream to `IsolatedStorageFile` and `MediaElement` can play a local stream.
- **Quality** is a setting that can only be changed from the settings screen, and
  only after leaving playback. A client lets the viewer lower it mid-film.
- **Genres** are visible in the library only as metadata; there is no way to say
  "only the comedies".

## Playlists

- `GET /Users/{userId}/Items?IncludeItemTypes=Playlist&Recursive=true` lists the
  playlists; `GET /Playlists/{id}/Items?UserId=` lists the entries.
- Home grows a **Playlists** section (horizontal posters, hidden when empty). A
  playlist opens `PlaylistPage`: its entries with a **Play all** button.
- The player gains a queue: a navigation parameter can carry the item plus the
  remaining entries. When an item ends, the queue's next entry plays; only when
  the queue is exhausted does the episode rule apply.
- `Shared/PlayQueue.vb` owns "what comes after this index", so the rule is tested
  without a page.

## Offline downloads

- **Download** on the item page: `PlaybackInfo` at the preferred bitrate, then the
  progressive MP4 transcode stream is copied into `IsolatedStorageFile` with a
  percentage on the button. The original file is never downloaded: only MP4 is
  playable here.
- Metadata (name, series, duration, resume position, size) lives in one JSON
  document next to the files. `Shared/Downloads.vb` owns the file name, the size
  label and the percentage, so those are tested.
- **Downloads** on Home lists what is stored; a tap plays it offline, the manage
  button opens `DownloadsPage` where each file can be deleted.
- Offline playback feeds `MediaElement.SetSource` from the stored stream, uses the
  stored resume position and never talks to the server. The stored position is
  updated when playback stops.
- A download replaces its previous copy of the same item. A removal deletes the
  file and its metadata.

## Quality

- The player's track panel gains a **Quality** list: 2, 4, 8 and 20 Mbps.
- Choosing one saves it as the device preference (the settings screen shows the
  same value) and restarts the item at the same position, because a bitrate is a
  different `PlaybackInfo`/transcode, not a switch inside the running stream.

## Genre filter

- The library header gains a **Genre** button; `GET /Genres?ParentId=` fills a
  panel whose first row is *All genres*.
- A genre filters the item query with `GenreIds=`; the filter, the sort and paging
  combine. Changing either filter restarts from the first page.
- `Shared/LibraryFilters.vb` builds the choices (all first, unnamed genres
  skipped), so the panel model is tested.

## Out of scope

Live TV, casting, subtitle rendering on the device for external files, playlist
editing (create/add/remove), and background downloads (the download runs while the
item page is open; leaving the app stops it).
