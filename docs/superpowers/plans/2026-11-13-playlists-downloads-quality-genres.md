# Plan: playlists, downloads, quality, genres

1. **Rules first** (`Shared/`): `PlayQueue`, `LibraryFilters`, `QualityOptions`,
   `Downloads` (`DownloadRecordDto` and its formatting rules). Tests for each.
2. **URLs and API**: `Genres`, `Playlists`, `PlaylistItems`, `GenreIds` on `Items`;
   `GetGenresAsync`, `GetPlaylistsAsync`, `GetPlaylistItemsAsync`. Extend
   `UrlsTests` and `BrowseTests`.
3. **Quality in the player**: list in the existing panel, reuse the restart path,
   persist through `AppSession`.
4. **Genre in the library**: header button, panel, filter passed to the item query.
5. **Playlists**: `PlaylistPage`, Home section, queue support in `PlayerPage`.
6. **Downloads**: `Services/DownloadStore`, `Services/DownloadService`,
   `DownloadsPage`, item page button, Home section, offline branch in the player.
7. **Localisation**: every new label in both `.resx` files.
8. **Verify**: Debug|x86 and Release|ARM builds, full MSTest suite, bug-hunter pass
   over the new code, then the docs and one commit per feature.
