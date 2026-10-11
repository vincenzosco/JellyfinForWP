<TestClass>
Public Class AccountStoreTests
    ' Named NewStore, not Store: VB is case-insensitive and a local `store` would
    ' then collide with the helper.
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
End Class
