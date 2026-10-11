# Multiple Accounts (multiuser sessions) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let one phone keep several Jellyfin accounts, enter the last used one
directly, and switch between them without typing a password again.

**Architecture:** A pure `AccountStore` in `Shared/` holds the accounts and the
active one, persisted as a single JSON document through an `IAccountStorage`
seam; the app binds that seam to `LocalSettings` and `AppSession` becomes a
façade over the active account, so no page learns there is more than one. A new
`AccountsPage` is the picker; `MainPage` gains a "sign in again" mode used when a
saved token is rejected.

**Tech Stack:** VB.NET 12 / VS2013 (no NuGet, no VB14 syntax), WinRT XAML for
Windows Phone 8.1, `System.Runtime.Serialization.Json` for JSON, MSTest v1 for
the desktop tests over `Shared/`.

**Spec:** `docs/superpowers/specs/2026-10-11-multiuser-sessions.md`

## Global Constraints

- **No NuGet, no restore.** `System.*` and `Windows.*` framework APIs only.
- **`Shared/` may not use WinRT types** — it is linked into the .NET 4.5 test
  project. Declare `Namespace Global.JellyfinPlayer.Shared` in every file there.
- **VB 12 only:** no string interpolation, no `NameOf`, no readonly
  auto-properties.
- **Every user-visible string lives in both** `Strings/en-US/Resources.resw` and
  `Strings/it-IT/Resources.resw`; XAML uses `x:Uid`, code uses
  `Localized.Text` / `Localized.Format`. Only the brand name and the example URL
  may stay literal. `LocalizationTests` fails otherwise.
- **XAML cannot create the `Geometry` type**: icon paths go in a `Path`'s
  `Data`, never in a `<Geometry x:Key>` resource (BUG-5).
- **All JSON goes through `Shared/Json.vb`** (`DataContractJsonSerializer`);
  a fixed-key JSON object is a class with `[DataMember]` properties, never a
  `Dictionary`.
- **Build:** `Debug|x86` must end with `Your package has been successfully created.`
- **Tests:** the whole suite must be green. Baseline before this work: **127**
  tests (120 unit + 7 integration; the 7 need the server configured in
  `test.settings.local`).
- **WP8.1 has no UI automation**: page code carries no unit tests. Its
  verification is the build, `LocalizationTests`, `XamlResourceTests`, and the
  manual checklist in `agents/TESTING.md`.
- **Commits carry no AI attribution** (`agents/AGENTS.md` rule 5).

### Commands used by every task

```bash
# Build (bash; the -p: form is required - /p: is mangled by Git Bash)
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal

# Test (build first; the solution build also builds the test project)
"/c/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/mstest.exe" \
  /testcontainer:JellyfinPlayer/JellyfinPlayer.Core.Tests/bin/Debug/JellyfinPlayer.Core.Tests.dll \
  /testsettings:JellyfinPlayer/JellyfinPlayer.Core.Tests/run.testsettings
```

A passing run prints `N/N test(s) Passed` and exits 0.

---

### Task 1: The account model, the storage seam and the store

**Files:**
- Create: `JellyfinPlayer/Shared/AccountDto.vb`
- Create: `JellyfinPlayer/Shared/IAccountStorage.vb`
- Create: `JellyfinPlayer/Shared/AccountStore.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/AccountStoreTests.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/FakeAccountStorage.vb`

**Interfaces:**
- Consumes: `Json.Serialize` / `Json.Deserialize(Of T)` (`Shared/Json.vb`).
- Produces: `AccountDto` (`Id`, `BaseUrl`, `UserId`, `UserName`, `ServerName`,
  `AccessToken`, `ServerId`), `IAccountStorage.Load() As String` /
  `Save(json As String)`, `AccountStore(storage As IAccountStorage)` with
  `Accounts`, `ActiveId`, `Active`, `AddOrUpdate(baseUrl, userId, userName,
  token, serverId, serverName) As AccountDto`, `SetActive(id)`, `ClearActive()`,
  `Remove(id)`, `SetUserName(id, userName)`, `Find(baseUrl, userId)`, `ById(id)`,
  `Load()`, `Save()`, `Shared Normalize(baseUrl) As String`.

Both `.vbproj` files already include `..\Shared\*.vb` by wildcard and
`*.vb` for tests, so no project file changes are needed in this task.

- [ ] **Step 1: Write the failing tests**

Create `JellyfinPlayer/JellyfinPlayer.Core.Tests/FakeAccountStorage.vb`:

```vb
' An in-memory IAccountStorage: LocalSettings cannot be reached from a desktop test.
Public Class FakeAccountStorage
    Implements IAccountStorage

    Public ReadOnly Property Saved As String
        Get
            Return _saved
        End Get
    End Property

    Public Property ThrowOnLoad As Boolean
    Public Property ThrowOnSave As Boolean

    Public Sub New(Optional initial As String = Nothing)
        _saved = initial
    End Sub

    Public Function Load() As String Implements IAccountStorage.Load
        If ThrowOnLoad Then Throw New System.Exception("storage unavailable")
        Return _saved
    End Function

    Public Sub Save(json As String) Implements IAccountStorage.Save
        If ThrowOnSave Then Throw New System.Exception("storage full")
        _saved = json
    End Sub

    Private _saved As String
End Class
```

Create `JellyfinPlayer/JellyfinPlayer.Core.Tests/AccountStoreTests.vb`:

