# Recovering from a failed stream (fallback on the right side) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When one playback path fails, the player tries the *other* one — transcode
automatically after a failed direct stream, and offer "Play direct" after a failed
transcode — instead of showing the URL that just failed.

**Architecture:** `PlaybackService` already resolves a direct URL and, for a
transcode, a progressive MP4 URL. What it stores as the fallback is the URL it is
already about to play: `DirectFallbackUri` is set inside the *direct* branch, so a
failed direct stream offers "Play direct" with the identical URL, while a failed
transcode offers nothing. The plan now carries the *other* way — the transcode URL
for a direct plan (retried automatically, once) and the direct URL for a transcode
plan (offered through the existing button) — with the URL builders and the
retry-once rule kept in `Shared/` where MSTest can reach them.

**Tech Stack:** VB.NET, WinRT 8.1, MSTest v1 (.NET 4.5) over `Shared/*.vb`.

**Spec:** `docs/superpowers/specs/2026-10-10-fullscreen-player-and-controls.md`
and the WP8.1 client plan's "on failure show the error and a Play direct retry
button" (Task 16). BUG-6 itself is closed as *not a bug*: an MKV title plays on
the phone, so the device profile's `mp4,mkv` claim is correct and stays.

## Global Constraints

- Decisions stay in `Shared/` so MSTest covers them; the WinRT page only wires them.
- The player keeps working when no fallback exists: the current error message and
  the "Play direct" button stay exactly as they are for that case.
- No new user-visible string, so no `.resw` change.
- Tests stay green (114 before this work) and both `Debug|x86` and `Release|ARM`
  builds end with `Your package has been successfully created.`

## Review Focus

- **A fallback that loops.** Two broken URLs must not ping-pong: the retry happens
  once, and only when the fallback is a different URL (`StreamFallback.Next`), so
  the user always ends up at an error they can read.
- **A failed transcode that offers the useless button** (the bug being fixed) and
  **a failed direct stream that offers nothing**: each direction is covered by its
  own assertion in Task 1 and wired in Task 2.
- **A transcode URL that the server refuses.** The retry is last-ditch: if it fails
  too, the existing failure text and button appear, which is the same outcome as
  before the change.
- **Progress reported with the wrong play method** after switching to the transcode
  (`_plan.IsTranscode` must follow the URL actually playing).
- **A stale transcode URL after a seek.** The fallback URL is built from the
  playback-info source, not from a position, so a retry mid-file cannot restart it
  from the beginning; the position is re-applied by `Media_Opened`.

---

### Task 1: The two URL builders and the retry-once rule (Shared, tested)

**Files:**
- Modify: `JellyfinPlayer/Shared/PlaybackUrlResolver.vb`
- Create: `JellyfinPlayer/Shared/StreamFallback.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer.Core.Tests/PlaybackUrlResolverTests.vb`
- Create: `JellyfinPlayer/JellyfinPlayer.Core.Tests/StreamFallbackTests.vb`

**Interfaces:**
- Consumes: `JellyfinUrls.VideoStream`/`VideoTranscode`, `PlaybackUrlResolver.EnsureKey`.
- Produces:
  - `PlaybackUrlResolver.TranscodeUrl(baseUrl As String, token As String, itemId As String, source As MediaSourceInfoDto, maxBitrate As Integer) As String`
    — the server's `TranscodingUrl` when it gave one, else the progressive MP4 URL.
  - `PlaybackUrlResolver.DirectUrl(baseUrl As String, token As String, itemId As String, source As MediaSourceInfoDto) As String`
    — the server's `DirectStreamUrl` when it gave one, else the static stream URL
    when the source supports direct play/stream, else `Nothing`.
  - `StreamFallback.Next(current As Uri, fallback As Uri, alreadyRetried As Boolean) As Uri`
    — `Nothing` when there is nothing safe to try.

- [ ] **Step 1: Write the failing tests**

