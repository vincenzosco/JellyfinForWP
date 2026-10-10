# Fullscreen player, more playback functions, nicer UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the playback screen a fullscreen button, the functions a video player is expected to have (skip ±10 s/30 s, mute, fit/fill, elapsed/total time, buffering, auto-hiding controls) and a cleaner, icon-based layout that keeps every action reachable with one hand.

**Architecture:** The player's *decisions* live in the WinRT-free `Shared/` layer, where the desktop MSTest suite can prove them: seek clamping (`SeekTarget`), clock text (`Ticks.FormatSpan`) and the system-chrome state machine (`IPlayerShell` + `PlayerSurfaceController`). The WinRT side is one thin adapter (`WinRtPlayerShell`) plus the page, which are verified by compilation and the manual checklist — the pattern `UdpServerDiscovery` already follows. "Fullscreen" on WP8.1 is status bar hidden + app chrome hidden + video filling the page + landscape allowed, because `ApplicationView.TryEnterFullScreenMode` does not exist there.

**Tech Stack:** VB.NET / WinRT XAML, Windows Phone 8.1, MSBuild 12 (VS2013), MSTest v1 (.NET 4.5), `.resw` resources, `Windows.UI.ViewManagement.StatusBar`, `Windows.Graphics.Display.DisplayInformation`, `Windows.System.Display.DisplayRequest`.

**Spec:** `docs/superpowers/specs/2026-10-10-fullscreen-player-and-controls.md`

## Global Constraints

- VB.NET, `Option Strict Off`, `Option Explicit On`. No new NuGet packages; no new references in the app project.
- Only APIs verified in the WP8.1 metadata winmd (`C:\Program Files (x86)\Windows Phone Kits\8.1\References\CommonConfiguration\Neutral\Windows.winmd`). Verified present: `StatusBar`/`HideAsync`, `DisplayInformation.AutoRotationPreferences`, `DisplayRequest.RequestActive/RequestRelease`, `MediaElement.IsMuted/BufferingProgress/NaturalVideoWidth/Stretch/CurrentState`, `CurrentStateChanged`, `BufferingProgressChanged`, `Tapped`, `AutomationProperties.SetName`, `Rectangle.RadiusX/RadiusY`. Verified **absent**: `ApplicationView.TryEnterFullScreenMode`/`ExitFullScreenMode`/`IsFullScreenMode`, `MediaElement.RoundedCorners`, `MediaElement.DurationChanged`.
- Rounded overlay bars use `Rectangle` + `RadiusX`/`RadiusY`; do not rely on `Border.CornerRadius`.
- Everything under `JellyfinPlayer/Shared/` is linked into the desktop test project (no WinRT): it must keep compiling without `Windows.*` types. That is why the player's shell is an interface with a WP8.1 implementation in the app project.
- User-visible strings follow the rules from the previous plan: XAML uses `x:Uid` + `Strings/<lang>/Resources.resw`; code uses `Localized.Text`/`Format`/`DescribeError`. Supported languages `en-US` (default) and `it-IT`, key sets identical.
- `LocalizationTests.No_maintained_XAML_literal_text_remains` allows only `Jellyfin`, `http://192.168.1.10:8096`, and (added in Task 4) the numeric badges `10` and `30`, which are digits, not prose.
- Commands, from the repository root in bash:

```bash
MSB="/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe"
"$MSB" JellyfinPlayer/JellyfinPlayer.Core.Tests/JellyfinPlayer.Core.Tests.vbproj -p:Configuration=Debug -p:Platform=AnyCPU -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings
"$MSB" JellyfinPlayer/JellyfinPlayer.sln -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

- App build success ends with `Your package has been successfully created.`; test success prints `N/N test(s) Passed` with no `Failed`. Baseline before this plan: **88 tests**.
- VB is case-insensitive: never let a local or parameter name collide with a type, module or function it is used with (`Path`, `File`, `Strings`, `Text`, `SeekTarget`, `Ticks`).
- Conventional Commits, one per task, no footer.
- Never commit `test.settings.local` or the Jellyfin password.

## Review Focus

1. **The user leaves the playback screen while fullscreen (or while paused).** Expected: the status bar, portrait rotation and the screen timeout are restored and the position is reported, so the next screen looks normal. Pinned by `PlayerSurfaceTests` in Task 2 (`RestoreAll_*`) and by `OnNavigatedFrom` calling `RestoreAll()` in Task 4.
2. **Skipping at the very start or the very end of a clip** (back-skip at 0:02, forward-skip on a 10 s video). Expected: the seek lands inside the video, never before 0 and never past the end. Pinned by `SeekTargetTests` in Task 1.
3. **A video whose duration is not known yet** (`NaturalDuration.HasTimeSpan = False`, normal while a transcode starts). Expected: no bogus total, no seek past the end, skip buttons still safe. Pinned by `Absolute_without_a_duration_is_zero` (Task 1) and by the page's `HasTimeSpan` guard (Task 4, manual checklist).
4. **Playback fails mid-video** (`MediaFailed`). Expected: a short localized message, the controls stay on screen, and the "play direct" fallback is offered. Pinned by the `PlaybackFailed` key being used from code + `Every_key_used_from_code_exists` (Task 4) and the manual checklist line.
5. **Tapping the video while the controls are hidden in fullscreen.** Expected: the controls come back, so nothing is ever unreachable. Pinned by the manual checklist line in Task 4 (touch input is not unit-testable here).

---

### Task 1: Seek clamping and clock text

**Files:**
- Create: `JellyfinPlayer/Shared/SeekTarget.vb`
- Modify: `JellyfinPlayer/Shared/Ticks.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/SeekTargetTests.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/TicksTests.vb`

**Interfaces:**
- Produces: `SeekTarget.Relative(position As TimeSpan, delta As TimeSpan, duration As TimeSpan) As TimeSpan` and `SeekTarget.Absolute(seconds As Double, duration As TimeSpan) As TimeSpan`.
- Produces: `Ticks.FormatSpan(value As TimeSpan) As String`, same rules as the existing `Ticks.Format` (`h:mm:ss` from one hour, else `m:ss`, negatives as `0:00`).
- Consumed by: Task 4 (`PlayerPage` skip buttons, seek slider, position/duration labels).

- [ ] **Step 1: Write the failing tests**

`SeekTargetTests.vb`:

```vb
<TestClass>
Public Class SeekTargetTests
    Private Shared ReadOnly TwoHours As TimeSpan = TimeSpan.FromHours(2)

    <TestMethod> Public Sub Relative_forward_stays_inside_the_video()
        Assert.AreEqual(TimeSpan.FromSeconds(90), SeekTarget.Relative(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30), TwoHours))
    End Sub

    <TestMethod> Public Sub Relative_back_at_the_start_stops_at_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Relative(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(-30), TwoHours))
    End Sub

    <TestMethod> Public Sub Relative_forward_at_the_end_stops_at_the_end()
        Assert.AreEqual(TwoHours, SeekTarget.Relative(TimeSpan.FromSeconds(7190), TimeSpan.FromSeconds(30), TwoHours))
    End Sub

    <TestMethod> Public Sub Relative_without_a_duration_is_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Relative(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), TimeSpan.Zero))
    End Sub

    <TestMethod> Public Sub Relative_treats_a_negative_position_as_zero()
        Assert.AreEqual(TimeSpan.FromSeconds(10), SeekTarget.Relative(TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(10), TwoHours))
    End Sub

    <TestMethod> Public Sub Absolute_clamps_to_the_duration()
        Assert.AreEqual(TwoHours, SeekTarget.Absolute(9999.0, TwoHours))
    End Sub

    <TestMethod> Public Sub Absolute_below_zero_is_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Absolute(-5.0, TwoHours))
    End Sub

    <TestMethod> Public Sub Absolute_keeps_a_valid_position()
        Assert.AreEqual(TimeSpan.FromSeconds(75.5), SeekTarget.Absolute(75.5, TwoHours))
    End Sub

    <TestMethod> Public Sub Absolute_without_a_duration_is_zero()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Absolute(30.0, TimeSpan.Zero))
    End Sub

    <TestMethod> Public Sub Absolute_ignores_not_a_number()
        Assert.AreEqual(TimeSpan.Zero, SeekTarget.Absolute(Double.NaN, TwoHours))
    End Sub
End Class
```

Add to `TicksTests.vb`:

```vb
    <TestMethod> Public Sub FormatSpan_minutes_seconds()
        Assert.AreEqual("2:05", Ticks.FormatSpan(TimeSpan.FromSeconds(125)))
    End Sub

    <TestMethod> Public Sub FormatSpan_hours_minutes_seconds()
        Assert.AreEqual("1:00:03", Ticks.FormatSpan(TimeSpan.FromSeconds(3603)))
    End Sub

    <TestMethod> Public Sub FormatSpan_negative_is_zero()
        Assert.AreEqual("0:00", Ticks.FormatSpan(TimeSpan.FromSeconds(-5)))
    End Sub