```vb
<TestClass>
Public Class AccountStoreTests
    ' Named NewStore, not Store: VB is case-insensitive and a local `store` would
    ' then collide with the helper (BC30980).
    Private Function NewStore(Optional storage As FakeAccountStorage = Nothing) As AccountStore
        Return New AccountStore(If(storage, New FakeAccountStorage()))
    End Function

    <TestMethod>
    Public Sub Adds_an_account_and_makes_it_active()
        Dim store = NewStore()
        Dim a = store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS")
        Assert.AreEqual(1, store.Accounts.Count)
        Assert.AreEqual(a.Id, store.ActiveId)
        Assert.AreEqual("bob", store.Active.UserName)
    End Sub

    <TestMethod>
    Public Sub Normalizes_the_server_address()
        Dim store = NewStore()
        store.AddOrUpdate("http://host:8096/", "u1", "bob", "T", "s1", "NAS")
        Assert.AreEqual("http://host:8096", store.Accounts(0).BaseUrl)
    End Sub

    ' The same user on the same server is one account, refreshed - not a duplicate.
    <TestMethod>
    Public Sub Signing_in_again_refreshes_instead_of_duplicating()
        Dim store = NewStore()
        Dim first = store.AddOrUpdate("http://host:8096", "u1", "bob", "OLD", "s1", "NAS")
        Dim second = store.AddOrUpdate("http://host:8096/", "u1", "bobby", "NEW", "s1", "NAS")
        Assert.AreEqual(1, store.Accounts.Count)
        Assert.AreEqual(first.Id, second.Id)
        Assert.AreEqual("NEW", store.Accounts(0).AccessToken)
        Assert.AreEqual("bobby", store.Accounts(0).UserName)
    End Sub

    <TestMethod>
    Public Sub A_second_user_on_the_same_server_is_a_second_account()
        Dim store = NewStore()
        store.AddOrUpdate("http://host:8096", "u1", "bob", "T1", "s1", "NAS")
        store.AddOrUpdate("http://host:8096", "u2", "eve", "T2", "s1", "NAS")
        Assert.AreEqual(2, store.Accounts.Count)
    End Sub

    <TestMethod>
    Public Sub ClearActive_leaves_the_account_and_its_token()
        Dim store = NewStore()
        store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS")
        store.ClearActive()
        Assert.IsNull(store.Active)
        Assert.AreEqual(1, store.Accounts.Count)
        Assert.AreEqual("T", store.Accounts(0).AccessToken)
    End Sub

    <TestMethod>
    Public Sub Removing_the_active_account_clears_the_pointer()
        Dim store = NewStore()
        Dim a = store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS")
        store.Remove(a.Id)
        Assert.AreEqual(0, store.Accounts.Count)
        Assert.IsNull(store.ActiveId)
    End Sub

    <TestMethod>
    Public Sub SetUserName_fills_a_name_without_touching_the_token()
        Dim store = NewStore()
        Dim a = store.AddOrUpdate("http://host:8096", "u1", Nothing, "T", "s1", Nothing)
        store.SetUserName(a.Id, "bob")
        Assert.AreEqual("bob", store.Accounts(0).UserName)
        Assert.AreEqual("T", store.Accounts(0).AccessToken)
    End Sub

    <TestMethod>
    Public Sub Round_trips_through_the_storage_document()
        Dim storage = New FakeAccountStorage()
        Dim first = NewStore(storage)
        Dim a = first.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS")
        first.AddOrUpdate("http://other:8096", "u2", "eve", "T2", "s2", "Other")
        first.SetActive(a.Id)

        Dim second = NewStore(storage)
        Assert.AreEqual(2, second.Accounts.Count)
        Assert.AreEqual(a.Id, second.ActiveId)
        Assert.AreEqual("bob", second.Active.UserName)
        Assert.AreEqual("Other", second.ById(second.Accounts(1).Id).ServerName)
    End Sub

    ' A bad document must not stop the app from starting.
    <TestMethod>
    Public Sub A_corrupt_document_reads_as_an_empty_store()
        Dim storage = New FakeAccountStorage("{ this is not json")
        Dim store = NewStore(storage)
        Assert.AreEqual(0, store.Accounts.Count)
        Assert.IsNull(store.ActiveId)
    End Sub

    <TestMethod>
    Public Sub An_unreadable_storage_reads_as_an_empty_store()
        Dim storage = New FakeAccountStorage()
        storage.ThrowOnLoad = True
        Dim store = NewStore(storage)
        Assert.AreEqual(0, store.Accounts.Count)
    End Sub

    <TestMethod>
    Public Sub An_active_pointer_to_a_missing_account_means_no_active_account()
        Dim storage = New FakeAccountStorage("{""Accounts"":[],""ActiveId"":""gone""}")
        Dim store = NewStore(storage)
        Assert.IsNull(store.ActiveId)
    End Sub
End Class
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Debug -p:Platform=x86 -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Expected: FAIL — `Type 'AccountStore' is not defined` / `Type 'IAccountStorage' is not defined`.

- [ ] **Step 3: Write the model and the seam**

`JellyfinPlayer/Shared/AccountDto.vb`:

```vb
Imports System.Collections.Generic
Imports System.Runtime.Serialization

Namespace Global.JellyfinPlayer.Shared
    ' One saved login. Json cannot map a JSON object onto a Dictionary, so the
    ' document is a class with [DataMember] properties (see ImageTagsDto).
    <DataContract>
    Public Class AccountDto
        <DataMember> Public Property Id As String
        <DataMember> Public Property BaseUrl As String
        <DataMember> Public Property UserId As String
        <DataMember> Public Property UserName As String
        <DataMember> Public Property ServerName As String
        <DataMember> Public Property AccessToken As String
        <DataMember> Public Property ServerId As String
    End Class

    ' The whole store as one document, so a save is a single atomic write.
    <DataContract>
    Public Class AccountDocumentDto
        <DataMember> Public Property Accounts As List(Of AccountDto)
        <DataMember> Public Property ActiveId As String
    End Class
End Namespace
```

`JellyfinPlayer/Shared/IAccountStorage.vb`:

```vb
Namespace Global.JellyfinPlayer.Shared
    ' Keeps AccountStore free of WinRT: the app binds this to LocalSettings, the
    ' tests to memory. Same pattern as IHttpTransport.
    Public Interface IAccountStorage
        Function Load() As String
        Sub Save(json As String)
    End Interface
End Namespace
```

- [ ] **Step 4: Write the store**

`JellyfinPlayer/Shared/AccountStore.vb`:

```vb
Imports System
Imports System.Collections.Generic

Namespace Global.JellyfinPlayer.Shared
    ' The saved logins and which one is in use. Pure: no WinRT, no network.
    Public Class AccountStore
        Private ReadOnly _storage As IAccountStorage
        Private ReadOnly _accounts As New List(Of AccountDto)()
        Private _activeId As String

        Public Sub New(storage As IAccountStorage)
            _storage = storage
            Load()
        End Sub

        Public ReadOnly Property Accounts As List(Of AccountDto)
            Get
                Return _accounts
            End Get
        End Property

        Public ReadOnly Property ActiveId As String
            Get
                Return _activeId
            End Get
        End Property

        Public ReadOnly Property Active As AccountDto
            Get
                Return ById(_activeId)
            End Get
        End Property

        ' Logging in as the same user on the same server refreshes that entry
        ' instead of adding a second one.
        Public Function AddOrUpdate(baseUrl As String, userId As String, userName As String, token As String, serverId As String, serverName As String) As AccountDto
            Dim normalized As String = Normalize(baseUrl)
            Dim account = Find(normalized, userId)
            If account Is Nothing Then
                account = New AccountDto With {.Id = Guid.NewGuid().ToString("N")}
                _accounts.Add(account)
            End If
            account.BaseUrl = normalized
            account.UserId = userId
            account.UserName = userName
            account.AccessToken = token
            account.ServerId = serverId
            If Not String.IsNullOrEmpty(serverName) Then account.ServerName = serverName
            _activeId = account.Id
            Save()
            Return account
        End Function

        Public Sub SetActive(id As String)
            _activeId = id
            Save()
        End Sub

        ' Leaving an account does not forget it: the token stays, so coming back
        ' asks for no password.
        Public Sub ClearActive()
            _activeId = Nothing
            Save()
        End Sub

        Public Sub Remove(id As String)
            For i = _accounts.Count - 1 To 0 Step -1
                If _accounts(i).Id = id Then _accounts.RemoveAt(i)
            Next
            If _activeId = id Then _activeId = Nothing
            Save()
        End Sub

        ' For a name the migration could not know - the token is left alone.
        Public Sub SetUserName(id As String, userName As String)
            Dim account = ById(id)
            If account Is Nothing OrElse String.IsNullOrEmpty(userName) Then Return
            account.UserName = userName
            Save()
        End Sub

        Public Function Find(baseUrl As String, userId As String) As AccountDto
            Dim normalized As String = Normalize(baseUrl)
            For Each account In _accounts
                If String.Equals(account.BaseUrl, normalized, StringComparison.OrdinalIgnoreCase) AndAlso account.UserId = userId Then Return account
            Next
            Return Nothing
        End Function

        Public Function ById(id As String) As AccountDto
            If String.IsNullOrEmpty(id) Then Return Nothing
            For Each account In _accounts
                If account.Id = id Then Return account
            Next
            Return Nothing
        End Function

        ' ---- persistence ----

        Public Sub Load()
            _accounts.Clear()
            _activeId = Nothing
            Dim raw As String = Nothing
            Try
                raw = _storage.Load()
            Catch
                ' Unreadable storage: start empty and let the login screen show.
                Return
            End Try
            If String.IsNullOrEmpty(raw) Then Return
            Dim document As AccountDocumentDto = Nothing
            Try
                document = Json.Deserialize(Of AccountDocumentDto)(raw)
            Catch
                ' A corrupt document is treated as no accounts; the next save
                ' overwrites it.
                Return
            End Try
            If document Is Nothing OrElse document.Accounts Is Nothing Then Return
            For Each account In document.Accounts
                If account IsNot Nothing AndAlso Not String.IsNullOrEmpty(account.Id) Then _accounts.Add(account)
            Next
            ' A pointer to an account that is not there means nothing is active.
            If ById(document.ActiveId) IsNot Nothing Then _activeId = document.ActiveId
        End Sub

        Public Sub Save()
            Dim document As New AccountDocumentDto With {.Accounts = _accounts, .ActiveId = _activeId}
            _storage.Save(Json.Serialize(document))
        End Sub

        ' Same normalization JellyfinUrls applies to the base address.
        Public Shared Function Normalize(baseUrl As String) As String
            Return If(baseUrl, String.Empty).Trim().TrimEnd("/"c)
        End Function
    End Class
