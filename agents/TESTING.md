# Testing

Two layers: **unit tests** over `Shared/` (no server) and **integration tests**
that hit a real Jellyfin server. Both live in `JellyfinPlayer.Core.Tests` and run
with MSTest v1 (VS2013).

## Commands

Both need the deployment-disabling settings file, otherwise MSTest copies the
assembly to `TestResults/<run>/Out` and the local config file is not found:

```bash
# everything (unit + integration)
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings

# only the integration subset
... /category:Integration
```

Build the test project first (`msbuild ... JellyfinPlayer.Core.Tests.vbproj`), or
build the whole solution.

## Configuring the integration tests

They are read-only and never change server state. Settings are resolved in this
order, first value wins:

1. Environment: `JELLYFIN_TEST_URL`, `JELLYFIN_TEST_USER`, `JELLYFIN_TEST_PASSWORD`.
2. `test.settings.local` next to the test assembly (copied from the project dir).
3. Built-in defaults: URL `http://192.168.0.108:8096`, user `vincenzo`, empty password.

`test.settings.local` is **git-ignored** — copy `test.settings.local.example` and
fill in the password. **Never commit it.** Before any push:

```bash
git check-ignore -v JellyfinPlayer/JellyfinPlayer.Core.Tests/test.settings.local
git grep -n '<the password>'   # must not match
```

### Behaviour when unconfigured

Tests call `Assert.Inconclusive("Jellyfin test server not configured…")`. MSTest v1
reports them as `Inconclusive` and **exits 1** (the run is not "passed"). That is
why the integration subset is normally run separately; a machine with no server
should expect `Inconclusive`, not a green run.

## What the integration tests cover

| Test | Checks |
| --- | --- |
| `Server_is_reachable_and_identifies_itself` | `/System/Info/Public` |
| `Login_with_configured_credentials_returns_a_token` | `AuthenticateByName`, clear 401 message |
| `Views_are_readable` | library list |
| `Items_load_for_the_first_library` | paged items |
| `Search_returns_results_for_an_existing_item_name` | search |
| `Playback_info_returns_a_playable_source_for_the_first_video` | PlaybackInfo + `PlaybackUrlResolver` produce a URL |
| `Resume_list_is_readable` | continue-watching list |

## What the unit tests cover for the player

No UI automation exists on WP8.1, so the player's *decisions* live in `Shared/` and are
covered by MSTest: `SeekTargetTests` (a seek never leaves the video, an unknown duration
never produces one), `TicksTests.FormatSpan*` (the elapsed/total clock), and
`PlayerSurfaceTests` (entering and leaving the full-page player, the screen staying awake
while playing and not while paused, and `RestoreAll` putting the system tray, portrait
rotation and the screen timeout back however the screen was left).
`Services/SilverlightPlayerShell.vb` is the Windows Phone binding of the same interface and
is verified by the build plus the checklist below.

## Localization consistency tests

`LocalizationTests` read the real files — copied next to the test assembly by
`JellyfinPlayer.Core.Tests.vbproj` — instead of a hand-made fixture: the two `.resx`
files, every XAML page, the manifest and the app sources. They fail when English and
Italian drift apart (missing key, empty value, different `{n}` placeholders), when a
`Localized.Text` key has no resource, when a key used from code does not exist, when the manifest
stops declaring `en-US`/`it-IT`, when a page prints a raw `ex.Message` instead of
`Localized.DescribeError(ex)`, or when a user-visible XAML literal is left untranslated
(only the brand name and the example URL are allowed literals).

## Project and markup checks

`SilverlightProjectTests` fails when the app drifts back to WinRT: a `WindowsXaml`
target, `NETFX_CORE`, `AppContainerExe`, `Windows.UI.Xaml`, `Windows.Storage`,
`DatagramSocket`, `ResourceLoader`, `ProgressRing`, `ThemeResource`, `ms-appx` or
`x:Uid` in a maintained file. It also checks that every screen is a
`phone:PhoneApplicationPage`, that the startup route and every project page exist,
and that both manifests agree on product id and version. `XamlResourceTests` rejects
a `<Geometry>` object element and a dangling `{StaticResource}`.

## Verification the automated tests cannot do

WP8.1 has no UI automation, so the app itself is verified manually on the emulator
or a device:

- **Autodiscovery**: launch with no saved server on a LAN with the server running →
  the address field fills within ~3 s and the status names the server.
- **No server**: launch with the server stopped → "No server found automatically.
  Enter the address." and manual entry still works.
- **Several servers**: status reports the count and lists the addresses.
- **Login → Home**: views, "Continue watching" and "Next up" render.
- **Library**: scroll/paging loads further items.
- **Series**: season picker switches the episode list.
- **Detail**: Play/Resume opens the player; Favorite and Watched toggles persist.
  A page whose markup cannot load shows **nothing at all** - no screen, no error,
  no crash - because `Frame.Navigate` returns `False` and `NavigationFailed` is
  not handled. Treat "the button did nothing" as a page-load failure first, and
  run `XamlResourceTests` to find it without a deploy.