```

- [ ] **Step 2: Run the tests to verify they fail**

Build the test project and run it. Expected: `error BC30451: 'SeekTarget' non dichiarato` and `error BC30456: 'FormatSpan' non è un membro di 'JellyfinPlayer.Shared.Ticks'`.

- [ ] **Step 3: Implement them**

`SeekTarget.vb`: module `SeekTarget` in `Namespace Global.JellyfinPlayer.Shared`; `Relative` returns zero when `duration <= TimeSpan.Zero`, treats a negative `position` as zero, adds `delta`, then clamps into `[TimeSpan.Zero, duration]`. `Absolute` returns zero for `Double.IsNaN(seconds)`, `seconds <= 0` or `duration <= TimeSpan.Zero`, else `TimeSpan.FromSeconds(seconds)` clamped into `[TimeSpan.Zero, duration]`.

`Ticks.vb`: rename the private `FormatTime` to `Public Function FormatSpan(value As TimeSpan) As String` and make `Format(ticks As Long?)` call `FormatSpan(TimeSpan.FromTicks(ticks.Value))`. No behaviour change to `Format`/`FormatDuration` (their five existing tests must keep passing).

- [ ] **Step 4: Run the tests to verify they pass**

Expected: `102/102 test(s) Passed` (88 baseline + 11 seek tests + 3 clock tests).

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/Shared/SeekTarget.vb JellyfinPlayer/Shared/Ticks.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/SeekTargetTests.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/TicksTests.vb
git commit -m "feat: clamp seeks and format playback times"
```

---

### Task 2: The system-chrome state machine and its WP8.1 binding

**Files:**
- Create: `JellyfinPlayer/Shared/IPlayerShell.vb`
- Create: `JellyfinPlayer/Shared/PlayerSurfaceController.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/FakePlayerShell.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/PlayerSurfaceTests.vb`
- Create: `JellyfinPlayer/JellyfinPlayer/Services/WinRtPlayerShell.vb`

**Interfaces:**
- Produces: `IPlayerShell` with `Sub HideStatusBar()`, `ShowStatusBar()`, `AllowRotation()`, `RestorePortrait()`, `RequestKeepAwake()`, `ReleaseKeepAwake()`.
- Produces: `PlayerSurfaceController(shell As IPlayerShell)` with `ReadOnly Property IsFullScreen As Boolean`, `Sub EnterFullScreen()`, `ExitFullScreen()`, `SetPlaying(playing As Boolean)`, `RestoreAll()`.
- Produces: `Services.WinRtPlayerShell` implementing `IPlayerShell` over `Windows.UI.ViewManagement.StatusBar`, `Windows.Graphics.Display.DisplayInformation` and `Windows.System.Display.DisplayRequest`.
- Consumed by: Task 4 (the fullscreen button, keep-awake, and `OnNavigatedFrom`).

- [ ] **Step 1: Write the failing tests**

`FakePlayerShell.vb` records what the controller asks for:

```vb
Public Class FakePlayerShell
    Implements IPlayerShell

    Public ReadOnly Calls As New List(Of String)()

    Public Sub HideStatusBar() Implements IPlayerShell.HideStatusBar
        Calls.Add("HideStatusBar")
    End Sub
    Public Sub ShowStatusBar() Implements IPlayerShell.ShowStatusBar
        Calls.Add("ShowStatusBar")
    End Sub
    Public Sub AllowRotation() Implements IPlayerShell.AllowRotation
        Calls.Add("AllowRotation")
    End Sub
    Public Sub RestorePortrait() Implements IPlayerShell.RestorePortrait
        Calls.Add("RestorePortrait")
    End Sub
    Public Sub RequestKeepAwake() Implements IPlayerShell.RequestKeepAwake
        Calls.Add("RequestKeepAwake")
    End Sub
    Public Sub ReleaseKeepAwake() Implements IPlayerShell.ReleaseKeepAwake
        Calls.Add("ReleaseKeepAwake")
    End Sub

    Public Function CountOf(name As String) As Integer
        Return Calls.Where(Function(c) c = name).Count()
    End Function
End Class
```

`PlayerSurfaceTests.vb`:

```vb
<TestClass>
Public Class PlayerSurfaceTests
    ' Not called Surface: VB is case-insensitive, so a method named Surface and a
    ' local named surface are the same name to the compiler (BC30980).
    Private Function NewSurface(fake As FakePlayerShell) As PlayerSurfaceController
        Return New PlayerSurfaceController(fake)
    End Function

    <TestMethod> Public Sub Enter_fullscreen_hides_the_status_bar_and_allows_rotation()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        Assert.IsTrue(surface.IsFullScreen)
        CollectionAssert.AreEqual({"HideStatusBar", "AllowRotation"}, fake.Calls.ToArray())
    End Sub

    <TestMethod> Public Sub Entering_twice_does_not_repeat_the_calls()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.EnterFullScreen()
        Assert.AreEqual(1, fake.CountOf("HideStatusBar"))
        Assert.AreEqual(1, fake.CountOf("AllowRotation"))
    End Sub

    <TestMethod> Public Sub Exit_fullscreen_restores_the_status_bar_and_portrait()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        fake.Calls.Clear()
        surface.ExitFullScreen()
        Assert.IsFalse(surface.IsFullScreen)
        CollectionAssert.AreEqual({"ShowStatusBar", "RestorePortrait"}, fake.Calls.ToArray())
    End Sub

    <TestMethod> Public Sub Exit_without_fullscreen_does_nothing()
        Dim fake As New FakePlayerShell()
        NewSurface(fake).ExitFullScreen()
        Assert.AreEqual(0, fake.Calls.Count)
    End Sub

    <TestMethod> Public Sub Playing_keeps_the_screen_awake_and_pausing_releases_it()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.SetPlaying(True)
        Assert.AreEqual(1, fake.CountOf("RequestKeepAwake"))
        surface.SetPlaying(True)
        Assert.AreEqual(1, fake.CountOf("RequestKeepAwake"), "idempotent while already playing")
        surface.SetPlaying(False)
        Assert.AreEqual(1, fake.CountOf("ReleaseKeepAwake"))
        surface.SetPlaying(False)
        Assert.AreEqual(1, fake.CountOf("ReleaseKeepAwake"), "idempotent while already stopped")
    End Sub

    ' Review Focus 1: however the user left the screen, the next one must look normal.
    <TestMethod> Public Sub RestoreAll_undoes_a_fullscreen_session_left_open()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.SetPlaying(True)
        fake.Calls.Clear()
        surface.RestoreAll()
        CollectionAssert.AreEqual({"ShowStatusBar", "RestorePortrait", "ReleaseKeepAwake"}, fake.Calls.ToArray())
        Assert.IsFalse(surface.IsFullScreen)
    End Sub

    <TestMethod> Public Sub RestoreAll_is_idempotent()
        Dim fake As New FakePlayerShell()
        Dim surface As PlayerSurfaceController = NewSurface(fake)
        surface.EnterFullScreen()
        surface.RestoreAll()
        fake.Calls.Clear()
        surface.RestoreAll()
        Assert.AreEqual(0, fake.Calls.Count)
    End Sub

    <TestMethod> Public Sub RestoreAll_on_a_fresh_controller_does_nothing()
        Dim fake As New FakePlayerShell()
        NewSurface(fake).RestoreAll()
        Assert.AreEqual(0, fake.Calls.Count)
    End Sub
End Class
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: `error BC30451: 'IPlayerShell' non dichiarato` (and `PlayerSurfaceController`).

- [ ] **Step 3: Implement the interface and the controller**

`IPlayerShell.vb`: the interface exactly as in Interfaces, in `Namespace Global.JellyfinPlayer.Shared`.

`PlayerSurfaceController.vb`: class in the same namespace with `_shell`, `_isFullScreen`, `_awake` fields. `EnterFullScreen` calls `HideStatusBar` then `AllowRotation` only when `Not _isFullScreen`; `ExitFullScreen` calls `ShowStatusBar` then `RestorePortrait` only when `_isFullScreen`; `SetPlaying` calls `RequestKeepAwake`/`ReleaseKeepAwake` only on a change; `RestoreAll` exits fullscreen when needed, releases keep-awake when needed, and does nothing at all otherwise.

- [ ] **Step 4: Run the tests to verify they pass**

Expected: `111/111 test(s) Passed` (102 + 9 surface tests). Then build the app:
`Your package has been successfully created.`

- [ ] **Step 5: Write the WP8.1 binding**

`JellyfinPlayer/JellyfinPlayer/Services/WinRtPlayerShell.vb` — `Imports Windows.Graphics.Display`, `Windows.System.Display`, `Windows.UI.ViewManagement`, `JellyfinPlayer.Shared`; class `WinRtPlayerShell` in `Namespace Services` implementing `IPlayerShell`:

- `HideStatusBar`/`ShowStatusBar`: `Dim bar = StatusBar.GetForCurrentView()`; if `bar IsNot Nothing` call `bar.HideAsync()` / `bar.ShowAsync()` (fire and forget: the returned action is not awaited, since the interface is synchronous).
- `AllowRotation`: `DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait Or DisplayOrientations.Landscape`.
- `RestorePortrait`: `DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait`.
- `RequestKeepAwake`: create the `DisplayRequest` on first use, then `RequestActive()`. `ReleaseKeepAwake`: `RequestRelease()` when one is active, then clear the field.

- [ ] **Step 6: Build the app and commit**

Expected: `Your package has been successfully created.`

```bash
git add JellyfinPlayer/Shared/IPlayerShell.vb JellyfinPlayer/Shared/PlayerSurfaceController.vb JellyfinPlayer/JellyfinPlayer/Services/WinRtPlayerShell.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/FakePlayerShell.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/PlayerSurfaceTests.vb
git commit -m "feat: add the player system-chrome state machine"
```

---

### Task 3: The player screen, restyled

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml` (rewrite)
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Strings/en-US/Resources.resw`, `Strings/it-IT/Resources.resw`

**Interfaces:**
- Consumes: nothing new (this task only reshapes the screen and keeps the existing functions).
- Produces: the element names Task 4 wires — `RootGrid`, `MediaEl`, `OverlayRoot`, `TopBar`, `CloseButton`, `TitleText`, `CenterCluster`, `PlayPauseButton`, `PlayIcon`, `PauseIcon`, `BottomBar`, `PositionText`, `SeekSlider`, `DurationText`, `Busy`, `ErrorText`, `DirectButton`, plus the geometry resources `IconPlay`, `IconPause`, `IconChevronBack`.
- Note: the geometries the new buttons use (`IconSpeaker`, `IconFit`, `IconFill`, `IconEnterFullScreen`, `IconExitFullScreen`, `IconSkipBack`, `IconSkipForward`) are declared in Task 4 together with those buttons, so no commit carries an unused resource. The play/pause `ProgressRing` keeps its old name `Busy` in this task; Task 4 renames it `BufferingRing` when it adds `BufferingText`.

- [ ] **Step 1: Rewrite `PlayerPage.xaml`**

Structure (keep `x:Class`, the `d`/`mc` namespaces and `Background="Black"`):

```xml
<Page ... Background="Black">
    <Page.Resources>
        <!-- 24x24 viewBox geometries, all filled shapes: no icon font, so no
             missing-glyph boxes on a WP8.1 device. -->
        <Geometry x:Key="IconPlay">...</Geometry>      <!-- triangle pointing right -->
        <Geometry x:Key="IconPause">...</Geometry>     <!-- two bars -->
        <Geometry x:Key="IconChevronBack">...</Geometry>
        <Geometry x:Key="IconSpeaker">...</Geometry>   <!-- speaker, mute adds a slash -->
        <Geometry x:Key="IconFit">...</Geometry>       <!-- inward corners -->
        <Geometry x:Key="IconFill">...</Geometry>      <!-- outward corners -->
        <Geometry x:Key="IconEnterFullScreen">...</Geometry>
        <Geometry x:Key="IconExitFullScreen">...</Geometry>
        <Geometry x:Key="IconSkipBack">...</Geometry>  <!-- double chevron pointing left -->
        <Geometry x:Key="IconSkipForward">...</Geometry>
    </Page.Resources>

    <Grid x:Name="RootGrid" Background="Black">
        <MediaElement x:Name="MediaEl" Stretch="Uniform" MediaOpened="Media_Opened" MediaEnded="Media_Ended" MediaFailed="Media_Failed"/>

        <Grid x:Name="OverlayRoot">
            <!-- top: translucent bar, close button + title -->
            <Grid x:Name="TopBar" VerticalAlignment="Top" Height="52">
                <Rectangle Fill="#99000000"/>
                <Button x:Name="CloseButton" Click="Stop_Click" .../>   <!-- Path x:Key IconChevronBack, 22x22 -->
                <TextBlock x:Name="TitleText" .../>
            </Grid>

            <!-- centre: the big play/pause button, and the buffering indicator -->
            <StackPanel x:Name="CenterCluster" HorizontalAlignment="Center" VerticalAlignment="Center">
                <Button x:Name="PlayPauseButton" Width="72" Height="72" Click="PlayPause_Click" ...>
                    <Grid>
                        <Path x:Name="PlayIcon" Data="{StaticResource IconPlay}" Width="34" Height="34" Fill="White" Stretch="Uniform"/>
                        <Path x:Name="PauseIcon" Data="{StaticResource IconPause}" Width="34" Height="34" Fill="White" Stretch="Uniform" Visibility="Collapsed"/>
                    </Grid>
                </Button>
            </StackPanel>

            <!-- bottom: seek row + action row, rounded translucent bar -->
            <Grid x:Name="BottomBar" VerticalAlignment="Bottom">
                <Rectangle Fill="#99000000" RadiusX="12" RadiusY="12"/>
                <StackPanel>
                    <Grid>  <!-- PositionText | SeekSlider | DurationText -->
                        <TextBlock x:Name="PositionText" .../>
                        <Slider x:Name="SeekSlider" Minimum="0" Maximum="100" ValueChanged="Seek_Changed" .../>
                        <TextBlock x:Name="DurationText" .../>
                    </Grid>
                    <Grid>  <!-- actions: skip back | skip forward | direct | mute | fit | fullscreen -->
                        ...
                    </Grid>
                    <TextBlock x:Name="ErrorText" Foreground="#FF8A80" TextWrapping="Wrap" Visibility="Collapsed" .../>
                </StackPanel>
            </Grid>
        </Grid>
    </Grid>
