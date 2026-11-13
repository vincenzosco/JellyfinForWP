using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class AccountStoreTests
    {
        private AccountStore NewStore(FakeAccountStorage storage = null)
        {
            return new AccountStore(storage ?? new FakeAccountStorage());
        }

        [TestMethod]
        public void Adds_an_account_and_makes_it_active()
        {
            var store = NewStore();
            var a = store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS");
            Assert.AreEqual(1, store.Accounts.Count);
            Assert.AreEqual(a.Id, store.ActiveId);
            Assert.AreEqual("bob", store.Active.UserName);
        }

        [TestMethod]
        public void Normalizes_the_server_address()
        {
            var store = NewStore();
            store.AddOrUpdate("http://host:8096/", "u1", "bob", "T", "s1", "NAS");
            Assert.AreEqual("http://host:8096", store.Accounts[0].BaseUrl);
        }

        // The same user on the same server is one account, refreshed - not a duplicate.
        [TestMethod]
        public void Signing_in_again_refreshes_instead_of_duplicating()
        {
            var store = NewStore();
            var first = store.AddOrUpdate("http://host:8096", "u1", "bob", "OLD", "s1", "NAS");
            var second = store.AddOrUpdate("http://host:8096/", "u1", "bobby", "NEW", "s1", "NAS");
            Assert.AreEqual(1, store.Accounts.Count);
            Assert.AreEqual(first.Id, second.Id);
            Assert.AreEqual("NEW", store.Accounts[0].AccessToken);
            Assert.AreEqual("bobby", store.Accounts[0].UserName);
        }

        [TestMethod]
        public void A_second_user_on_the_same_server_is_a_second_account()
        {
            var store = NewStore();
            store.AddOrUpdate("http://host:8096", "u1", "bob", "T1", "s1", "NAS");
            store.AddOrUpdate("http://host:8096", "u2", "eve", "T2", "s1", "NAS");
            Assert.AreEqual(2, store.Accounts.Count);
        }

        [TestMethod]
        public void ClearActive_leaves_the_account_and_its_token()
        {
            var store = NewStore();
            store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS");
            store.ClearActive();
            Assert.IsNull(store.Active);
            Assert.AreEqual(1, store.Accounts.Count);
            Assert.AreEqual("T", store.Accounts[0].AccessToken);
        }

        [TestMethod]
        public void Removing_the_active_account_clears_the_pointer()
        {
            var store = NewStore();
            var a = store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS");
            store.Remove(a.Id);
            Assert.AreEqual(0, store.Accounts.Count);
            Assert.IsNull(store.ActiveId);
        }

        [TestMethod]
        public void SetUserName_fills_a_name_without_touching_the_token()
        {
            var store = NewStore();
            var a = store.AddOrUpdate("http://host:8096", "u1", null, "T", "s1", null);
            store.SetUserName(a.Id, "bob");
            Assert.AreEqual("bob", store.Accounts[0].UserName);
            Assert.AreEqual("T", store.Accounts[0].AccessToken);
        }

        [TestMethod]
        public void Round_trips_through_the_storage_document()
        {
            var storage = new FakeAccountStorage();
            var first = NewStore(storage);
            var a = first.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS");
            first.AddOrUpdate("http://other:8096", "u2", "eve", "T2", "s2", "Other");
            first.SetActive(a.Id);

            var second = NewStore(storage);
            Assert.AreEqual(2, second.Accounts.Count);
            Assert.AreEqual(a.Id, second.ActiveId);
            Assert.AreEqual("bob", second.Active.UserName);
            Assert.AreEqual("Other", second.ById(second.Accounts[1].Id).ServerName);
        }

        // A bad document must not stop the app from starting.
        [TestMethod]
        public void A_corrupt_document_reads_as_an_empty_store()
        {
            var storage = new FakeAccountStorage("{ this is not json");
            var store = NewStore(storage);
            Assert.AreEqual(0, store.Accounts.Count);
            Assert.IsNull(store.ActiveId);
        }

        [TestMethod]
        public void An_unreadable_storage_reads_as_an_empty_store()
        {
            var storage = new FakeAccountStorage();
            storage.ThrowOnLoad = true;
            var store = NewStore(storage);
            Assert.AreEqual(0, store.Accounts.Count);
        }

        [TestMethod]
        public void An_active_pointer_to_a_missing_account_means_no_active_account()
        {
            var storage = new FakeAccountStorage("{\"Accounts\":[],\"ActiveId\":\"gone\"}");
            var store = NewStore(storage);
            Assert.IsNull(store.ActiveId);
        }

        [TestMethod]
        public void Migrates_the_single_session_keys_into_the_first_account()
        {
            var store = NewStore();
            var a = store.MigrateFromLegacy("http://host:8096", "T", "u1", "s1");
            Assert.IsNotNull(a);
            Assert.AreEqual(1, store.Accounts.Count);
            Assert.AreEqual(a.Id, store.ActiveId);
            Assert.AreEqual("http://host:8096", a.BaseUrl);
            Assert.AreEqual("T", a.AccessToken);
            Assert.AreEqual("u1", a.UserId);
            // The name is unknown at migration time: no network call can happen here.
            Assert.IsNull(a.UserName);
        }

        [TestMethod]
        public void Migration_never_overwrites_existing_accounts()
        {
            var store = NewStore();
            store.AddOrUpdate("http://host:8096", "u1", "bob", "T", "s1", "NAS");
            var migrated = store.MigrateFromLegacy("http://old:8096", "OLD", "u9", "s9");
            Assert.IsNull(migrated);
            Assert.AreEqual(1, store.Accounts.Count);
            Assert.AreEqual("bob", store.Active.UserName);
        }

        [TestMethod]
        public void Migration_does_nothing_without_a_saved_session()
        {
            var store = NewStore();
            Assert.IsNull(store.MigrateFromLegacy(null, null, null, null));
            Assert.IsNull(store.MigrateFromLegacy("http://host:8096", null, "u1", "s1"));
            Assert.AreEqual(0, store.Accounts.Count);
        }
    }
}
