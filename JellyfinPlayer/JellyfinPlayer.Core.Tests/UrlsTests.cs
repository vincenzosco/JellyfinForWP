using System.Linq;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class UrlsTests
    {
        private JellyfinUrls MakeUrls()
        {
            return new JellyfinUrls("http://host:8096/", "TOK");
        }

        [TestMethod]
        public void Base_url_is_normalized()
        {
            Assert.AreEqual("http://host:8096", MakeUrls().BaseUrl);
        }

        [TestMethod]
        public void System_info_public_url()
        {
            Assert.AreEqual("http://host:8096/System/Info/Public", MakeUrls().SystemInfoPublic());
        }

        [TestMethod]
        public void Primary_image_url_has_api_key()
        {
            Assert.AreEqual("http://host:8096/Items/id1/Images/Primary?maxHeight=300&quality=90&ApiKey=TOK", MakeUrls().PrimaryImage("id1", 300));
        }

        [TestMethod]
        public void Items_url_has_paging_and_key()
        {
            string u = MakeUrls().Items("user1", "v1", "Movie", 0, 50, "SortName", "Ascending");
            Assert.IsTrue(u.Contains("Recursive=true"), u);
            Assert.IsTrue(u.Contains("ParentId=v1"), u);
            Assert.IsTrue(u.Contains("StartIndex=0"), u);
            Assert.IsTrue(u.Contains("Limit=50"), u);
            Assert.IsTrue(u.Contains("ApiKey=TOK"), u);
        }

        [TestMethod]
        public void Search_encodes_term()
        {
            string u = MakeUrls().Search("user1", "the office");
            Assert.IsTrue(u.Contains("SearchTerm=the%20office"), u);
        }

        [TestMethod]
        public void Device_profile_uses_progressive_mp4()
        {
            var dp = DeviceProfileBuilder.DefaultProfile();
            Assert.AreEqual("http", dp.TranscodingProfiles[0].Protocol);
            Assert.AreEqual("mp4", dp.TranscodingProfiles[0].Container);
        }

        [TestMethod]
        public void Image_url_uses_the_parameter_jellyfin_12_accepts()
        {
            // 12.0 disabled the legacy api_key query parameter along with the X-Emby-*
            // headers, and an image URL cannot carry an Authorization header.
            string u = MakeUrls().PrimaryImage("id1", 300);
            Assert.IsTrue(u.Contains("ApiKey=TOK"), u);
            Assert.IsFalse(u.Contains("api_key="), "the removed api_key parameter must not be sent: " + u);
        }

        [TestMethod]
        public void Device_profile_does_not_claim_mkv()
        {
            // Windows Phone Silverlight plays MP4/H.264; claiming Matroska would make
            // the server choose a direct stream the phone cannot open.
            var dp = DeviceProfileBuilder.DefaultProfile();
            string container = dp.DirectPlayProfiles[0].Container;
            Assert.AreEqual("mp4", container);
            Assert.IsFalse(container.Contains("mkv"), container);
        }

        [TestMethod]
        public void Playback_request_carries_user_and_profile()
        {
            var r = DeviceProfileBuilder.ToRequest("u1", 0L);
            Assert.AreEqual("u1", r.UserId);
            Assert.AreEqual(8000000, r.MaxStreamingBitrate);
            Assert.IsTrue(r.AutoOpenLiveStream);
            Assert.IsNotNull(r.DeviceProfile);
        }

        [TestMethod]
        public void Transcode_url_carries_the_chosen_tracks()
        {
            string u = MakeUrls().VideoTranscode("i1", "m1", 8000000, 2, 3);
            Assert.IsTrue(u.Contains("AudioStreamIndex=2"), u);
            Assert.IsTrue(u.Contains("SubtitleStreamIndex=3"), u);
            Assert.IsTrue(u.Contains("SubtitleMethod=Encode"), u);
        }

        [TestMethod]
        public void Transcode_url_without_tracks_has_no_track_parameters()
        {
            string u = MakeUrls().VideoTranscode("i1", "m1", 8000000);
            Assert.IsFalse(u.Contains("AudioStreamIndex"), u);
            Assert.IsFalse(u.Contains("SubtitleStreamIndex"), u);
        }

        [TestMethod]
        public void Playback_request_carries_the_chosen_tracks()
        {
            var r = DeviceProfileBuilder.ToRequest("u1", 0L, 8000000, 2, 3);
            Assert.AreEqual(2, r.AudioStreamIndex.Value);
            Assert.AreEqual(3, r.SubtitleStreamIndex.Value);
        }

        [TestMethod]
        public void Subtitle_profiles_cover_image_subtitles()
        {
            var formats = DeviceProfileBuilder.DefaultProfile().SubtitleProfiles.Select(p => p.Format).ToList();
            Assert.IsTrue(formats.Contains("pgssub"), "PGS subtitles must be offered for burn-in");
            Assert.IsTrue(formats.Contains("srt"), "text subtitles must stay offered");
        }

        [TestMethod]
        public void Genres_url_is_scoped_to_the_library()
        {
            string u = MakeUrls().Genres("user1", "view1");
            Assert.IsTrue(u.Contains("/Genres?"), u);
            Assert.IsTrue(u.Contains("UserId=user1"), u);
            Assert.IsTrue(u.Contains("ParentId=view1"), u);
        }

        [TestMethod]
        public void Items_url_carries_the_genre_filter()
        {
            string u = MakeUrls().Items("user1", "view1", "Movie", 0, 50, "SortName", "Ascending", "genre1");
            Assert.IsTrue(u.Contains("GenreIds=genre1"), u);
        }

        [TestMethod]
        public void Playlist_urls_name_the_user_and_the_playlist()
        {
            Assert.IsTrue(MakeUrls().Playlists("user1").Contains("IncludeItemTypes=Playlist"), MakeUrls().Playlists("user1"));
            string entries = MakeUrls().PlaylistItems("list1", "user1");
            Assert.IsTrue(entries.Contains("/Playlists/list1/Items"), entries);
            Assert.IsTrue(entries.Contains("UserId=user1"), entries);
        }

        [TestMethod]
        public void Missing_token_omits_api_key()
        {
            Assert.IsFalse(new JellyfinUrls("http://h").PrimaryImage("i", 100).Contains("ApiKey"), "no token should mean no ApiKey");
        }

        [TestMethod]
        public void User_url_carries_the_api_key()
        {
            string u = MakeUrls().User("u1");
            Assert.IsTrue(u.Contains("/Users/u1"), u);
            Assert.IsTrue(u.Contains("ApiKey=TOK"), u);
        }
    }
}