</Page>
```

Layout values to use: top bar 52 px, bottom bar padding `16,8,16,12`, icon buttons 48×48 with `Margin="4,0"`, icons 22×22 (`Width`/`Height` + `Stretch="Uniform"`), centre button 72×72 with a 34×34 icon, bars `#99000000`. The visual order in the action row: skip back, skip forward, `DirectButton` (centred, pill-shaped, text kept from its resource), then mute, fit/fill, fullscreen on the right.

- [ ] **Step 2: Rewrite the code-behind's icon handling**

`PlayerPage.xaml.vb`: replace `PlayPauseButton.Content = Localized.Text("PlayAction"/"PauseAction")` with toggling `PlayIcon.Visibility`/`PauseIcon.Visibility`, and set the accessible name once in `OnNavigatedTo`:

```vb
        Windows.UI.Xaml.Automation.AutomationProperties.SetName(CloseButton, Localized.Text("ActionClose"))
```

The other icon buttons get their names in Task 4, together with their behaviour. Keep `Stop_Click`, `PlayPause_Click`, `Direct_Click`, `Seek_Changed`, `Media_Opened`, `Media_Ended`, `Media_Failed` and the progress-reporting helpers as they are.

- [ ] **Step 3: Move the resource keys the icons replaced**

Remove `PlayPauseButton.Content` and `StopButton.Content` from **both** `Strings/en-US/Resources.resw` and `Strings/it-IT/Resources.resw` (the elements no longer carry `x:Uid`, and `PlayAction`/`PauseAction` remain used from code for the accessible names). `DirectButton.Content` stays — that button keeps visible text. Add `ActionClose` (`Close the player` / `Chiudi il lettore`) to both files in this step, next to `PauseAction`: this is the task whose code first calls `Localized.Text("ActionClose")`, and `Every_key_used_from_code_exists` fails on a key that only exists in the plan.

