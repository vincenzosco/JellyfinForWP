# Build and test

All commands are run from the repository root in **bash** (Git Bash on Windows).

## Build the whole solution (x86 Debug)

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Success ends with `Your package has been successfully created.`

> Use the `-p:` prefix. The `/p:` form is rewritten into a Windows path by Git
> Bash's MSYS path conversion and silently breaks the build.

Outputs:
- App binary: `JellyfinPlayer/JellyfinPlayer/bin/x86/Debug/JellyfinPlayer.exe`
- Package: `JellyfinPlayer/JellyfinPlayer/AppPackages/JellyfinPlayer_1.0.0.0_x86_Debug_Test/`

## Release / ARM

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Also supports `-p:Platform="Any CPU"` (note the space — that is the solution
platform name; `AnyCPU` is rejected). Add `-t:Rebuild` for a clean build.

## Unit tests (shared logic)

```bash
# build first (the solution build also builds the test project), then:
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll
```

A passing run prints `N/N test(s) Passed`. The test project targets .NET 4.5 and
links `Shared/*.vb`; it needs no package restore.

## Requirements (already present on the reference machine)

- Visual Studio 2013 (MSBuild 12.0.40629.0) and the Windows Phone 8.1 SDK.
- `MSBuild/Microsoft/WindowsXaml/v12.0` targets.
- `Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll` in the VS2013
  `Common7/IDE/PublicAssemblies` folder (referenced via `$(MSBuildProgramFiles32)`).

## Deploying to a device/emulator

Use VS2013 (**Deploy**), or the `AppPackages/.../*_Test` folder's PowerShell
`Add-AppDevPackage.ps1`, or a XAP/APPX deployer. The app requires the
`internetClientServer` capability, already set in `Package.appxmanifest`.
