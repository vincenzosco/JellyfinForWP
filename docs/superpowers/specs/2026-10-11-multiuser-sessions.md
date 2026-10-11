# Multiple accounts on the device (multiuser sessions)

Date: 2026-10-11
Status: agreed with the requester (four decisions taken in chat, below)

## Request (verbatim, Italian)

> implementa sessioni multiuser

"Session" is ambiguous in Jellyfin — the server also calls *sessions* the devices
connected to it. Three readings were on the table; the requester chose the first:

1. **More accounts saved on the phone** — a picker and a user switch that never
   asks for the password again. **Chosen.**
2. The same, but with per-account local data (bitrate, last server used).
3. Jellyfin's server-side session list with remote control (play/pause/stop on
   another device).

## Decisions taken in chat

1. **Entry:** the last used account is entered directly. The picker appears at
   startup only when no account is active; switching is an explicit command in
   the Home header and in Settings.
2. **Rejected token (401):** ask for **that account's** password only (server and
   user prefilled, password empty). The other accounts are untouched.
3. **Sign out:** leaves the account, does not forget it — the token is kept, so
   re-entering is one tap. Forgetting is a separate **Remove account** in the
   picker.
4. **Store shape:** a pure `AccountStore` in `Shared/` behind an
   `IAccountStorage` seam, with the thin WinRT binding in the app. This is the
   pattern the repo already uses twice (`IHttpTransport`/`HttpTransport`,
   `IPlayerShell`/`WinRtPlayerShell`) and it keeps the store testable on the
   desktop under MSTest.
5. **Bitrate stays device-wide** (the per-account option was not chosen).
6. **401 detection is on Home only**, the first screen after entry; every other
   page keeps today's page-local message. A global hook would mean touching the
   `Catch` of every page.

## What exists today

- `Services/AppSession.vb` — static singleton holding **one** identity, with flat
  keys in `ApplicationData.Current.LocalSettings`: `JellyfinBaseUrl`,
  `JellyfinAccessToken`, `JellyfinUserId`, `JellyfinServerId`,
  `JellyfinDeviceId`, `JellyfinBitrate`. Surface: `Current`, `IsLoggedIn`,
  `Load()`, `Save()`, `Clear()` (keeps the device id), `CreateClient()`.
- `MainPage` — server address + credentials, `session.Save()`, then Home.
  `App.OnLaunched` navigates to Home when `IsLoggedIn`, else to MainPage.
- `SettingsPage` — shows the server and the **raw user id**, offers Sign out
  (`Clear()`), and the bitrate picker.
- `HomePage` — header with the logo, a Search button and a Settings button.
- `AuthenticateByName` already answers with `User.Name`, which
  `AuthenticateResultDto.User` parses and the app then throws away (only
  `User.Id` is stored). The picker needs that name.

## Data model (`Shared/`)

`AccountDto` — a `<DataContract>`, like every other DTO in this repo:

| Field | Meaning |
| --- | --- |
| `Id` | GUID `"N"`, generated on creation — the stable key the active pointer uses |
| `BaseUrl` | Server address, stored as `JellyfinUrls` normalizes it (no trailing `/`) |
| `UserId` | Jellyfin user id |
| `UserName` | `User.Name` from the login reply; may be empty (see migration) |
| `ServerName` | `ServerInfoDto.ServerName`, already fetched at login; may be empty |
| `AccessToken` | The account's own token |
| `ServerId` | Jellyfin server id |

**Identity of an account** is the pair `(BaseUrl, UserId)`. Logging in again as
the same user on the same server **updates** that entry (token, name, server
name) instead of adding a second one.

**The document:** `{"Accounts":[...],"ActiveId":"..."}`, serialized with the
repo's `Json` (`DataContractJsonSerializer`) into **one** `LocalSettings` value,
`JellyfinAccounts`. One value means one atomic write and no key explosion;
`LocalSettings` allows 8 KB per value and an account is ~250 bytes, so dozens of
profiles fit. Moving to `LocalFolder` is explicitly **not** needed now (YAGNI).

**Device-wide, unchanged:** `JellyfinDeviceId` (it identifies the *phone*) and
`JellyfinBitrate`.

## The seam

`Shared/IAccountStorage.vb` — `Function Load() As String` / `Sub Save(json As String)`.
The app implements it in `Services/LocalAccountStorage.vb` over
`ApplicationData.Current.LocalSettings.Values`; the tests implement it in memory.
Only `Shared/AccountStore.vb` touches the document format, so the format never
leaks into pages.

`AccountStore` (pure, no WinRT): `Accounts`, `ActiveId`, `Active`,
`SetActive(id)`, `AddOrUpdate(account)`, `Remove(id)`, `Clear()`.

## Migration from the legacy keys

On `Load()`:

- If the `JellyfinAccounts` document is **absent** and the legacy keys are
  present → build one account from `JellyfinBaseUrl` / `JellyfinAccessToken` /
  `JellyfinUserId` / `JellyfinServerId`, make it active, then remove those four
  keys. `UserName` and `ServerName` are empty in this case, because a value in
  `LocalSettings` cannot be produced from a network call.
- If the document **exists** → ignore and remove any leftover legacy keys.
- The device id and the bitrate are never touched by the migration.

An empty `UserName` is not left to rot: the first Home load of that account fills
it once through a new `JellyfinApiClient.GetUserAsync(userId)`
(`GET /Users/{userId}` → `UserDto.Name`) and saves it. Until then the picker
shows the user id, which is what `SettingsPage` shows today anyway.

## Flows

**Startup** — `App.OnLaunched`: active account with a token → Home; no active
account but saved accounts → `AccountsPage`; no account → `MainPage`.

**Picker** (`Pages/AccountsPage.xaml` + `.xaml.vb`, registered in
`JellyfinPlayer.vbproj` with both the `<Page>` and the `<Compile DependentUpon>`
entry, per the conventions) — a list bound to `AccountItem` view models (no value
converters, like `PosterItem`): user name first line (fallback: user id), server
name and address below, the active account visibly marked. Tap a row → activate
that account → Home. Tap the active row → Home. Below the list: **Add account**
(→ `MainPage` in add mode, existing accounts untouched) and **Remove account**,
acting on the selected row (`SelectionMode="Single"`) — on a phone that is the
least surprising interaction and the easiest to label in two languages.

**Switching accounts clears the back stack** (`NavigationService.ClearBackStack`).
Without that, going back from the new user's Home would walk into the previous
user's screens — another user's content in the same stack.

**Home** — the header gets a third button (person/switch icon) that opens
`AccountsPage`. The icon is a `Path` with inline `Data`, **not** a
`<Geometry x:Key>` resource: WinRT XAML cannot create the `Geometry` base type —
the bug closed in BUG-5, pinned by `XamlResourceTests`.

**Settings** — `AccountLabel` shows the user **name** instead of the raw id;
**Sign out** now leaves the account (clears only `ActiveId`) and returns to the
picker, keeping the account and its token; a **Switch account** button opens the
picker too.

**Rejected token (401)** — navigation into the account happens, the first call
fails, and the app goes to the re-login of **that** account: `MainPage` gains a
"sign in again" mode (navigated with a parameter carrying the account: server and
user prefilled, password empty, discovery hidden). On success it updates that
entry's token and activates it. Detection lives in `HomePage`, whose
`Try/Catch` already wraps the views/resume/nextup load.

## Errors

- **Unreadable or corrupt store** → start from an empty store and show the login
  screen; never a launch that fails. The bad value is overwritten on the next
  save.
- **Failed write** → `Localized.DescribeError` on the page that caused it; the
  in-memory state stays coherent, so there is no half-written account.
- **`Remove` of the active account** → the active pointer is cleared; if other
  accounts remain, the picker is next.

## Acceptance criteria

- **A.** Accounts can be added, switched and removed; the list survives an app
  restart. An account migrated from the legacy keys shows its user id until Home
  has fetched and saved its name once, and shows the name from then on.
- **B.** Launch enters the last used account directly; the picker appears only
  when no account is active; with no account at all, the current login page.
- **C.** Switching accounts opens *that* account's Home and clears the back
  stack, so going back cannot reach the previous account's screens.
- **D.** Sign out returns to the picker **without** forgetting the account or its
  token: re-entering it asks for no password.
- **E.** A 401 on entry asks for that account's password only; the other accounts
  are untouched; success updates that account's token and activates it.
- **F.** Upgrading over the current build keeps the session: the legacy keys
  become account 0 (active) and are removed. With the document already present,
  leftover legacy keys are ignored and removed.
- **G.** A corrupt or unreadable store yields an empty store and a usable login,
  never a crash.
- **H.** Signing in twice as the same `(server, user)` leaves **one** entry with a
  refreshed token.
- **I.** Every new user-visible string exists in **both** `en-US` and `it-IT`;
  `LocalizationTests` and `XamlResourceTests` pass; `Debug|x86` and `Release|ARM`
  build; the whole MSTest suite is green (baseline before this work: **127**
  tests).
- **J.** The Jellyfin device id stays device-wide, and the docs state the
  consequence: two accounts on one phone are two Jellyfin sessions sharing one
  `DeviceId`, which is how the server's session panel shows them.

## Out of scope

- Per-account bitrate (device-wide by decision 5).
- A global 401 hook on every page (decision 6).
- The server-side session list and remote control — a different reading of
  "sessioni" (reading 3 above).
- Account avatars, and any notion of a shared "household" beyond the stored
  entries.
- Runtime verification on an emulator or device: no emulator is available here,
  so the flows go on the manual checklist in `agents/TESTING.md`.
