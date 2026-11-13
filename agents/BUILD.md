# Build and test

All commands are run from the repository root in **bash** (Git Bash on Windows).

## Build the whole solution (x86 Debug)

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Success ends with `Xap assembly completed` (`Assemblaggio Xap completato` on an Italian
MSBuild) and writes a `.xap`.

> Use the `-p:` prefix. The `/p:` form is rewritten into a Windows path by Git
> Bash's MSYS path conversion and silently breaks the build.

Outputs:
- App assembly: `JellyfinPlayer/JellyfinPlayer/bin/x86/Debug/JellyfinPlayer.dll`
- Package: `JellyfinPlayer/JellyfinPlayer/bin/x86/Debug/JellyfinPlayer_Debug_x86.xap`

An ARM release build writes `JellyfinPlayer/JellyfinPlayer/bin/ARM/Release/JellyfinPlayer_Release_ARM.xap`.

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
links `Shared/*.cs`; it needs no package restore.

## Requirements (already present on the reference machine)

- Visual Studio 2013 (MSBuild 12.0.40629.0) and the Windows Phone 8.1 SDK.
- The Windows Phone 8.1 Silverlight targets (`Microsoft.WindowsPhone.CSharp.targets`).
- `Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll` in the VS2013
  `Common7/IDE/PublicAssemblies` folder (referenced via `$(MSBuildProgramFiles32)`).

## Deploying to a device/emulator

Use VS2013 (**Deploy**) or a XAP deployer (for example the Windows Phone
Application Deployment tool) with the generated `.xap`. The app requires the
`ID_CAP_NETWORKING` capability, already set in `Properties/WMAppManifest.xml`.
