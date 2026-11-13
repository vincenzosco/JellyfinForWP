# Conventions

## Language and options

- **C# 5** (`<LangVersion>5</LangVersion>`), because VS2013 ships the C# 5 compiler.
  No null-conditional operator (`?.`), `nameof`, string interpolation,
  expression-bodied members or auto-property initializers.
- Use explicit types where the intent is not obvious; `var` is fine for a local
  whose type is spelled out on the right.

## Namespaces

- `Shared/` files declare `namespace JellyfinPlayer.Shared`.
- App services declare `namespace JellyfinPlayer.Services`; pages and `App` live in
  `JellyfinPlayer` so their `x:Class` is exactly `JellyfinPlayer.<PageName>`.
- Inside a page, `NavigationService` is the framework property; the app's helper is
  `Services.NavigationService` (`using JellyfinPlayer.Services;` alone does not win
  over the inherited member).

## Dependencies

- **No NuGet packages, no restore.** Only `System.*` and `Microsoft.Phone`
  framework assemblies. JSON goes through `System.Runtime.Serialization.Json`.
- `DataContractJsonSerializer` cannot map a JSON object onto
  `Dictionary<string, string>`. Model fixed-key objects as a class with
  `[DataMember]` properties instead (see `ImageTagsDto`).

## Pages

- One page per file pair `Pages/<Name>.xaml` + `Pages/<Name>.xaml.cs`, with a
  `phone:PhoneApplicationPage` root; add the `<Page>` entry in `JellyfinPlayer.csproj`
  (the code-behind is picked up by the `Pages\*.xaml.cs` wildcard).
- Navigate through `Services.NavigationService.Navigate(typeof(Page), parameter)`; pass a
  `BaseItemDto` (or `null`) as the parameter.
- Data binding uses plain `{Binding}` on `PosterItem` properties. No converters.
- Show errors in a page-local `TextBlock`; never let an exception escape an
  `async void` handler (wrap in `try`/`catch`).

## Errors

- All API failures surface as `JellyfinException` with a `StatusCode`
  (0 for network/timeout, HTTP status otherwise). Show `.Message`; special-case 401
  in the login page.
