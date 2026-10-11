# Playback screen loads again (the Play button opens the player) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tapping **Play** / **Resume** on the detail screen opens the playback
screen again, after the Windows Phone 8.1 XAML loader refused to load
`PlayerPage.xaml`.

**Architecture:** The player screen declares its icons as resources. It declares
them with `Windows.UI.Xaml.Media.Geometry` itself, and WinRT XAML cannot create
that type — the MS docs say a `Geometry` value *"needs a nonabstract derived
type of Geometry, as an object element for XAML"*, and a bare `Geometry` is not
activatable. The page therefore fails to load, `Frame.Navigate` returns `False`
and the button that opened it looks dead (nothing is shown, no error, no crash).
The fix drops the resource dictionary and writes each icon's path data directly
into its own `Path.Data`, which is the documented, supported form. A regression
test scans the real page XAML so this class of mistake cannot come back.

**Tech Stack:** VB.NET, WinRT XAML (Windows Phone 8.1 / WinRT 8.1), MSTest v1 on
.NET 4.5, MSBuild from Visual Studio 2013.

**Spec:** `docs/superpowers/specs/2026-10-10-fullscreen-player-and-controls.md`
(the icons and the control layout are the ones this spec asked for; only the
resource form changes).

## Global Constraints

- Windows Phone 8.1 (WinRT 8.1) only: no `AdaptiveMediaSource`, no
  `ApplicationView.TryEnterFullScreenMode`, no `StreamGeometry`.
- Icons stay filled 24×24 path shapes drawn from path data — no icon font, so no
  missing-glyph box can appear on a device.
- Every user-visible string stays in both `Strings/en-US/Resources.resw` and
  `Strings/it-IT/Resources.resw`; no new literal text in XAML.
- The app build must end with `Your package has been successfully created.`
  for `Debug|x86` and for `Release|ARM`.
- The test suite must stay green (112 tests before this work: 111 baseline + the
  BUG-1 regression test) with 0 failures.

## Review Focus

- **A page whose markup cannot load.** The user's report is exactly this: a tap
  on a button that calls `Frame.Navigate` produces *nothing* — no screen, no
  error, no crash — because `Navigate` returns `False` and the app has no
  `NavigationFailed` handler. `PlayerPage.xaml` is where the class of mistake
  lives; the test `No_XAML_element_uses_a_type_that_XAML_cannot_create` in Task 1
  pins it for every page.
- **A `{StaticResource}` with no matching `x:Key`.** Same symptom, same silent
  cause. Test `Every_StaticResource_has_an_xKey_in_the_same_file` in Task 1.
- **An icon whose data is lost or mistyped while moving it** (a shape that stops
  matching the button it labels, or a silently dropped `Path`). The 10 data
  strings move verbatim in Task 1; the diff is the check.
- **Path fill rule.** `IconExitFullScreen` relies on the default fill rule
  (even-odd) to punch the inner rectangle out of the outer one. The moved data
  must not gain an explicit `F0`/`F1` prefix that changes it.
- **A resource that was only reachable through `Page.Resources`.** All ten
  resources are referenced from one `Path` each and from nowhere else; Task 1
  verifies that by grepping before deleting the block, so no icon goes missing.

---

### Task 1: The player page loads on a device again

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/XamlResourceTests.vb`
- Modify: `agents/TESTING.md` (the manual checklist gains the check that was
  missing: opening the player)

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new. `PlayerPage` keeps every element name the code-behind
  uses (`RootGrid`, `MediaEl`, `OverlayRoot`, `CloseButton`, `TitleText`,
  `PlayPauseButton`, `PlayIcon`, `PauseIcon`, `SeekSlider`, `DurationText`,
  `PositionText`, `BufferingRing`, `BufferingText`, `DirectButton`, `MuteButton`,
  `MuteSlash`, `FitButton`, `IconFit`, `IconFill`, `FullScreenButton`,
  `IconEnterFullScreen`, `IconExitFullScreen`, `SkipBackButton`,
  `SkipForwardButton`, `ErrorText`) with the same `Click`/event handlers, so
  `PlayerPage.g.vb` regenerates unchanged apart from line numbers.

- [ ] **Step 1: Write the failing test**

`JellyfinPlayer.Core.Tests/XamlResourceTests.vb` — the real page XAML files, the
way `LocalizationTests` reads them (`Xaml\*.xaml` next to the test assembly):

```vb
Imports System
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions

' Guards the XAML against the two mistakes that make a whole page fail to load on
' a device. A page that cannot load is the worst failure this app has: the button
' that opened it looks dead, because Frame.Navigate returns False and nothing is
' shown.
'
' 1. An object element whose type XAML cannot create (the WinRT base types:
'    Geometry, Brush, Transform...). A Geometry value needs a nonabstract derived
'    type - PathGeometry, for one - so <Geometry x:Key="..."> cannot be created.
' 2. A {StaticResource} with no matching x:Key in the same file.
<TestClass>
Public Class XamlResourceTests
    Private Shared ReadOnly BaseDir As String = AppDomain.CurrentDomain.BaseDirectory

    ' XAML base types that markup cannot instantiate: a resource or object element
    ' has to name a derived, activatable type instead.
    Private Shared ReadOnly NotCreatableTypes As String() = {
        "Geometry",
        "Brush",
        "Transform",
        "Shape",
        "PathSegment",
        "ImageSource",
        "Timeline",
        "EasingFunctionBase"
    }

    Private Shared Function XamlFiles() As String()
        Return Directory.GetFiles(Path.Combine(BaseDir, "Xaml"), "*.xaml")
    End Function

    <TestMethod>
    Public Sub No_XAML_element_uses_a_type_that_XAML_cannot_create()
        Dim files = XamlFiles()
        Assert.IsTrue(files.Length >= 6, "expected the page XAML files, found " & files.Length.ToString())
        For Each file In files
            Dim text As String = File.ReadAllText(file)
            For Each typeName As String In NotCreatableTypes
                ' "<Geometry ", "<Geometry>" or "<Geometry/>", but not a property
                ' element such as <Geometry.Transform>.
                Dim m = Regex.Match(text, "<\s*" & typeName & "(?![A-Za-z.])")
                Assert.IsFalse(m.Success,
                    Path.GetFileName(file) & " creates <" & typeName & "> as an object element; use a derived type (for a path, PathGeometry or the Path's own Data attribute)")
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub Every_StaticResource_has_an_xKey_in_the_same_file()
        For Each file In XamlFiles()
            Dim text As String = File.ReadAllText(file)
            Dim defined = Regex.Matches(text, "x:Key=""([^""]+)""").Cast(Of Match)().
                                 Select(Function(m) m.Groups(1).Value).ToList()
            For Each m In Regex.Matches(text, "StaticResource\s+([^}\s]+)").Cast(Of Match)()
                Dim key As String = m.Groups(1).Value
                Assert.IsTrue(defined.Contains(key),
                    Path.GetFileName(file) & " uses {StaticResource " & key & "} but defines no x:Key for it")
            Next
        Next
    End Sub