- **Playback**: direct-play a compatible MP4; play a file the server must transcode;
  the close button returns and the position is saved.
- **Playback fallback**: a direct stream that fails (a file this phone cannot open, or a
  stream cut off) retries once through the transcode **on its own** and keeps playing; if
  the transcode fails too, the failure sentence appears together with "Play direct", and
  tapping it plays the original file. A second failure must show the sentence - the player
  never loops between the two URLs.
- **Player fullscreen**: tap the fullscreen button → the status bar and both bars disappear
  and the video fills the screen; turn the phone → the video follows it into landscape; tap
  the video → the bars come back; press the hardware back button → it leaves fullscreen and
  only the next press leaves the screen; leave the screen while fullscreen → the status bar
  and portrait rotation are back on the next screen and the screen timeout works again.
- **Player controls**: pause → the controls stay on screen; play → they hide after ~4 s and
  a tap brings them back; skip back/forward → the position moves by 10 s/30 s and never
  leaves the video; mute → the sound stops and the speaker icon is slashed; fit/fill → the
  video letterboxes or crops; the elapsed time and the total match the video; a video still
  loading shows the ring and "Buffering..."; a failed stream shows a short sentence and the
  "Play direct" fallback, with the controls still reachable.
- **Unknown duration**: open a transcode right after it starts → the total time is never
  wrong and skipping cannot move past the end.
- **Tracks**: open a title with more than one audio track → the tracks button appears;
  pick another language → playback restarts at the same position with that audio; pick a
  subtitle → the subtitle is burned in and the video restarts at the same position; pick
  **Off** → playback returns to direct play. A file with one audio track and no subtitles
  offers no tracks button.
- **Next episode**: let an episode end while a following episode exists → the next one
  starts on its own, from the beginning, on the same screen; the last episode of a series
  simply stops. Back after the automatic switch leaves the player, not the previous episode.
- **Library sort**: the header button switches between name and date added, and the grid
  reloads from the first page in the new order.
- **Genre**: the header genre button lists *All genres* and the library's genres; choosing
  one shows only those items, and combining it with the sort keeps both applied.
- **Quality**: in the player panel, picking a lower bitrate restarts the item at the same
  position; the settings screen shows the same value afterwards.
- **Playlists**: Home shows the playlists the server has; opening one lists its entries,
  **Play all** starts the first and the following entries continue when each ends; the
  last entry ends playback instead of jumping elsewhere.
- **Download**: the item page downloads with a percentage and then offers to remove the
  copy; the Home downloads section and **Manage downloads** list it; playing it with the
  server unreachable starts and resumes from the stored position.
- **Settings**: bitrate persists; the account name is shown; Sign out returns to the
  picker with the account still saved.
- **Language**: set the emulator/device language to Italian → every screen and every
  runtime message (discovery status, errors, search, settings) is Italian; set it to a
  language that is neither EN nor IT (e.g. French) → everything is English.
- **Logo**: the login page and the home header show `Assets/BrandMark.png` immediately
  left of the "Jellyfin" wordmark.
- **An address that answers with a web page**: point the app at an address whose port
  serves a web page (e.g. `http://192.168.0.108:80`) → a short sentence saying the
  address answered with a web page instead of the Jellyfin API, **not** HTML on screen.
- **Accounts**: with two logins saved (two users on the same server, or one user on each
  of two servers), launching goes straight into the last one used. Tap the header's switch
  button → the picker lists both, the one in use is marked; tapping the other opens *its*
  Home, and the back button does not walk back into the first account's screens. Remove
  account forgets only that login and leaves the other usable.
- **Sign out keeps the account**: Settings → Sign out → the picker, where that account is
  still listed; tapping it re-enters with **no** password prompt.
- **Refused token**: revoke the app's device from the Jellyfin dashboard (or sign the user
  out from another client), then launch → the app asks for that account's password only,
  with the server and user already filled in; typing it enters Home, and the other accounts
  were never touched.
- **Upgrade from the old WinRT build**: the Silverlight app stores accounts in
  `IsolatedStorageSettings`, which the previous WinRT package did not use, so a saved login
  is **not** carried over. Install over it and confirm the app opens on the login screen with
  manual entry, then signs in normally. This is expected; it is not a regression.
- **Authorization on the device**: after signing in, confirm the server accepts the
  `Authorization: MediaBrowser …` header (Home loads). Silverlight's HTTP stack has header
  restrictions, so a token that arrives in a desktop test is not proof it arrives here; if
  it does not, the API calls fail and the app says so instead of silently sending a query key.