Add to `PlaybackUrlResolverTests.vb`: the server's `TranscodingUrl` wins when
present (and carries `api_key`); without it the built URL contains
`/Videos/i1/stream.mp4` and `MaxStreamingBitrate=2000000`; `DirectUrl` returns
`Nothing` for a source with no direct flags and the static stream URL for one with
`SupportsDirectStream = True`. In the new `StreamFallbackTests.vb`: `Nothing` when
`alreadyRetried`, when the fallback is `Nothing`, or when it equals the URL that
just failed; the fallback when it differs.

- [ ] **Step 2: Run the tests to verify they fail**

Build the test project and run mstest. Expected: compile error — `TranscodeUrl`,
`DirectUrl` and `StreamFallback` do not exist yet.

- [ ] **Step 3: Implement the three members**

`TranscodeUrl`: `source.TranscodingUrl` via `EnsureKey` when non-empty, else
`New JellyfinUrls(baseUrl, token).VideoTranscode(itemId, source.Id, maxBitrate)`.
`DirectUrl`: `source.DirectStreamUrl` via `EnsureKey`, else `VideoStream` when
`SupportsDirectStream OrElse SupportsDirectPlay`, else `Nothing`. Document on both
that they are only ever used as a retry after a failure, so building a URL the
server may refuse is deliberate: the alternative is the error text.

- [ ] **Step 4: Run the tests to verify they pass**

Expected: `118/118` (114 + the new ones), 0 failed.

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/Shared/PlaybackUrlResolver.vb JellyfinPlayer/Shared/StreamFallback.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/PlaybackUrlResolverTests.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/StreamFallbackTests.vb
git commit -m "feat: build the fallback a failed stream falls back to"
```

---

### Task 2: The player uses the right fallback for the plan it is playing

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/PlaybackService.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/PlayerPage.xaml.vb`
- Modify: `agents/TESTING.md`
- Modify: `README.md`, `README.it.md`

**Interfaces:**
- Consumes: `PlaybackUrlResolver.TranscodeUrl`, `PlaybackUrlResolver.DirectUrl`,
  `StreamFallback.Next` (Task 1).
- Produces: `PlaybackPlan.TranscodeFallbackUri As Uri` — the URL `PlayerPage`
  retries once when the direct stream it is playing fails.

- [ ] **Step 1: `PlaybackService.PrepareAsync` fills the opposite side**

In the direct branch, set `plan.TranscodeFallbackUri` from `TranscodeUrl`; stop
setting `DirectFallbackUri` there. In the transcode branch, set
`DirectFallbackUri` from `DirectUrl` (only when it is non-`Nothing`), which is
what makes the "Play direct" button appear after a transcode fails.

- [ ] **Step 2: `PlayerPage` retries once, automatically**

Field `_retriedTranscode As Boolean`, reset in `StartAsync`. In `Media_Failed`,
before showing the failure: `Dim next = StreamFallback.Next(MediaEl.Source, If(_plan IsNot Nothing, _plan.TranscodeFallbackUri, Nothing), _retriedTranscode)`;
when `next IsNot Nothing` set `_retriedTranscode = True`, `_plan.IsTranscode = True`
(so progress reporting follows the URL actually playing), clear `ErrorText`, keep
the controls on screen, assign `MediaEl.Source = next` and `MediaEl.Play()`, then
return. Otherwise the existing failure text and `DirectButton` logic run unchanged.

- [ ] **Step 3: Document the two directions**

`agents/TESTING.md`, player checklist: a direct stream that fails retries once
through the transcode on its own; a transcode that fails offers "Play direct", and
tapping it plays the original file. `README.md` / `README.it.md`: the playback
bullet mentions the automatic fallback.

- [ ] **Step 4: Full verification**

`mstest` run: 118/118, 0 failed. `Debug|x86` and `Release|ARM` builds:
`Your package has been successfully created.`

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer JellyfinPlayer/JellyfinPlayer.Core.Tests agents README.md README.it.md
git commit -m "fix: fall back to the other playback path when a stream fails"
```

---

## Verification the automated tests cannot do (no emulator here)

That a real failure produces the retry (and the retry plays) can only be seen on
the phone: the `MediaFailed` event cannot be raised from a desktop test. The
retry-once rule and both URLs are unit-tested, and the wiring is verified by the
build plus the manual checklist above.