End Namespace
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the two commands from *Commands used by every task*.
Expected: `144/144 test(s) Passed` (the 127 baseline + the 11 new ones), exit 0, no `Failed`.

- [ ] **Step 6: Commit**

```bash
git add JellyfinPlayer/Shared/AccountDto.vb JellyfinPlayer/Shared/IAccountStorage.vb \
        JellyfinPlayer/Shared/AccountStore.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/AccountStoreTests.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/FakeAccountStorage.vb
git commit -m "feat: keep the saved logins in a pure store behind a storage seam"
```

---

### Task 2: The migration from the single-session keys

**Files:**
- Modify: `JellyfinPlayer/Shared/AccountStore.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/AccountStoreTests.vb`

**Interfaces:**
- Consumes: `AccountStore.AddOrUpdate` from Task 1.
- Produces: `AccountStore.MigrateFromLegacy(baseUrl As String, token As String,
  userId As String, serverId As String) As AccountDto` — returns the created
  account, or `Nothing` when there was nothing to migrate or accounts already
  exist. The caller (Task 3) removes the four legacy keys.

- [ ] **Step 1: Write the failing tests**

Append to `AccountStoreTests`:

```vb
    <TestMethod>
    Public Sub Migrates_the_single_session_keys_into_the_first_account()
        Dim store = NewStore()
        Dim a = store.MigrateFromLegacy("http://host:8096", "T", "u1", "s1")
        Assert.IsNotNull(a)
        Assert.AreEqual(1, store.Accounts.Count)
        Assert.AreEqual(a.Id, store.ActiveId)
        Assert.AreEqual("http://host:8096", a.BaseUrl)
        Assert.AreEqual("T", a.AccessToken)
        Assert.AreEqual("u1", a.UserId)
        ' The name is unknown at migration time: no network call can happen here.
        Assert.IsNull(a.UserName)
    End Sub

    <TestMethod>
    Public Sub Migration_never_overwrites_existing_accounts()
        Dim store = NewStore()
        store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS")
        Dim migrated = store.MigrateFromLegacy("http://old:8096", "OLD", "u9", "s9")
        Assert.IsNull(migrated)
        Assert.AreEqual(1, store.Accounts.Count)
        Assert.AreEqual("bob", store.Active.UserName)
    End Sub

    <TestMethod>
    Public Sub Migration_does_nothing_without_a_saved_session()
        Dim store = NewStore()
        Assert.IsNull(store.MigrateFromLegacy(Nothing, Nothing, Nothing, Nothing))
        Assert.IsNull(store.MigrateFromLegacy("http://host:8096", Nothing, "u1", "s1"))
        Assert.AreEqual(0, store.Accounts.Count)
    End Sub
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: FAIL — `MigrateFromLegacy is not a member of AccountStore`.

- [ ] **Step 3: Implement the migration**

Add to `AccountStore`, above the `---- persistence ----` comment:

```vb
        ' An app upgraded from the single-session build still has its four loose
        ' keys: they become the first account, active, so the upgrade keeps the
        ' session. Only ever runs when there are no accounts yet.
        Public Function MigrateFromLegacy(baseUrl As String, token As String, userId As String, serverId As String) As AccountDto
            If String.IsNullOrEmpty(baseUrl) OrElse String.IsNullOrEmpty(token) Then Return Nothing
            If _accounts.Count > 0 Then Return Nothing
            Return AddOrUpdate(baseUrl, userId, Nothing, token, serverId, Nothing)
        End Function
```

- [ ] **Step 4: Run the tests to verify they pass**

Expected: `141/141 test(s) Passed`, exit 0.

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/Shared/AccountStore.vb JellyfinPlayer/JellyfinPlayer.Core.Tests/AccountStoreTests.vb
git commit -m "feat: migrate the single-session keys into the first account"
```

---

### Task 3: `AppSession` as a façade over the active account

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Services/LocalAccountStorage.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/AppSession.vb` (whole file)
- Modify: `JellyfinPlayer/JellyfinPlayer/Services/NavigationService.vb` (add `ClearBackStack`)
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb:93-99` (the login stores an account)
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml.vb:44-47` (Sign out no longer clears everything)

**Interfaces:**
- Consumes: `AccountStore`, `IAccountStorage`, `AccountDto` (Tasks 1–2).
- Produces: `AppSession` with the **unchanged** surface used by the pages —
  `Current`, `Load()`, `Save()`, `IsLoggedIn`, `BaseUrl`, `AccessToken`,
  `UserId`, `ServerId`, `DeviceId`, `PreferredBitrate`, `CreateClient()` — plus
  `Accounts`, `ActiveAccount`, `Store`, `SignIn(baseUrl, userId, userName, token,
  serverId, serverName) As AccountDto`, `SwitchTo(id)`, `SignOut()`,
  `RemoveAccount(id)`, `SetUserName(id, userName)`.
  `AppSession.Clear()` is **removed** (its only caller becomes `SignOut()`).

There is no unit test for this task: it is the WinRT binding
(`ApplicationData` is unavailable to the desktop test host). The store's
behaviour is already covered by Tasks 1–2; this task is verified by the build and
by the manual checklist item added in Task 9.

- [ ] **Step 1: Write the storage binding**

`JellyfinPlayer/JellyfinPlayer/Services/LocalAccountStorage.vb`:

```vb
Imports JellyfinPlayer.Shared
Imports Windows.Storage

Namespace Services
    ' The one and only place the account document meets LocalSettings. One value
    ' holds the whole document, so a save is a single atomic write.
    Public Class LocalAccountStorage
        Implements IAccountStorage

        Public Const Key As String = "JellyfinAccounts"

        Public Function Load() As String Implements IAccountStorage.Load
            Dim values = ApplicationData.Current.LocalSettings.Values
            Dim raw As Object = Nothing
            If values.TryGetValue(Key, raw) AndAlso raw IsNot Nothing Then Return raw.ToString()
            Return Nothing
        End Function

        Public Sub Save(json As String) Implements IAccountStorage.Save
            ApplicationData.Current.LocalSettings.Values(Key) = json
        End Sub
    End Class