- [ ] **Step 4: Run the localisation suite and the app build**

Expected:`LocalizationTests` green (the two files still have identical key sets; no new literal text),
`111/111 test(s) Passed`, and `Your package has been successfully created.`

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml.vb JellyfinPlayer/JellyfinPlayer/Strings
git commit -m "feat: restyle the playback screen with icon controls"
```

---

### Task 4: The new playback functions

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml`, `PlayerPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Strings/en-US/Resources.resw`, `Strings/it-IT/Resources.resw`
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/NavigationService.vb`, `JellyfinPlayer/JellyfinPlayer/App.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/LocalizationTests.vb`
- Modify: `agents/TESTING.md`, `README.md`, `README.it.md`

**Interfaces:**
- Consumes: `SeekTarget.Relative/Absolute`, `Ticks.FormatSpan` (Task 1); `PlayerSurfaceController` + `WinRtPlayerShell` (Task 2); the element names and geometries from Task 3.
- Produces: `Services.NavigationService.BackInterceptor As Func(Of Boolean)` — when set and it returns `True`, the hardware back button is consumed by the page instead of navigating.

**New resource keys** (add to both files, same order; the two removed in Task 3 stay removed):

| Key | English | Italiano |
| --- | --- | --- |
| `ActionFullScreen` | Full screen | Schermo intero |
| `ActionExitFullScreen` | Exit full screen | Esci da schermo intero |
| `ActionMute` | Mute the sound | Disattiva l'audio |
| `ActionUnmute` | Turn the sound back on | Riattiva l'audio |
| `ActionStretchFill` | Fill the screen (crop the edges) | Riempi lo schermo (taglia i bordi) |
| `ActionStretchFit` | Fit the whole video | Mostra tutto il video |
| `ActionSkipBack` | Back 10 seconds | Indietro di 10 secondi |
| `ActionSkipForward` | Forward 30 seconds | Avanti di 30 secondi |
| `ActionSeek` | Playback position | Posizione di riproduzione |
| `BufferingText.Text` | Buffering... | Caricamento... |

- [ ] **Step 1: Add the buttons to the XAML**

In the action row add `SkipBackButton` (Path `IconSkipBack` plus a `TextBlock Text="10"` badge), `SkipForwardButton` (Path `IconSkipForward` plus `TextBlock Text="30"`), `MuteButton` (Path `IconSpeaker` plus `MuteSlash`, a 22×2 white `Rectangle` rotated -45° and hidden by default), `FitButton` (Paths `IconFit`/`IconFill`) and `FullScreenButton` (Paths `IconEnterFullScreen`/`IconExitFullScreen`), each 48×48, with `Click` handlers `SkipBack_Click`, `SkipForward_Click`, `Mute_Click`, `Fit_Click`, `FullScreen_Click`. In the centre cluster add, under the play button, `BufferingRing` (a 32×32 `ProgressRing`, `IsActive=False`) and `BufferingText` (`x:Uid="BufferingText"`, collapsed). Give `SeekSlider` the `x:Uid="SeekSlider"` accessible name via code instead (`AutomationProperties`), not `x:Uid`.

- [ ] **Step 2: Allow the numeric badges in the literal check**

In `LocalizationTests.No_maintained_XAML_literal_text_remains`, extend the allowed set to `{"Jellyfin", "http://192.168.1.10:8096", "10", "30"}` with the comment `' numeric badges over the skip icons: digits, not prose`.

