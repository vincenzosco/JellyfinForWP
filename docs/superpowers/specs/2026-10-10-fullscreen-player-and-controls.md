# Fullscreen player, more playback functions, nicer UI

Date: 2026-10-10
Status: agreed with the requester (chat request), scope decided below

## Request (verbatim, Italian)

> fai in modo che cliccando un pulsante, si possa vedere il film/video a tutto
> schermo, e nella schermata di ripduzione, metti più funzioni, e rendi più bella
> e semplice ui

Three deliverables, all on the playback screen:

1. **A button that shows the video fullscreen.**
2. **More functions** on the playback screen.
3. **A nicer, simpler UI.**

**Scope decision:** "più bella e semplice ui" is read as *the playback screen*,
the sentence's subject. A restyle of the other screens is **out of scope** (it
would touch every page and every resource for no functional gain); the shared
rules it would need — icons in XAML, no literals, both languages — are already in
place from the previous plan, so a later app-wide pass can reuse the player's
patterns.

## What exists today

`JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml` (+ `.xaml.vb`): a
full-bleed `MediaElement` with a title in the top-left, and text buttons
"Pause" / "Play direct" / "Stop" plus a bare `Slider` at the bottom, all
permanently visible. Progress is reported to the server every 10 s and on
stop/end; resume works.

## Verified Windows Phone 8.1 constraints

Probed against
`C:\Program Files (x86)\Windows Phone Kits\8.1\References\CommonConfiguration\Neutral\Windows.winmd`:

| API | WP8.1 |
| --- | --- |
| `ApplicationView.TryEnterFullScreenMode` / `ExitFullScreenMode` / `IsFullScreenMode` | **absent** |
| `StatusBar.GetForCurrentView().HideAsync()` | present |
| `DisplayInformation.AutoRotationPreferences`, `DisplayOrientations` | present |
| `DisplayRequest.RequestActive/RequestRelease` | present |
| `MediaElement.IsMuted`, `BufferingProgress`, `NaturalVideoWidth/Height`, `Stretch`, `CurrentState` | present |
| `MediaElement.CurrentStateChanged`, `BufferingProgressChanged`, `MediaOpened/Failed/Ended` | present |
| `MediaElement.RoundedCorners`, `DurationChanged` | absent |
| `UIElement.Tapped` | present |
| `AutomationProperties.SetName` | present |
| `Border.CornerRadius` | not verified — see Ruling 1 below |

**Ruling 1:** because `ApplicationView.TryEnterFullScreenMode` does not exist on
WP8.1, "fullscreen" is implemented as: hide the system status bar, hide the app's
own chrome (top bar, bottom bar, page title), let the `MediaElement` fill the
page, and allow landscape rotation. Cost if wrong: on a device where the status
bar cannot be hidden the player still fills the page, so the feature degrades to
"chrome hidden" rather than breaking.

**Ruling 2:** the rounded overlay bars use `Rectangle` + `RadiusX`/`RadiusY`
(verified present) instead of `Border.CornerRadius` (presence on WP8.1 not
verified). Cost if wrong: a slightly different way to draw the same bars.

There is no orientation element in `Package.appxmanifest`, so the app's rotation
is governed by `DisplayInformation.AutoRotationPreferences`: the player allows
portrait **and** landscape while it is open, and restores portrait when it
leaves.

## Acceptance criteria

- **A.** The playback screen has a fullscreen button. Tapping it hides the status
  bar and the player's own bars, lets the video fill the page, allows landscape,
  and keeps the screen awake. Tapping it again (or leaving the screen, however
  the user leaves it) restores the status bar, portrait rotation and the screen
  timeout.
- **B.** The hardware back button leaves fullscreen first and only then leaves
  the screen.
- **C.** The playback screen offers, in addition to play/pause, direct-play
  fallback and stop: seek ±10 s / ±30 s, mute, fit/fill toggle, elapsed and total
  time, a buffering indicator, and tap-to-show/hide controls that hide by
  themselves only while the video is playing.
- **D.** No seek can move outside the video: skipping before the start lands at
  0, skipping past the end lands on the duration, and an unknown duration never
  produces a seek or a bogus total.
- **E.** Every new user-visible string exists in English and Italian, and the
  existing automated checks still pass (`LocalizationTests`, 88 tests green
  before this work).
- **F.** The screen is reachable, legible and operable with one hand: icon
  buttons have a localized accessible name, the bars are translucent so the video
  shows through, and the video area is never blocked by orphan controls.

## Out of scope

- Restyling the other screens.
- Next-episode/queue, subtitles, playback-rate and volume controls (not
  supported by the WP8.1 `MediaElement` API or requiring new server endpoints).
- Runtime verification on an emulator/device: no emulator is available here, so
  the visual result and the touch behaviour go on the manual checklist in
  `agents/TESTING.md`.