End Namespace
```

- [ ] **Step 2: Rewrite `AppSession`**

Replace `JellyfinPlayer/JellyfinPlayer/Services/AppSession.vb` with:

```vb
Imports System
Imports System.Collections.Generic
Imports Windows.Storage
Imports JellyfinPlayer.Shared

Namespace Services
    ' What the pages see: the active account plus the device-wide settings. The
    ' accounts themselves live in the AccountStore, so a page that only ever
    ' calls CreateClient() does not know how many logins are saved.
    Public Class AppSession
        Private Shared _current As AppSession

        Public Shared ReadOnly Property Current As AppSession
            Get
                If _current Is Nothing Then _current = New AppSession()
                Return _current
            End Get
        End Property

        Private Const KDeviceId As String = "JellyfinDeviceId"
        Private Const KBitrate As String = "JellyfinBitrate"
        ' The single-session keys of the previous build: read once by Load() to
        ' migrate them, then removed.
        Private Const KLegacyBaseUrl As String = "JellyfinBaseUrl"
        Private Const KLegacyAccessToken As String = "JellyfinAccessToken"
        Private Const KLegacyUserId As String = "JellyfinUserId"
        Private Const KLegacyServerId As String = "JellyfinServerId"

        Private ReadOnly _store As AccountStore

        Public Sub New()
            _store = New AccountStore(New LocalAccountStorage())
        End Sub

        Public ReadOnly Property Store As AccountStore
            Get
                Return _store
            End Get
        End Property

        Public ReadOnly Property Accounts As List(Of AccountDto)
            Get
                Return _store.Accounts
            End Get
        End Property

        Public ReadOnly Property ActiveAccount As AccountDto
            Get
                Return _store.Active
            End Get
        End Property

        Public Property DeviceId As String
        Public Property PreferredBitrate As Integer = DeviceProfileBuilder.DefaultBitrate

        Public ReadOnly Property BaseUrl As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.BaseUrl)
            End Get
        End Property

        Public ReadOnly Property AccessToken As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.AccessToken)
            End Get
        End Property

        Public ReadOnly Property UserId As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.UserId)
            End Get
        End Property

        Public ReadOnly Property ServerId As String
            Get
                Return If(ActiveAccount Is Nothing, Nothing, ActiveAccount.ServerId)
            End Get
        End Property

        Public ReadOnly Property IsLoggedIn As Boolean
            Get
                Return Not String.IsNullOrEmpty(AccessToken) AndAlso Not String.IsNullOrEmpty(BaseUrl)
            End Get
        End Property

        Public Sub Load()
            Dim s = ApplicationData.Current.LocalSettings.Values
            DeviceId = ReadString(s, KDeviceId)
            If String.IsNullOrEmpty(DeviceId) Then
                DeviceId = Guid.NewGuid().ToString("N")
                s(KDeviceId) = DeviceId
            End If

            Dim raw As Object = Nothing
            If s.TryGetValue(KBitrate, raw) AndAlso raw IsNot Nothing Then
                PreferredBitrate = CInt(raw)
            Else
                PreferredBitrate = DeviceProfileBuilder.DefaultBitrate
            End If

            ' An upgrade arrives with four loose keys and no document.
            _store.MigrateFromLegacy(ReadString(s, KLegacyBaseUrl), ReadString(s, KLegacyAccessToken),
                                     ReadString(s, KLegacyUserId), ReadString(s, KLegacyServerId))
            For Each key In New String() {KLegacyBaseUrl, KLegacyAccessToken, KLegacyUserId, KLegacyServerId}
                If s.ContainsKey(key) Then s.Remove(key)
            Next
        End Sub

        Public Sub Save()
            Dim s = ApplicationData.Current.LocalSettings.Values
            s(KDeviceId) = DeviceId
            s(KBitrate) = PreferredBitrate
            _store.Save()
        End Sub

        ' A successful login: the same server and user refreshes that entry.
        Public Function SignIn(baseUrl As String, userId As String, userName As String, token As String, serverId As String, serverName As String) As AccountDto
            Return _store.AddOrUpdate(baseUrl, userId, userName, token, serverId, serverName)
        End Function

        Public Sub SwitchTo(id As String)
            _store.SetActive(id)
        End Sub

        ' Leaving an account keeps it (and its token) saved.
        Public Sub SignOut()
            _store.ClearActive()
        End Sub

        Public Sub RemoveAccount(id As String)
            _store.Remove(id)
        End Sub

        Public Sub SetUserName(id As String, userName As String)
            _store.SetUserName(id, userName)
        End Sub

        Public Function CreateClient() As JellyfinApiClient
            Dim account = ActiveAccount
            Dim urls As New JellyfinUrls(If(account Is Nothing, Nothing, account.BaseUrl),
                                         If(account Is Nothing, Nothing, account.AccessToken))
            Dim client As New JellyfinApiClient(New HttpTransport(), urls, DeviceId)
            If account IsNot Nothing Then
                client.AccessToken = account.AccessToken
                client.UserId = account.UserId
                client.ServerId = account.ServerId
            End If
            Return client
        End Function

        Private Shared Function ReadString(s As IDictionary(Of String, Object), key As String) As String
            Dim v As Object = Nothing
            If s.TryGetValue(key, v) AndAlso v IsNot Nothing Then Return v.ToString()
            Return Nothing
        End Function
    End Class
End Namespace
```

- [ ] **Step 3: Add `ClearBackStack` to the navigation service**

The login and the switch both need it, and `MainPage` changes in the next step
cannot compile without it.

In `Services/NavigationService.vb`, after `GoBack()`:

```vb
        ' A switch of account must not leave the previous user's screens behind
        ' it: going back would walk into another account's library.
        Public Sub ClearBackStack()
            Dim f = RootFrame
            If f IsNot Nothing Then f.BackStack.Clear()
        End Sub
```

- [ ] **Step 4: Point the login and Sign out at the new API**

`MainPage` currently assigns the four session properties, which are now read-only
derived values, and `SettingsPage` calls the removed `Clear()`. In
`MainPage.xaml.vb`, replace the block

```vb
            session.BaseUrl = urls.BaseUrl
            session.AccessToken = client.AccessToken
            session.UserId = client.UserId
            session.ServerId = client.ServerId
            session.Save()

            NavigationService.Navigate(GetType(HomePage), Nothing)
```

with

```vb
            ' The name is what the picker shows, so it is stored with the token.
            session.SignIn(urls.BaseUrl, client.UserId, If(result.User Is Nothing, Nothing, result.User.Name),
                           client.AccessToken, client.ServerId, info.ServerName)

            ' Entering a different account must not leave the previous one's
            ' screens in the back stack.
            NavigationService.ClearBackStack()
            NavigationService.Navigate(GetType(HomePage), Nothing)
```

In `Pages/SettingsPage.xaml.vb`, replace the body of `SignOut_Click`:

```vb
    Private Sub SignOut_Click(sender As Object, e As RoutedEventArgs)
        ' Leaves the account without forgetting it: its token stays, so coming
        ' back asks for no password.
        AppSession.Current.SignOut()
        NavigationService.Navigate(GetType(MainPage), Nothing)
    End Sub