- [ ] **Step 3: Wire the functions**

In `PlayerPage.xaml.vb`:

- Fields: `Private ReadOnly _surface As New PlayerSurfaceController(New WinRtPlayerShell())`, `Private _uiTimer As DispatcherTimer`, `Private _tickCount As Integer`, `Private _seekGraceUntil As DateTime`, `Private _controlsRemaining As Integer`.
- **Replace the existing 10 s `_timer`** (and `StartTimer`/`Timer_Tick`): the screen now has one 1 s `_uiTimer` whose `Tick` calls `TickUi()`, and the server progress report moves inside it — every tenth tick calls `ReportProgressAsync()`, which keeps the old 10 s cadence. Start it in `StartAsync` after `MediaEl.Play()`; stop it in `OnNavigatedFrom`.
- `OnNavigatedTo`: set the accessible names of every icon button from `Localized.Text("ActionSkipBack"/"ActionSkipForward"/"ActionMute"/"ActionStretchFit"/"ActionFullScreen"/"ActionSeek"/"ActionClose")`; install `NavigationService.BackInterceptor`: `Return False` when `_surface.IsFullScreen` is False, otherwise exit fullscreen and `Return True`.
- `TickUi`: refresh `PositionText` with `Ticks.FormatSpan(MediaEl.Position)`; when `MediaEl.NaturalDuration.HasTimeSpan` and `DateTime.UtcNow >= _seekGraceUntil` and `MediaEl.CurrentState = MediaElementState.Playing`, set `_updatingSeek`, write `SeekSlider.Value` and `DurationText.Text = Ticks.FormatSpan(duration)`, `_updatingSeek = False`; decrement `_controlsRemaining` and hide `OverlayRoot` when it reaches zero — only while `MediaEl.CurrentState = MediaElementState.Playing`.
- `Root_Tapped`: toggle `OverlayRoot.Visibility`; when showing while playing, restart the 4 s countdown.
- `Media_Opened`/state changes: `_surface.SetPlaying(True)` when the state is `Playing`, `SetPlaying(False)` on pause/stop/failure/end; while buffering show `BufferingRing`/`BufferingText` and keep the controls visible (Review Focus items 3 and 5).
- `SkipBack_Click`/`SkipForward_Click`: `SeekTo(SeekTarget.Relative(MediaEl.Position, TimeSpan.FromSeconds(-10), Duration))` and `+30`, with `Private Function Duration() As TimeSpan` returning `MediaEl.NaturalDuration.TimeSpan` only when `HasTimeSpan`, else `TimeSpan.Zero`.
- `Mute_Click`: toggle `MediaEl.IsMuted`, toggle `MuteSlash.Visibility`, update the accessible name to `ActionMute`/`ActionUnmute`.
- `Fit_Click`: toggle `MediaEl.Stretch` between `Uniform` and `UniformToFill`, toggle `IconFit`/`IconFill`, update the accessible name.
- `FullScreen_Click`: `_surface.EnterFullScreen()`/`ExitFullScreen()`, toggle `IconEnterFullScreen`/`IconExitFullScreen`, and hide the overlay immediately when entering while playing.
- `Seek_Changed`: when `_updatingSeek` is False, treat it as a user seek (`_seekGraceUntil = DateTime.UtcNow.AddSeconds(2)`) and `SeekTo(SeekTarget.Absolute(SeekSlider.Value, Duration))`.
- `Media_Failed`: keep the controls visible, show the localized `PlaybackFailed` message, and offer `DirectButton` when a direct fallback exists.
- `OnNavigatedFrom`: stop the timer, clear `NavigationService.BackInterceptor`, `_surface.RestoreAll()`, then the existing report-stopped logic.

