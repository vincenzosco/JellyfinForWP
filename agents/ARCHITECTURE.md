# Architecture

```
                 +---------------------------+
                 |  Shared/  (linked .vb)    |
                 |  DTOs, Json, JellyfinUrls,|
                 |  Ticks, DeviceProfile,    |
                 |  IHttpTransport,          |
                 |  HttpTransport,           |
                 |  JellyfinApiClient        |
                 +------------+--------------+
                              | linked as <Compile Include="..\Shared\*.vb">
              +---------------+----------------+
              |                                |
   JellyfinPlayer (WP8.1 app)      JellyfinPlayer.Core.Tests (MSTest, .NET 4.5)
   XAML pages + WinRT services     runs Shared/ on the desktop with FakeTransport
```

## Why linked source, not a project reference

A plain .NET 4.5 library cannot be referenced from a WinRT WP8.1 app, and a PCL
adds project-system complexity. Instead both projects compile the **same**
`Shared/*.vb` files. That keeps the API client unit-testable on the desktop while
shipping it unchanged in the app.

**Constraint:** `Shared/` may only use APIs present in both .NET 4.5 and WP8.1 —
`System.Net.Http`, `System.Runtime.Serialization.Json`, `System.Threading.Tasks`,
`System.Collections.Generic`, `System.Text`, `System.Uri`. No WinRT types.

## The test seam

`IHttpTransport` is the only I/O boundary. `JellyfinApiClient` depends on it, so
tests inject `FakeTransport` (records the last request, returns canned JSON), and
the app injects `HttpTransport` (a real `HttpClient`, 15 s timeout). Failure
messages are produced by the pure `HttpTransport.MapException`.

## App-side layers

- `Services/AppSession.vb` — the single source of truth for server URL, token,
  user id, device id and bitrate, persisted in `ApplicationData.Current.LocalSettings`.
  `CreateClient()` builds a ready-to-use `JellyfinApiClient`.
- `Services/NavigationService.vb` — navigate/go-back on the root `Frame`.
- `Services/ImageHelper.vb` — image URLs (they carry `?api_key=` because
  `BitmapImage` cannot set headers) and the "does this item have a poster" check.
- `Services/PosterItem.vb` — view model for every grid/list; deliberately avoids
  XAML value converters.
- `Services/PlaybackService.vb` — turns a `PlaybackInfo` response into a
  `PlaybackPlan` (direct stream or MP4 transcode).
- `Services/ItemNavigator.vb` — routes an item to `SeriesPage` or `ItemDetailPage`.

## Data flow

`Page.OnNavigatedTo` → `AppSession.Current.CreateClient()` → `JellyfinApiClient`
(URLs from `JellyfinUrls`, JSON via `Json`) → `IHttpTransport` → server.