```

Task 9 replaces that navigation target with the picker, and Task 7 adds the
re-login mode to `MainPage`. The rest of both pages is untouched here.

- [ ] **Step 5: Build and run the whole suite**

Run the two commands from *Commands used by every task*.
Expected: build ends with `Your package has been successfully created.`, tests
`141/141 test(s) Passed`, exit 0 — the same numbers as Task 2, because no test
input changed and the pages now go through the façade.

- [ ] **Step 6: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Services/AppSession.vb \
        JellyfinPlayer/JellyfinPlayer/Services/LocalAccountStorage.vb \
        JellyfinPlayer/JellyfinPlayer/Services/NavigationService.vb \
        JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb \
        JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml.vb
git commit -m "feat: make AppSession a facade over the active account"
```

---

### Task 4: `GetUserAsync` and the failure helper for a rejected token

**Files:**
- Modify: `JellyfinPlayer/Shared/JellyfinUrls.vb` (add `User`)
- Modify: `JellyfinPlayer/Shared/JellyfinApiClient.vb` (add `GetUserAsync`)
- Modify: `JellyfinPlayer/Shared/FailureText.vb` (add `IsUnauthorized`)
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/UrlsTests.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/AuthTests.vb`
- Test: `JellyfinPlayer/JellyfinPlayer.Core.Tests/FailurePathTests.vb`

**Interfaces:**
- Consumes: existing `TokenHeaders()`, `JellyfinUrls.WithKey`.
- Produces: `JellyfinUrls.User(userId) As String`,
  `JellyfinApiClient.GetUserAsync(userId As String) As Task(Of UserDto)`,
  `FailureText.IsUnauthorized(ex As Exception) As Boolean`.

- [ ] **Step 1: Write the failing tests**

Append to `UrlsTests`:

```vb
    <TestMethod>
    Public Sub User_url_carries_the_api_key()
        Dim u = MakeUrls().User("u1")
        Assert.IsTrue(u.EndsWith("/Users/u1"), u)
        Assert.IsTrue(u.Contains("api_key=TOK"), u)
    End Sub
```

Append to `AuthTests`:

```vb
    <TestMethod>
    Public Sub GetUser_returns_the_name()
        Dim fake = New FakeTransport()
        fake.Handler = Function(u) "{""Id"":""u1"",""Name"":""bob""}"
        Dim user = MakeClient(fake).GetUserAsync("u1").Result
        Assert.AreEqual("bob", user.Name)
    End Sub
```

Append to `FailurePathTests`:

```vb
    <TestMethod>
    Public Sub IsUnauthorized_is_true_for_401_and_403()
        Assert.IsTrue(FailureText.IsUnauthorized(New JellyfinException("nope", 401)))
        Assert.IsTrue(FailureText.IsUnauthorized(New JellyfinException("nope", 403)))
        Assert.IsFalse(FailureText.IsUnauthorized(New JellyfinException("nope", 500)))
        Assert.IsFalse(FailureText.IsUnauthorized(New JellyfinException("timeout", 0, JellyfinErrorKind.Timeout)))
        Assert.IsFalse(FailureText.IsUnauthorized(New Exception("boom")))
    End Sub
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: FAIL — `User is not a member of JellyfinUrls`, `GetUserAsync is not a
member of JellyfinApiClient`, `IsUnauthorized is not a member of FailureText`.

- [ ] **Step 3: Implement the three additions**

In `JellyfinUrls`, after `PublicUsers()`:

```vb
        ' The signed-in user's own record; the picker needs User.Name from it.
        Public Function User(userId As String) As String
            Return WithKey(U("/Users/" & userId))
        End Function
```

In `JellyfinApiClient`, after `GetPublicUsersAsync`:

```vb
        Public Async Function GetUserAsync(userId As String) As Task(Of UserDto)
            Dim body As String = Await _transport.GetStringAsync(_urls.User(userId), TokenHeaders())
            Return ResponseFailures.Parse(Of UserDto)(body)
        End Function
```

In `FailureText`, before `Describe`:

```vb
        ' The saved token of this account is no longer accepted: the caller should
        ' ask for the password again instead of printing an HTTP failure.
        Public Function IsUnauthorized(ex As Exception) As Boolean
            Dim failure = TryCast(ex, JellyfinException)
            If failure Is Nothing Then Return False
            Return failure.Kind = JellyfinErrorKind.HttpFailure AndAlso (failure.StatusCode = 401 OrElse failure.StatusCode = 403)
        End Function
```

- [ ] **Step 4: Run the tests to verify they pass**

Expected: `144/144 test(s) Passed`, exit 0.

- [ ] **Step 5: Commit**

```bash
git add JellyfinPlayer/Shared/JellyfinUrls.vb JellyfinPlayer/Shared/JellyfinApiClient.vb \
        JellyfinPlayer/Shared/FailureText.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/UrlsTests.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/AuthTests.vb \
        JellyfinPlayer/JellyfinPlayer.Core.Tests/FailurePathTests.vb
git commit -m "feat: read a user's name and recognise a rejected token"
```

---

### Task 5: The picker page

