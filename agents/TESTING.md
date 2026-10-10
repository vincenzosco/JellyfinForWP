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
`PlayerSurfaceTests` (entering and leaving fullscreen, the screen staying awake while
playing and not while paused, and `RestoreAll` putting the status bar, portrait rotation
and the screen timeout back however the screen was left). `Services.WinRtPlayerShell` is
the WP8.1 binding of the same interface and is verified by the build plus the checklist
above.

## Localization consistency tests

`LocalizationTests` read the real files — copied next to the test assembly by
`JellyfinPlayer.Core.Tests.vbproj` — instead of a hand-made fixture: the two `.resw`
files, every XAML page, the manifest and the app sources. They fail when English and
Italian drift apart (missing key, empty value, different `{n}` placeholders), when an
`x:Uid` has no resource, when a key used from code does not exist, when the manifest
stops declaring `en-US`/`it-IT`, when a page prints a raw `ex.Message` instead of
`Localized.DescribeError(ex)`, or when a user-visible XAML literal is left untranslated
(only the brand name and the example URL are allowed literals).

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
- **Playback**: direct-play a compatible MP4; play a file the server must transcode;
  the close button returns and the position is saved.
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
- **Settings**: bitrate persists; Sign out returns to login.
- **Language**: set the emulator/device language to Italian → every screen and every
  runtime message (discovery status, errors, search, settings) is Italian; set it to a
  language that is neither EN nor IT (e.g. French) → everything is English.
- **Logo**: the login page and the home header show `Assets/BrandMark.png` immediately
  left of the "Jellyfin" wordmark.
- **An address that answers with a web page**: point the app at an address whose port
  serves a web page (e.g. `http://192.168.0.108:80`) → a short sentence saying the
  address answered with a web page instead of the Jellyfin API, **not** HTML on screen.
