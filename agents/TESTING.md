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
  Stop returns and the position is saved.
- **Settings**: bitrate persists; Sign out returns to login.
