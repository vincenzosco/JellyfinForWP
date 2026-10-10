# JellyfinPlayer — agent entry point

Windows Phone 8.1 (WinRT XAML) Jellyfin client, written in **VB.NET**, built with
**VS2013 / MSBuild 12**. Read these before changing anything:

- [`BUILD.md`](BUILD.md) — exact build and test commands.
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — projects, the `Shared/` layer, data flow.
- [`CONVENTIONS.md`](CONVENTIONS.md) — style, namespaces, the no-NuGet rule.
- [`JELLYFIN_API.md`](JELLYFIN_API.md) — endpoints, auth, ticks, device profile.
- [`MAINTENANCE.md`](MAINTENANCE.md) — how to add a page/endpoint, regenerate assets.
- [`README.md`](README.md) — what the app does.
- [`TESTING.md`](TESTING.md) — unit vs. integration tests, and the manual checklist.

Quick reference:

```bash
# Build (bash; note the -p: form — /p: is mangled by Git Bash path conversion)
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# Test the shared logic (and the integration tests, when configured)
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings

# Only the tests that talk to a real server:
... /category:Integration
```

Rules of the road:

1. **Never add a NuGet package.** The solution must build with a bare `msbuild`
   (no restore). Use `System.Net.Http`, `System.Runtime.Serialization.Json`, and
   `Windows.*` only.
2. Shared logic is linked, not referenced — see `ARCHITECTURE.md`. Keep it free of
   WinRT-only types so the desktop tests keep compiling.
3. `Shared/*.vb` uses `Namespace Global.JellyfinPlayer.Shared` on purpose; VB would
   otherwise prepend each project's `RootNamespace`.
4. **User-visible strings are never literals in code.** XAML uses `x:Uid` with an entry
   in `Strings/<lang>/Resources.resw`; code-behind uses `Localized.Text` / `Localized.Format`
   and `Localized.DescribeError(ex)` for failures. Supported languages are `en-US`
   (default) and `it-IT`, declared in `Package.appxmanifest`; keep both files in sync —
   `LocalizationTests` fails otherwise. Brand names and example URLs stay literal.
