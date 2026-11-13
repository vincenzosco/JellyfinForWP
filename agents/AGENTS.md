# JellyfinPlayer — agent entry point

Windows Phone Silverlight 8.0 Jellyfin client, written in **C# 5**, built with
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
   (no restore). Use `System.Net.HttpWebRequest`, `System.Runtime.Serialization.Json`,
   `System.Net.Sockets` and the `Microsoft.Phone` framework assemblies only.
2. Shared logic is linked, not referenced — see `ARCHITECTURE.md`. Keep it free of
   Windows Phone-only types so the desktop tests keep compiling.
3. `Shared/*.cs` declares `namespace JellyfinPlayer.Shared`; both projects use the
   same root namespace, so the linked files keep one identity.
4. **User-visible strings are never literals in code.** Labels and messages come from
   `Resources.resx` / `Resources.it-IT.resx` through `Localized.Text` / `Localized.Format`,
   and failures through `Localized.DescribeError(ex)`; Silverlight has no `x:Uid` lookup.
   Supported languages are `en-US` (default) and `it-IT`, declared in
   `Package.appxmanifest`; keep both files in sync — `LocalizationTests` fails otherwise.
   Brand names and example URLs stay literal.
5. **Both manifests carry the identity.** `Package.appxmanifest` and
   `Properties/WMAppManifest.xml` must agree on product id and version;
   `SilverlightProjectTests` fails otherwise.
5. **The agent commits and pushes — never with AI attribution.** Once a change is
   verified, run `git add`, `git commit` and `git push` yourself; do not hand the
   commands over. The message must carry no `Co-Authored-By:` agent trailer and no
   "Generated with …" marker: write what the person who asked for the change would
   have written. (Requested 2026-10-11: trailers that reached the published history
   had to be stripped with a force-push.)
