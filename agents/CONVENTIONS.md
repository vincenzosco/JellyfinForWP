# Conventions

## Language and options

- **VB.NET**, matching the project options: `Option Explicit On`, `Option Infer On`,
  `Option Strict Off`. Write explicit types anyway (`Dim x As List(Of Foo) = ...`).
- Target is **VB 12** (VS2013). No VB14 features: no readonly auto-properties
  (use a backing field + `Get`), no string interpolation, no `NameOf`.

## Namespaces

- `Shared/` files declare `Namespace Global.JellyfinPlayer.Shared`. The `Global.`
  prefix is required: VB otherwise prepends each project's `RootNamespace`, which
  would give the app and the test project two different namespaces.
- App code declares `Namespace Services` (which becomes `JellyfinPlayer.Services`).
  Pages live in the app root namespace (no `Namespace` block) so their `x:Class`
  is exactly `JellyfinPlayer.<PageName>`.
- **VB `Imports` are absolute**, not relative to the root namespace. Import
  `JellyfinPlayer.Services`, not `Services`.
- Watch for VB keywords used as identifiers: `Default`, `Resume`. Prefer renaming
  (`DefaultProfile`, `ResumeItems`) over bracketing.

## Dependencies

- **No NuGet packages, no restore.** Only `System.*` and `Windows.*` framework
  APIs. JSON goes through `System.Runtime.Serialization.Json`.
- `DataContractJsonSerializer` cannot map a JSON object onto
  `Dictionary(Of String, String)`. Model fixed-key objects as a class with
  `[DataMember]` properties instead (see `ImageTagsDto`).

## Pages

- One page per file pair `Pages/<Name>.xaml` + `Pages/<Name>.xaml.vb`; add both the
  `<Page>` and the `<Compile ... DependentUpon>` entries in `JellyfinPlayer.vbproj`.
- Navigate through `NavigationService.Navigate(GetType(Page), parameter)`; pass a
  `BaseItemDto` (or `Nothing`) as the parameter.
- Data binding uses plain `{Binding}` on `PosterItem` properties. No converters.
- Show errors in a page-local `TextBlock`; never let an exception escape an
  `Async Sub` handler (wrap in `Try/Catch`).

## Errors

- All API failures surface as `JellyfinException` with a `StatusCode`
  (0 for network/timeout, HTTP status otherwise). Show `.Message`; special-case 401
  in the login page.
