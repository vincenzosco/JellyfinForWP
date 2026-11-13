using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class BrowseTests
    {
        private JellyfinApiClient MakeClient(FakeTransport fake)
        {
            var c = new JellyfinApiClient(fake, new JellyfinUrls("http://host:8096", "T"), "d");
            c.AccessToken = "T";
            c.UserId = "user1";
            return c;
        }

        [TestMethod]
        public void GetViews_parses_items_and_collection_type()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[{\"Id\":\"v1\",\"Name\":\"Movies\",\"CollectionType\":\"movies\"}],\"TotalRecordCount\":1}";
            var views = MakeClient(fake).GetViewsAsync("user1").Result;
            Assert.AreEqual(1, views.Count);
            Assert.AreEqual("movies", views[0].CollectionType);
        }

        [TestMethod]
        public void GetItems_passes_parent_and_paging()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[],\"TotalRecordCount\":0}";
            MakeClient(fake).GetItemsAsync("user1", "v1", "Movie", 100, 50).Wait();
            string url = fake.LastUrl;
            Assert.IsTrue(url.Contains("ParentId=v1"), url);
            Assert.IsTrue(url.Contains("StartIndex=100"), url);
            Assert.IsTrue(url.Contains("Limit=50"), url);
            Assert.IsTrue(url.Contains("Recursive=true"), url);
        }

        [TestMethod]
        public void GetItems_passes_the_requested_sort()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[],\"TotalRecordCount\":0}";
            MakeClient(fake).GetItemsAsync("user1", "v1", "Movie", 0, 50, "DateCreated", "Descending").Wait();
            Assert.IsTrue(fake.LastUrl.Contains("SortBy=DateCreated"), fake.LastUrl);
            Assert.IsTrue(fake.LastUrl.Contains("SortOrder=Descending"), fake.LastUrl);
        }

        [TestMethod]
        public void GetGenres_parses_the_genre_list()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[{\"Id\":\"g1\",\"Name\":\"Drama\"}],\"TotalRecordCount\":1}";
            var genres = MakeClient(fake).GetGenresAsync("user1", "view1").Result;
            Assert.AreEqual(1, genres.Count);
            Assert.AreEqual("Drama", genres[0].Name);
            Assert.IsTrue(fake.LastUrl.Contains("ParentId=view1"), fake.LastUrl);
        }

        [TestMethod]
        public void A_playlist_returns_its_entries_in_order()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[{\"Id\":\"a\",\"Name\":\"First\"},{\"Id\":\"b\",\"Name\":\"Second\"}],\"TotalRecordCount\":2}";
            var entries = MakeClient(fake).GetPlaylistItemsAsync("list1", "user1").Result;
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("First", entries[0].Name);
            Assert.AreEqual("Second", entries[1].Name);
        }

        [TestMethod]
        public void Search_encodes_term()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[],\"TotalRecordCount\":0}";
            MakeClient(fake).SearchAsync("user1", "the office").Wait();
            Assert.IsTrue(fake.LastUrl.Contains("SearchTerm=the%20office"), fake.LastUrl);
        }

        [TestMethod]
        public void GetItem_tolerates_missing_optional_fields()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Id\":\"i1\",\"Name\":\"Solo\"}";
            var item = MakeClient(fake).GetItemAsync("user1", "i1").Result;
            Assert.AreEqual("Solo", item.Name);
            Assert.IsNull(item.RunTimeTicks);
        }

        [TestMethod]
        public void Token_is_sent_in_the_standard_authorization_header()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[],\"TotalRecordCount\":0}";
            MakeClient(fake).GetViewsAsync("user1").Wait();
            Assert.IsTrue(fake.LastHeaders["Authorization"].Contains("Token=\"T\""), fake.LastHeaders["Authorization"]);
            Assert.IsFalse(fake.LastHeaders.ContainsKey("X-Emby-Token"), "the legacy header must not be sent: Jellyfin 12 rejects it");
        }

        [TestMethod]
        public void GetResume_returns_list()
        {
            var fake = new FakeTransport();
            fake.Handler = u => "{\"Items\":[{\"Id\":\"r1\",\"Name\":\"Resume me\"}],\"TotalRecordCount\":1}";
            var list = MakeClient(fake).GetResumeAsync("user1", 10).Result;
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("Resume me", list[0].Name);
        }
    }
}