**Files:**
- Create: `JellyfinPlayer/JellyfinPlayer/Services/AccountItem.vb`
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/AccountsPage.xaml`
- Create: `JellyfinPlayer/JellyfinPlayer/Pages/AccountsPage.xaml.vb`
- Modify: `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj` (page + code-behind entries)
- Modify: `JellyfinPlayer/JellyfinPlayer/Strings/en-US/Resources.resw`
- Modify: `JellyfinPlayer/JellyfinPlayer/Strings/it-IT/Resources.resw`

**Interfaces:**
- Consumes: `AppSession.Accounts`, `Store.ActiveId`, `SwitchTo(id)`,
  `RemoveAccount(id)` (Task 3).
- Produces: `AccountItem` with `Account`, `Title`, `Subtitle`, `IsActive`,
  `Marker` (localized "in use" text or empty); `AccountItem.Build(accounts As
  IList(Of AccountDto), activeId As String) As List(Of AccountItem)` and
  `AccountItem.DisplayName(account As AccountDto) As String` (used by Tasks 7 and
  9). `AccountsPage` is reachable by
  `NavigationService.Navigate(GetType(AccountsPage), Nothing)`, and it uses
  `NavigationService.ClearBackStack()` from Task 3.

No unit test: WP8.1 has no UI automation, and `AccountItem` lives in `Services`
next to `PosterItem`, outside the test project's linked set. Verified by the
build, `LocalizationTests`, `XamlResourceTests` (both already scan every
`Pages/*.xaml`) and checklist item 9 in Task 9.

- [ ] **Step 1: Add the strings to both languages**

Append before `</root>` of `Strings/en-US/Resources.resw`:

```xml
  <data name="AccountsHeader.Text" xml:space="preserve">
    <value>Accounts</value>
  </data>
  <data name="AddAccountButton.Content" xml:space="preserve">
    <value>Add account</value>
  </data>
  <data name="RemoveAccountButton.Content" xml:space="preserve">
    <value>Remove account</value>
  </data>
  <data name="SwitchAccountButton.Content" xml:space="preserve">
    <value>Switch account</value>
  </data>
  <data name="CurrentAccountMarker" xml:space="preserve">
    <value>In use</value>
  </data>
  <data name="PasswordRequiredStatus" xml:space="preserve">
    <value>Enter the password for {0}.</value>
  </data>
```

and the same keys with Italian values in `Strings/it-IT/Resources.resw`:

```xml
  <data name="AccountsHeader.Text" xml:space="preserve">
    <value>Account</value>
  </data>
  <data name="AddAccountButton.Content" xml:space="preserve">
    <value>Aggiungi account</value>
  </data>
  <data name="RemoveAccountButton.Content" xml:space="preserve">
    <value>Rimuovi account</value>
  </data>
  <data name="SwitchAccountButton.Content" xml:space="preserve">
    <value>Cambia account</value>
  </data>
  <data name="CurrentAccountMarker" xml:space="preserve">
    <value>In uso</value>
  </data>
  <data name="PasswordRequiredStatus" xml:space="preserve">
    <value>Inserisci la password di {0}.</value>
  </data>
```

- [ ] **Step 2: Write the view model**

`JellyfinPlayer/JellyfinPlayer/Services/AccountItem.vb`:

```vb
Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared

Namespace Services
    ' One row of the picker; keeps XAML binding simple (no converters), like PosterItem.
    Public Class AccountItem
        Public Property Account As AccountDto
        Public Property Title As String
        Public Property Subtitle As String
        Public Property IsActive As Boolean
        Public Property Marker As String

        Public Shared Function Build(accounts As IList(Of AccountDto), activeId As String) As List(Of AccountItem)
            Dim result As New List(Of AccountItem)()
            If accounts Is Nothing Then Return result
            For Each account In accounts
                result.Add(One(account, activeId))
            Next
            Return result
        End Function

        Public Shared Function One(account As AccountDto, activeId As String) As AccountItem
            Dim item As New AccountItem()
            item.Account = account
            item.Title = DisplayName(account)
            item.Subtitle = SubtitleFor(account)
            item.IsActive = account.Id = activeId
            item.Marker = If(item.IsActive, Localized.Text("CurrentAccountMarker"), "")
            Return item
        End Function

        ' A migrated account has no name until Home fetches it once.
        Public Shared Function DisplayName(account As AccountDto) As String
            If account Is Nothing Then Return ""
            If Not String.IsNullOrEmpty(account.UserName) Then Return account.UserName
            Return If(account.UserId, "")
        End Function

        Private Shared Function SubtitleFor(account As AccountDto) As String
            If account Is Nothing Then Return ""
            If String.IsNullOrEmpty(account.ServerName) Then Return account.BaseUrl
            Return account.ServerName & " - " & account.BaseUrl
        End Function
    End Class
End Namespace
```

- [ ] **Step 3: Write the page**

`JellyfinPlayer/JellyfinPlayer/Pages/AccountsPage.xaml`:

```xml
<Page
    x:Class="JellyfinPlayer.AccountsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d"
    Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock x:Uid="AccountsHeader" Grid.Row="0" FontSize="26" FontWeight="Bold" Foreground="#00A4DC" Margin="20,16,0,0"/>

        <ListView x:Name="AccountsList" Grid.Row="1" Margin="8,8,8,0"
                  SelectionMode="Single" IsItemClickEnabled="True"
                  SelectionChanged="Account_SelectionChanged" ItemClick="Account_Click">
            <ListView.ItemTemplate>
                <DataTemplate>
                    <StackPanel Margin="12,8">
                        <TextBlock Text="{Binding Title}" FontSize="18" FontWeight="Bold"/>
                        <TextBlock Text="{Binding Subtitle}" FontSize="13" Opacity="0.7" TextWrapping="Wrap"/>
                        <TextBlock Text="{Binding Marker}" FontSize="13" Foreground="#00A4DC"/>
                    </StackPanel>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="20,12,0,16">
            <Button x:Name="AddButton" x:Uid="AddAccountButton" Click="Add_Click"/>
            <Button x:Name="RemoveButton" x:Uid="RemoveAccountButton" IsEnabled="False" Click="Remove_Click" Margin="8,0,0,0"/>
        </StackPanel>
    </Grid>
</Page>
```

`JellyfinPlayer/JellyfinPlayer/Pages/AccountsPage.xaml.vb`:

```vb
Imports System
Imports System.Collections.Generic
Imports JellyfinPlayer.Shared
Imports JellyfinPlayer.Services
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation

''' <summary>
''' The account picker: enter one of the saved logins, add another, forget one.
''' </summary>
Public NotInheritable Class AccountsPage
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Protected Overrides Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)
        Refresh()
    End Sub

    Private Sub Refresh()
        Dim session = AppSession.Current
        AccountsList.ItemsSource = AccountItem.Build(session.Accounts, session.Store.ActiveId)
        RemoveButton.IsEnabled = False
    End Sub

    Private Sub Account_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        RemoveButton.IsEnabled = AccountsList.SelectedItem IsNot Nothing
    End Sub

    Private Sub Account_Click(sender As Object, e As ItemClickEventArgs)
        EnterAccount(TryCast(e.ClickedItem, AccountItem))
    End Sub

    Private Sub EnterAccount(item As AccountItem)
        If item Is Nothing Then Return
        AppSession.Current.SwitchTo(item.Account.Id)
        NavigationService.ClearBackStack()
        NavigationService.Navigate(GetType(HomePage), Nothing)
    End Sub

    Private Sub Add_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(MainPage), Nothing)
    End Sub

    Private Sub Remove_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(AccountsList.SelectedItem, AccountItem)
        If item Is Nothing Then Return
        AppSession.Current.RemoveAccount(item.Account.Id)
        Refresh()
    End Sub
End Class
```

A single tap opens an account (`ItemClick`); selection only enables **Remove
account**, so the two gestures do not fight each other.

- [ ] **Step 4: Register the page in the project**

In `JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj`, add next to the other
pages (keep the existing order: alphabetically inside `Pages\`):

```xml
    <Page Include="Pages\AccountsPage.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </Page>
```

and next to the other code-behind entries:

```xml
    <Compile Include="Pages\AccountsPage.xaml.vb">
      <DependentUpon>AccountsPage.xaml</DependentUpon>
    </Compile>
```

- [ ] **Step 5: Build**

Run the build command. Expected: `Your package has been successfully created.`,
exit 0, with no XAML errors and no `LocalizationTests` mismatch (run the test
command too: `144/144`).

- [ ] **Step 6: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Services/AccountItem.vb \
        JellyfinPlayer/JellyfinPlayer/Pages/AccountsPage.xaml \
        JellyfinPlayer/JellyfinPlayer/Pages/AccountsPage.xaml.vb \
        JellyfinPlayer/JellyfinPlayer/JellyfinPlayer.vbproj \
        JellyfinPlayer/JellyfinPlayer/Strings/en-US/Resources.resw \
        JellyfinPlayer/JellyfinPlayer/Strings/it-IT/Resources.resw
git commit -m "feat: add the account picker"
```

---

### Task 6: Startup routing

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/App.xaml.vb:57`

**Interfaces:**
- Consumes: `AppSession.IsLoggedIn`, `AppSession.Accounts` (Task 3);
  `AccountsPage` (Task 5).
- Produces: nothing for other tasks.

No unit test (`App.OnLaunched` needs `Window.Current`); verified by the build
plus checklist item 9.

- [ ] **Step 1: Route the launch by state**

In `App.xaml.vb`, replace the target line inside `OnLaunched`:

```vb
            Dim session = AppSession.Current
            Dim target As Type = GetType(MainPage)
            If session.IsLoggedIn Then
                target = GetType(HomePage)
            ElseIf session.Accounts.Count > 0 Then
                ' Accounts exist but none is in use: the picker is the way back in.
                target = GetType(AccountsPage)
            End If
```

- [ ] **Step 2: Build**

Run the build command. Expected: `Your package has been successfully created.`,
exit 0. Run the test command too: `144/144 test(s) Passed`.

- [ ] **Step 3: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/App.xaml.vb
git commit -m "feat: enter the last account and leave the picker otherwise"
```

---

### Task 7: Sign in again, and signing in as a new account

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml`
- Modify: `JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb`

**Interfaces:**
- Consumes: `AppSession.SignIn(...)`, `AccountDto` (Task 3), `AccountItem`
  (Task 5 — for the title of the re-login prompt), `NavigationService.ClearBackStack()`
  (Task 6), `Localized.Format` (existing).
- Produces: `MainPage` understood as two modes — **new account** (no parameter:
  today's behaviour) and **sign in again** (navigated with the `AccountDto` as
  the parameter: server and user prefilled, password empty, discovery hidden).

No unit test (WinRT page); verified by the build, `LocalizationTests` (the new
`x:Uid` must resolve) and checklist items 3 and 5.

- [ ] **Step 1: Give the discovery row a name so it can be hidden**

In `MainPage.xaml`, wrap the discovery status in a named element and keep the
rest of the layout identical:

```xml
            <TextBlock x:Uid="ServerAddressLabel" Margin="0,20,0,4"/>
            <TextBox x:Name="ServerBox" PlaceholderText="http://192.168.1.10:8096" IsEnabled="True"/>
            <TextBlock x:Name="DiscoveryStatus" TextWrapping="Wrap" Opacity="0.7" Margin="0,4,0,0"/>
```

(`ServerBox.IsEnabled` is what the re-login mode turns off, so a prefilled
address cannot be edited into a different server while re-authenticating.)

- [ ] **Step 2: Implement the two modes in the code-behind**

In `MainPage.xaml.vb`, replace `OnNavigatedTo` and `ConnectButton_Click`:

```vb
    Private _reLogin As AccountDto

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        MyBase.OnNavigatedTo(e)

        _reLogin = TryCast(e.Parameter, AccountDto)
        If _reLogin IsNot Nothing Then
            ' Signing in again for one saved account: the address and the user are
            ' known, only the password is missing, and nothing is discovered.
            ServerBox.Text = _reLogin.BaseUrl
            ServerBox.IsEnabled = False
            UserBox.Text = AccountItem.DisplayName(_reLogin)
            DiscoveryStatus.Text = Localized.Format("PasswordRequiredStatus", AccountItem.DisplayName(_reLogin))
            Return
        End If

        Dim session = AppSession.Current
        If Not String.IsNullOrEmpty(session.BaseUrl) Then
            ServerBox.Text = session.BaseUrl
            DiscoveryStatus.Text = Localized.Text("StatusUsingSavedServer")
            Return
        End If

        ServerBox.Text = "http://"
        Await DiscoverAsync()
    End Sub

    Private Async Sub ConnectButton_Click(sender As Object, e As RoutedEventArgs)
        Dim baseUrl As String = ServerBox.Text.Trim()
        If String.IsNullOrEmpty(baseUrl) Then
            StatusText.Text = Localized.Text("StatusEnterAddress")
            Return
        End If
        If Not baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) Then baseUrl = "http://" & baseUrl

        Busy.IsActive = True
        ConnectButton.IsEnabled = False
        StatusText.Text = ""

        Try
            Dim session = AppSession.Current
            Dim urls As New JellyfinUrls(baseUrl)
            Dim client As New JellyfinApiClient(New HttpTransport(), urls, session.DeviceId)

            Dim info = Await client.GetServerInfoAsync()
            If info Is Nothing Then Throw New JellyfinException(Localized.Text("StatusNotJellyfin"), 0)

            Dim result = Await client.AuthenticateAsync(UserBox.Text.Trim(), PassBox.Password)
            If result Is Nothing OrElse String.IsNullOrEmpty(result.AccessToken) Then
                StatusText.Text = Localized.Text("StatusWrongCredentials")
                Return
            End If

            ' The name is what the picker shows, so it is stored with the token.
            session.SignIn(urls.BaseUrl, client.UserId, If(result.User Is Nothing, Nothing, result.User.Name),
                           client.AccessToken, client.ServerId, info.ServerName)

            ' Entering a different account must not leave the previous one's
            ' screens in the back stack.
            NavigationService.ClearBackStack()
            NavigationService.Navigate(GetType(HomePage), Nothing)
        Catch ex As JellyfinException
            StatusText.Text = If(ex.StatusCode = 401, Localized.Text("StatusWrongCredentials"), Localized.DescribeError(ex))
        Catch ex As Exception
            StatusText.Text = Localized.DescribeError(ex)
        Finally
            Busy.IsActive = False
            ConnectButton.IsEnabled = True
        End Try
    End Sub
```

- [ ] **Step 3: Build and run the suite**

Expected: build `Your package has been successfully created.`, tests
`144/144 test(s) Passed`, exit 0.

- [ ] **Step 4: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/MainPage.xaml JellyfinPlayer/JellyfinPlayer/MainPage.xaml.vb
git commit -m "feat: sign in again for one account, or add a new one"
```

---

### Task 8: Home — the switch button, the 401 detour and the missing name

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml` (header)
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml.vb`

**Interfaces:**
- Consumes: `FailureText.IsUnauthorized` (Task 4), `GetUserAsync` (Task 4),
  `AppSession.SetUserName`, `ActiveAccount` (Task 3), `AccountsPage` (Task 5).
- Produces: nothing new for other tasks; this is the last consumer.

No unit test (WinRT page); verified by the build, `XamlResourceTests` (the new
icon must be a `Path` with inline `Data`) and checklist items 3, 4, 5.

- [ ] **Step 1: Add the switch button to the header**

In `HomePage.xaml`, inside the right-hand `StackPanel` of the header, before the
Search button:

```xml
                <Button x:Uid="SwitchAccountButton" Click="SwitchAccount_Click">
                    <Path Data="M12,2 A5,5 0 1 1 11.99,2 Z M4,20 C4,16.7 7.6,14.5 12,14.5 C16.4,14.5 20,16.7 20,20 L20,22 L4,22 Z"
                          Fill="#00A4DC" Stretch="Uniform" Width="20" Height="20"/>
                </Button>
```

The path data sits in the `Path`, never in a `<Geometry x:Key>` resource: WinRT
XAML cannot create the `Geometry` base type and the page would silently refuse to
load.

- [ ] **Step 2: Handle the switch command**

In `HomePage.xaml.vb`, next to the other click handlers:

```vb
    Private Sub SwitchAccount_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(AccountsPage), Nothing)
    End Sub
```

- [ ] **Step 3: Ask for the password when the saved token is refused**

In `HomePage.xaml.vb`, the three `Catch ex As Exception` blocks inside
`LoadAsync` currently all set `ErrorText.Text`. Replace each of them with the
same two-line guard so a dead token goes to the re-login instead of printing an
HTTP sentence:

```vb
        Catch ex As Exception
            If FailureText.IsUnauthorized(ex) Then
                NavigationService.Navigate(GetType(MainPage), session.ActiveAccount)
                Return
            End If
            ErrorText.Text = Localized.DescribeError(ex)
        End Try
```

(`Return` leaves `LoadAsync`; `_reLogin` keeps the account, so a wrong password
can be retried and the other accounts are untouched.)

- [ ] **Step 4: Fill a name the migration could not know**

In `HomePage.xaml.vb`, at the end of `LoadAsync` (after the three blocks):

```vb
        ' A migrated account has no name in the store; fetch it once so the picker
        ' shows a person instead of an id.
        If String.IsNullOrEmpty(session.ActiveAccount.UserName) Then
            Try
                Dim user = Await client.GetUserAsync(session.UserId)
                If user IsNot Nothing Then session.SetUserName(session.ActiveAccount.Id, user.Name)
            Catch ex As Exception
                ' The name is cosmetic: a failure here must not disturb Home.
            End Try
        End If
```

- [ ] **Step 5: Build and run the suite**

Expected: build `Your package has been successfully created.`, tests
`144/144 test(s) Passed`, exit 0.

- [ ] **Step 6: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml JellyfinPlayer/JellyfinPlayer/Pages/HomePage.xaml.vb
git commit -m "feat: switch accounts from Home and recover a refused token"
```

---

### Task 9: Settings — the name, the switch and the new sign out

**Files:**
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml`
- Modify: `JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml.vb`

**Interfaces:**
- Consumes: `AccountItem.DisplayName` (Task 5), `AppSession.SignOut()` and
  `AccountsPage` (Tasks 3 and 5).
- Produces: nothing for other tasks.

- [ ] **Step 1: Add the switch button to the page**

In `SettingsPage.xaml`, before `SignOutButton`:

```xml
        <Button x:Uid="SwitchAccountButton" Margin="0,24,0,0" Click="Switch_Click"/>
```

- [ ] **Step 2: Show the name and rewire the two actions**

In `SettingsPage.xaml.vb`, replace the body of `OnNavigatedTo`'s account lines
and `SignOut_Click`, and add `Switch_Click`:

```vb
        Dim session = AppSession.Current
        ServerText.Text = session.BaseUrl
        Dim name = AccountItem.DisplayName(session.ActiveAccount)
        AccountText.Text = If(String.IsNullOrEmpty(name), Localized.Text("NotSignedIn"), name)
```

```vb
    Private Sub Switch_Click(sender As Object, e As RoutedEventArgs)
        NavigationService.Navigate(GetType(AccountsPage), Nothing)
    End Sub

    Private Sub SignOut_Click(sender As Object, e As RoutedEventArgs)
        ' Leaves the account without forgetting it: its token stays, so coming
        ' back asks for no password.
        AppSession.Current.SignOut()
        NavigationService.ClearBackStack()
        NavigationService.Navigate(GetType(AccountsPage), Nothing)
    End Sub
```

- [ ] **Step 3: Build and run the suite**

Expected: build `Your package has been successfully created.`, tests
`144/144 test(s) Passed`, exit 0.

- [ ] **Step 4: Commit**

```bash
git add JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml JellyfinPlayer/JellyfinPlayer/Pages/SettingsPage.xaml.vb
git commit -m "feat: show the account name and switch accounts from Settings"
```

---

### Task 10: Documentation and the manual checklist

**Files:**
- Modify: `agents/ARCHITECTURE.md`
- Modify: `agents/TESTING.md`
- Modify: `agents/JELLYFIN_API.md`
- Modify: `README.md`
- Modify: `README.it.md`

**Interfaces:** none — documentation only.

- [ ] **Step 1: Architecture**

In `agents/ARCHITECTURE.md`, replace the `Services/AppSession.vb` bullet with:

```markdown
- `Shared/AccountStore.vb` + `Shared/IAccountStorage.vb` — the saved logins and
  which one is active, persisted as one JSON document. Pure, so the desktop tests
  drive it through an in-memory storage; `Services/LocalAccountStorage.vb` is the
  WinRT binding onto a single `LocalSettings` value (`JellyfinAccounts`).
- `Services/AppSession.vb` — the façade the pages talk to: the active account's
  server/token/user plus the device-wide id and bitrate. `CreateClient()` builds
  a ready-to-use `JellyfinApiClient` for the active account, so a page cannot
  tell how many logins are saved.
```

and add to *App-side layers*:

```markdown
- `Services/AccountItem.vb` — the picker's view model (user name, server, "in
  use" marker); the name falls back to the user id for an account migrated from
  the single-session build until Home fetches it once.
```

- [ ] **Step 2: API doc**

In `agents/JELLYFIN_API.md`'s endpoint table, after the "Public users" row:

```markdown
| Own user record | `GET /Users/{userId}` (fills `User.Name` for the account picker) |
```

- [ ] **Step 3: Manual checklist**

In `agents/TESTING.md`, add to *Verification the automated tests cannot do*:

```markdown
- **Accounts**: with two logins saved (two users on the same server, or one user
  on each of two servers), launching goes straight into the last one used. Tap the
  header's switch button → the picker lists both, the one in use is marked;
  tapping the other opens *its* Home, and the back button does not walk back into
  the first account's screens. Remove account forgets only that login and leaves
  the other usable.
- **Sign out keeps the account**: Settings → Sign out → the picker, where that
  account is still listed; tapping it re-enters with **no** password prompt.
- **Refused token**: revoke the app's device from the Jellyfin dashboard (or
  sign the user out from another client), then launch → the app asks for that
  account's password only, with the server and user already filled in; typing it
  enters Home, and the other accounts were never touched.
- **Upgrade from the previous build**: install over a version that had the single
  session, launch → Home opens for the same user with no login (the four old keys
  became the first account) and the picker shows that account, by user id until
  the first Home load has fetched its name.
```

- [ ] **Step 4: READMEs**

Add the feature bullet after the sign-in line in `README.md`:

```markdown
- Several accounts on one phone: the app enters the last one used, and the
  picker switches user without retyping a password.
```

and in `README.it.md`:

```markdown
- Più account sullo stesso telefono: l'app entra nell'ultimo usato e il selettore
  cambia utente senza ridigitare la password.
```

- [ ] **Step 5: Full verification**

Run both commands from *Commands used by every task*, then the release build:

```bash
"/c/Program Files (x86)/MSBuild/12.0/Bin/MSBuild.exe" JellyfinPlayer/JellyfinPlayer.sln \
  -p:Configuration=Release -p:Platform=ARM -p:VisualStudioVersion=12.0 -t:Build -nologo -v:minimal
```

Expected: `Debug|x86` and `Release|ARM` both end with `Your package has been
successfully created.`, the suite prints `144/144 test(s) Passed` with exit 0 and
no `Failed`/`Inconclusive` (the 7 integration tests run against the configured
server).

- [ ] **Step 6: Commit and push**

```bash
git add agents/ARCHITECTURE.md agents/TESTING.md agents/JELLYFIN_API.md README.md README.it.md
git commit -m "docs: describe the multiple-account store, picker and checklist"
git push origin main
```