End Class
```

- [ ] **Step 2: Run the test to verify it fails**

Build the test project, then:

```bash
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings
```

Expected: `No_XAML_element_uses_a_type_that_XAML_cannot_create` **fails** with
`PlayerPage.xaml creates <Geometry> as an object element; ...`; the
`StaticResource` test passes; nothing else changes.

- [ ] **Step 3: Inline the icon path data in `PlayerPage.xaml`**

Verify first that nothing else uses the resources, so none is dropped:

```bash
grep -rn "StaticResource" JellyfinPlayer/JellyfinPlayer --include=*.xaml --exclude-dir=obj
```

Expected: ten hits, all in `Pages/PlayerPage.xaml`, each on the `Data` of one
`Path`. Then:

- Delete the whole `<Page.Resources>` block (lines 9-25 as of this writing) with
  its ten `<Geometry x:Key="...">` elements. Every key is used exactly once, so
  there is nothing left to share.
- Put each icon's path data into the `Data` of the `Path` that used it, verbatim
  (same numbers, same commands, no `F0`/`F1` prefix — the even-odd default is
  what `IconExitFullScreen` relies on):

| Element | `Path` name | `Data` |
| --- | --- | --- |
| Close | (unnamed) | `M 16,4 L 8,12 L 16,20 L 18,18 L 12,12 L 18,6 Z` |
| Play | `PlayIcon` | `M 6,4 L 20,12 L 6,20 Z` |
| Pause | `PauseIcon` | `M 7,4 H 10.6 V 20 H 7 Z M 13.4,4 H 17 V 20 H 13.4 Z` |
| Skip back | (unnamed) | `M 10.5,5 L 4,12 L 10.5,19 L 12.5,17 L 8.2,12 L 12.5,7 Z M 19.5,5 L 13,12 L 19.5,19 L 21.5,17 L 17.2,12 L 21.5,7 Z` |
| Skip forward | (unnamed) | `M 13.5,5 L 20,12 L 13.5,19 L 11.5,17 L 15.8,12 L 11.5,7 Z M 4.5,5 L 11,12 L 4.5,19 L 2.5,17 L 6.8,12 L 2.5,7 Z` |
| Mute | (unnamed) | `M 4.5,9 H 8.5 L 13,4.5 V 19.5 L 8.5,15 H 4.5 Z M 15.4,9.3 L 17,8 L 17,16 L 15.4,14.7 Z M 18.6,7.2 L 20.4,5.8 L 20.4,18.2 L 18.6,16.8 Z` |
| Fit | `IconFit` | `M 3,5.5 H 21 V 8.5 H 3 Z M 3,15.5 H 21 V 18.5 H 3 Z` |
| Fill | `IconFill` | `M 5.5,3 H 8.5 V 21 H 5.5 Z M 15.5,3 H 18.5 V 21 H 15.5 Z` |
| Enter fullscreen | `IconEnterFullScreen` | `M 4,4 H 10 V 6.4 H 6.4 V 10 H 4 Z M 14,4 H 20 V 10 H 17.6 V 6.4 H 14 Z M 4,14 H 6.4 V 17.6 H 10 V 20 H 4 Z M 17.6,14 H 20 V 20 H 14 V 17.6 H 17.6 Z` |
| Exit fullscreen | `IconExitFullScreen` | `M 4,4 H 20 V 20 H 4 Z M 7.5,7.5 V 16.5 H 16.5 V 7.5 Z` |

- Keep the reason the icons are paths, as a comment where the resources used to
  be: no icon font, so no missing-glyph box can appear on a device. Do not write
  the rejected element name in that comment — the test reads comments too.

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: `No_XAML_element_uses_a_type_that_XAML_cannot_create`
and `Every_StaticResource_has_an_xKey_in_the_same_file` pass, and the whole run
stays at `112/112` passed, 0 failed.

- [ ] **Step 5: Build the app for the emulator and for the phone**

```bash
MSBuild.exe JellyfinPlayer/JellyfinPlayer.sln /p:Configuration=Debug /p:Platform=x86 ...
MSBuild.exe JellyfinPlayer/JellyfinPlayer.sln /p:Configuration=Release /p:Platform=ARM ...
```

Expected for both: `Your package has been successfully created.`, exit 0.

- [ ] **Step 6: Record the manual check that was missing**

`agents/TESTING.md`, in the manual checklist: the existing
`Detail: Play/Resume opens the player` line is the check that would have caught
this; add, right after it, that a page which cannot load shows **nothing at
all** (no screen, no error), so "the button did nothing" must always be treated
as a page-load failure first.

- [ ] **Step 7: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/XamlResourceTests.vb \
        agents/TESTING.md
git commit -m "fix: load the playback screen again on a device"
```

---

## Verification the automated tests cannot do (no emulator here)

Deploying the fixed package and tapping **Play** on a real title is the only way
to see the playback screen appear. The regression test proves the markup is
loadable *shape*, not that the video plays: the MKV claim in
`DeviceProfileBuilder.DefaultProfile` (`.Container = "mp4,mkv"`) is **not**
verified against the Windows Phone 8.1 media stack here and is left alone, since
removing it would force a transcode for files the phone may play directly. It is
recorded as `MANUAL_REVIEW` in `.bug-hunter/fix-report.json`.
