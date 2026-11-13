using System;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    // Read-only tests against a real Jellyfin server. Configure via
    // test.settings.local or the JELLYFIN_TEST_* environment variables; they report
    // Inconclusive when no server is configured and never mutate server state.
    [TestClass]
    public class LocalServerIntegrationTests
    {
        private TestServerSettings _settings;

        [TestInitialize]
        public void Setup()
        {
            _settings = TestServerSettings.Load();
        }

        private bool RequireConfigured()
        {
            if (_settings.IsConfigured) return true;
            Assert.Inconclusive("Jellyfin test server not configured (test.settings.local or JELLYFIN_TEST_* env vars).");
            return false;
        }

        private JellyfinApiClient MakeClient()
        {
            return new JellyfinApiClient(new HttpTransport(), new JellyfinUrls(_settings.Url), "integration-test");
        }

        private JellyfinApiClient AuthenticatedClient()
        {
            var client = MakeClient();
            client.AuthenticateAsync(_settings.User, _settings.Password).GetAwaiter().GetResult();
            return client;
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Server_is_reachable_and_identifies_itself()
        {
            if (!RequireConfigured()) return;
            var info = MakeClient().GetServerInfoAsync().GetAwaiter().GetResult();
            Assert.IsNotNull(info);
            Assert.IsFalse(string.IsNullOrEmpty(info.Id), "server Id should not be empty");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Login_with_configured_credentials_returns_a_token()
        {
            if (!RequireConfigured()) return;
            var client = MakeClient();
            try
            {
                client.AuthenticateAsync(_settings.User, _settings.Password).GetAwaiter().GetResult();
            }
            catch (JellyfinException ex)
            {
                Assert.Fail("Authentication failed (HTTP " + ex.StatusCode + "): " + ex.Message);
            }
            Assert.IsFalse(string.IsNullOrEmpty(client.AccessToken), "AccessToken");
            Assert.IsFalse(string.IsNullOrEmpty(client.UserId), "UserId");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Views_are_readable()
        {
            if (!RequireConfigured()) return;
            var client = AuthenticatedClient();
            var views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult();
            Assert.IsNotNull(views);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Items_load_for_the_first_library()
        {
            if (!RequireConfigured()) return;
            var client = AuthenticatedClient();
            var views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult();
            if (views == null || views.Count == 0)
            {
                Assert.Inconclusive("The server has no libraries to page through.");
                return;
            }
            var q = client.GetItemsAsync(client.UserId, views[0].Id, "Movie,Series,Episode,Video,BoxSet", 0, 5).GetAwaiter().GetResult();
            Assert.IsNotNull(q);
            Assert.IsNotNull(q.Items);
            Assert.IsTrue(q.TotalRecordCount >= 0);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Search_returns_results_for_an_existing_item_name()
        {
            if (!RequireConfigured()) return;
            var client = AuthenticatedClient();
            var views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult();

            BaseItemDto first = null;
            if (views != null)
            {
                foreach (var library in views)
                {
                    var q = client.GetItemsAsync(client.UserId, library.Id, "Movie,Series,Episode,Video,BoxSet", 0, 5).GetAwaiter().GetResult();
                    if (q != null && q.Items != null && q.Items.Count > 0)
                    {
                        first = q.Items[0];
                        break;
                    }
                }
            }

            if (first == null || string.IsNullOrEmpty(first.Name))
            {
                Assert.Inconclusive("No item available to search for.");
                return;
            }

            var results = client.SearchAsync(client.UserId, first.Name).GetAwaiter().GetResult();
            Assert.IsNotNull(results);
            Assert.IsNotNull(results.Items);
            Assert.IsTrue(results.Items.Count >= 1, "search for '" + first.Name + "' should return at least the item itself");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Playback_info_returns_a_playable_source_for_the_first_video()
        {
            if (!RequireConfigured()) return;
            var client = AuthenticatedClient();
            var views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult();

            BaseItemDto video = null;
            if (views != null)
            {
                foreach (var library in views)
                {
                    var q = client.GetItemsAsync(client.UserId, library.Id, "Movie,Episode,Video", 0, 10).GetAwaiter().GetResult();
                    if (q != null && q.Items != null && q.Items.Count > 0)
                    {
                        video = q.Items[0];
                        break;
                    }
                }
            }

            if (video == null)
            {
                Assert.Inconclusive("No video item available to test playback info.");
                return;
            }

            var info = client.GetPlaybackInfoAsync(client.UserId, video.Id, 0L).GetAwaiter().GetResult();
            Assert.IsNotNull(info);
            Assert.IsNotNull(info.MediaSources);
            Assert.IsTrue(info.MediaSources.Count >= 1, "expected at least one media source");
            // The server often returns only flags; PlaybackUrlResolver must still produce a URL.
            var source = info.MediaSources[0];
            var resolved = PlaybackUrlResolver.Resolve(_settings.Url, client.AccessToken, video.Id, source, 8000000);
            Assert.IsNotNull(resolved, "a playable URL should be resolvable for '" + video.Name + "'");
            Assert.IsFalse(string.IsNullOrEmpty(resolved.Url), "resolved URL should not be empty");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Image_url_with_the_modern_key_parameter_is_accepted()
        {
            // Jellyfin 12 rejects the legacy api_key parameter, and an image URL has no
            // header to fall back on. Fetching one proves the parameter the app builds
            // is the one the server accepts.
            if (!RequireConfigured()) return;
            var client = AuthenticatedClient();
            var views = client.GetViewsAsync(client.UserId).GetAwaiter().GetResult();
            if (views == null || views.Count == 0)
            {
                Assert.Inconclusive("The server has no libraries to read an image from.");
                return;
            }

            BaseItemDto item = null;
            foreach (var library in views)
            {
                var q = client.GetItemsAsync(client.UserId, library.Id, "Movie,Series,Episode,Video,BoxSet", 0, 20).GetAwaiter().GetResult();
                if (q != null && q.Items != null)
                {
                    foreach (var candidate in q.Items)
                    {
                        if (candidate.ImageTags != null && !string.IsNullOrEmpty(candidate.ImageTags.Primary))
                        {
                            item = candidate;
                            break;
                        }
                    }
                }
                if (item != null) break;
            }
            if (item == null)
            {
                Assert.Inconclusive("No item with a primary image on this server.");
                return;
            }

            string url = new JellyfinUrls(_settings.Url, client.AccessToken).PrimaryImage(item.Id, 120);
            Assert.IsTrue(url.Contains("ApiKey="), url);
            try
            {
                string body = new HttpTransport().GetStringAsync(url, null).GetAwaiter().GetResult();
                Assert.IsNotNull(body);
            }
            catch (JellyfinException ex)
            {
                Assert.Fail("Image URL rejected (HTTP " + ex.StatusCode + "): " + ex.Message);
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void Resume_list_is_readable()
        {
            if (!RequireConfigured()) return;
            var client = AuthenticatedClient();
            var list = client.GetResumeAsync(client.UserId, 5).GetAwaiter().GetResult();
            Assert.IsNotNull(list);
        }
    }
}
