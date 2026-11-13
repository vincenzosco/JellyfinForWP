# Architecture

```
                 +---------------------------+
                 |  Shared/  (linked .cs)    |
                 |  DTOs, Json, JellyfinUrls,|
                 |  Ticks, DeviceProfile,    |
                 |  IHttpTransport,          |
                 |  HttpTransport,           |
                 |  JellyfinApiClient        |
                 +------------+--------------+
                              | linked as <Compile Include="..\Shared\*.cs">
              +---------------+----------------+
              |                                |
   JellyfinPlayer (WP8.1 app)      JellyfinPlayer.Core.Tests (MSTest, .NET 4.5)
   XAML pages + phone services     runs Shared/ on the desktop with FakeTransport
```

## Why linked source, not a project reference

A plain .NET 4.5 library cannot be referenced from a Windows Phone Silverlight
app, and a PCL adds project-system complexity. Instead both projects compile the **same**
`Shared/*.cs` files. That keeps the API client unit-testable on the desktop while
shipping it unchanged in the app.

**Constraint:** `Shared/` may only use APIs present in both .NET 4.5 and WP8.1 —
`System.Net.HttpWebRequest`, `System.Runtime.Serialization.Json`, `System.Threading.Tasks`,
`System.Collections.Generic`, `System.Text`, `System.Uri`. No phone-only types.

## The test seam

`IHttpTransport` is the only I/O boundary. `JellyfinApiClient` depends on it, so
tests inject `FakeTransport` (records the last request, returns canned JSON), and
the app injects `HttpTransport` (a real `HttpWebRequest`, 15 s timeout). Failure
messages are produced by the pure `HttpTransport.MapException`.

## App-side layers

- `Shared/AccountStore.cs` + `Shared/IAccountStorage.cs` — the saved logins and
  which one is active, persisted as one JSON document. Pure, so the desktop tests
  drive it through an in-memory storage; `Services/LocalAccountStorage.cs` is the
  `IsolatedStorageSettings` binding onto a single value (`JellyfinAccounts`), serialised
  behind one lock because the settings collection is not thread-safe.
- `Services/AppSession.cs` — the façade the pages talk to: the active account's
  server/token/user plus the device-wide id and bitrate. `CreateClient()` builds
  a ready-to-use `JellyfinApiClient` for the active account, so a page cannot
  tell how many logins are saved.
- `Services/AccountItem.cs` — the picker's view model (user name, server, "in
  use" marker); the name falls back to the user id for an account migrated from
  the single-session build until Home fetches it once.
- `Services/NavigationService.cs` — navigate/go-back on the root `PhoneApplicationFrame`
  and the page's Back key handling.
- `Services/SilverlightPlayerShell.cs` — the `IPlayerShell` binding: hides the System
  Tray, allows landscape and disables the idle timeout while the video plays.
- `Services/ImageHelper.cs` — image URLs (they carry `?ApiKey=` because
  `BitmapImage` cannot set headers) and the "does this item have a poster" check.
- `Services/PosterItem.cs` — view model for every grid/list; deliberately avoids
  XAML value converters.
- `Services/PlaybackService.cs` — turns a `PlaybackInfo` response into a
  `PlaybackPlan` (direct stream or MP4 transcode).
- `Services/ItemNavigator.cs` — routes an item to `SeriesPage` or `ItemDetailPage`.

## Shared rules the player leans on

- `Shared/TrackOptions.cs` — the audio/subtitle rows the picker shows, and the
  rule that the default audio is sent as no choice so direct play survives.
- `Shared/NextEpisode.cs` — the episode after the current one, by season then
  number; the service only supplies the list.
- `Shared/PlaybackUrlResolver.cs` — builds the URL for either path, adding
  `AudioStreamIndex`/`SubtitleStreamIndex` when a track was chosen.
- `Shared/PlayQueue.cs` — the playlist queue: `PlayRequest` carries the item and
  its entries, and `NextIndex` says which entry continues.
- `Shared/LibraryFilters.cs` — the genre panel rows, *All genres* first.
- `Shared/QualityOptions.cs` — the bitrates the player offers.
- `Shared/Downloads.cs` — the stored download record and its file-name, size and
  percentage rules. `Services/DownloadStore.cs` keeps them in isolated storage and
  `Services/DownloadService.cs` copies the progressive MP4 into it.

## Data flow

`PhoneApplicationPage.OnNavigatedTo` → `AppSession.Current.CreateClient()` → `JellyfinApiClient`
(URLs from `JellyfinUrls`, JSON via `Json`) → `IHttpTransport` → server.