- [ ] **Step 4: Let the hardware back button leave fullscreen first**

`Services/NavigationService.vb` gains `Public BackInterceptor As Func(Of Boolean)`. `App.OnBackPressed` becomes: if `BackInterceptor IsNot Nothing AndAlso BackInterceptor()` then `e.Handled = True` and return; otherwise the existing `CanGoBack`/`GoBack` behaviour. `PlayerPage` sets the interceptor in `OnNavigatedTo` (returning `True` after exiting fullscreen, `False` otherwise) and clears it in `OnNavigatedFrom`.

- [ ] **Step 5: Run the localisation suite and the app build**

Expected: `LocalizationTests` green — identical key sets, every `x:Uid` resolved, every key used from code present in both files — `111/111 test(s) Passed`, and `Your package has been successfully created.`

- [ ] **Step 6: Document the manual checks and the features**

`agents/TESTING.md`, in the manual checklist, add:

- **Player fullscreen**: tap the fullscreen button → the status bar and both bars disappear and the video fills the screen; rotate the phone → the video follows; tap the video → the bars come back; leave fullscreen → the status bar returns; navigate away while fullscreen → the status bar and portrait are back on the next screen.
- **Player controls**: pause → the controls stay on screen; play → they hide after ~4 s and come back on tap; skip back/forward → the position moves by 10 s/30 s and never leaves the video; mute → sound off and the icon slashes; fit/fill → the video crops or letterboxes; the elapsed and total times match the video; a video still loading shows the buffering ring; a failed stream shows a short message and the "Play direct" fallback.
- **Unknown duration**: open a transcode right after it starts → the total time is not wrong and skipping does not move past the end.

`README.md` and `README.it.md`: extend the playback bullet with the new functions (fullscreen, skip ±10 s/30 s, mute, fit/fill, auto-hiding controls).

- [ ] **Step 7: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer JellyfinPlayer/JellyfinPlayer.Core.Tests agents README.md README.it.md
git commit -m "feat: add fullscreen, skipping and the rest of the player controls"
```

---

## Final verification (whole plan)

- [ ] Test project build + full MSTest run: no `Failed`. Integration tests are green against the configured server, otherwise `Inconclusive` (report that as "not run against a server", never as passing).
- [ ] `Debug|x86` **and** `Release|ARM` builds end with `Your package has been successfully created.`
- [ ] `Debug|x86` after the last source change, so the packaged app matches the tested sources.
- [ ] `git status --short` clean; `test.settings.local` untracked and ignored; no `TestResults` tracked.
- [ ] `SeekTarget`, `Ticks.FormatSpan`, `PlayerSurfaceController` and `WinRtPlayerShell` are all reachable from `PlayerPage` — no dead code.

## Verification the automated tests cannot do (no emulator here)

Recorded in `agents/TESTING.md` by Task 4: fullscreen enter/exit and what the next screen looks like, landscape following the phone, tap-to-show controls, the icon buttons' look and touch targets, mute/fit visuals, the buffering indicator, and the "play direct" fallback after a failure.
