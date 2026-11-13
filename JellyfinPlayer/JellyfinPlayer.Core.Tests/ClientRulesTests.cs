using System.Collections.Generic;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    // The rules behind the queue, the genre panel, the quality list and the download
    // store. They are page-free, so a device is not needed to know they are right.
    [TestClass]
    public class ClientRulesTests
    {
        private static BaseItemDto Item(string id)
        {
            return new BaseItemDto { Id = id, Name = id };
        }

        [TestMethod]
        public void PlayQueue_moves_to_the_next_entry()
        {
            var items = new List<BaseItemDto> { Item("a"), Item("b"), Item("c") };
            Assert.AreEqual(1, PlayQueue.NextIndex(items, 0));
            Assert.AreEqual(2, PlayQueue.NextIndex(items, 1));
            Assert.AreEqual("b", PlayQueue.At(items, 1).Id);
        }

        [TestMethod]
        public void PlayQueue_stops_after_the_last_entry()
        {
            var items = new List<BaseItemDto> { Item("a") };
            Assert.AreEqual(-1, PlayQueue.NextIndex(items, 0));
            Assert.AreEqual(-1, PlayQueue.NextIndex(null, 0));
            Assert.IsNull(PlayQueue.At(items, 5));
            Assert.IsNull(PlayQueue.At(null, 0));
        }

        [TestMethod]
        public void GenreChoices_start_with_all_and_skip_broken_rows()
        {
            var genres = new List<BaseItemDto>
            {
                new BaseItemDto { Id = "g1", Name = "Drama" },
                new BaseItemDto { Id = "", Name = "No id" },
                null,
                new BaseItemDto { Id = "g2", Name = null }
            };
            var choices = LibraryFilters.GenreChoices(genres, "All genres");
            Assert.AreEqual(3, choices.Count);
            Assert.IsTrue(choices[0].IsAll, "the first row must mean no filter");
            Assert.IsNull(choices[0].Id);
            Assert.AreEqual("Drama", choices[1].Name);
            // A nameless genre still needs something to show.
            Assert.AreEqual("g2", choices[2].Name);
        }

        [TestMethod]
        public void GenreChoices_work_without_a_genre_list()
        {
            var choices = LibraryFilters.GenreChoices(null, "All");
            Assert.AreEqual(1, choices.Count);
            Assert.IsTrue(choices[0].IsAll);
        }

        [TestMethod]
        public void QualityChoices_are_the_stored_bitrates()
        {
            var choices = QualityOptions.Choices("Mbps");
            Assert.AreEqual(4, choices.Count);
            Assert.AreEqual(8000000, choices[2].Bitrate);
            Assert.AreEqual("8 Mbps", choices[2].Label);
            Assert.AreEqual(2, QualityOptions.IndexOf(8000000));
            Assert.AreEqual(-1, QualityOptions.IndexOf(123));
        }

        [TestMethod]
        public void Downloads_use_a_safe_file_name()
        {
            Assert.AreEqual("abc-123_x.mp4", Downloads.FileNameFor("abc-123_x"));
            Assert.AreEqual("abcmp4.mp4", Downloads.FileNameFor("a/b c.mp4"));
            // Nothing usable must still be a file, not a bare extension.
            Assert.AreEqual("download.mp4", Downloads.FileNameFor("../.."));
            Assert.AreEqual("download.mp4", Downloads.FileNameFor(null));
        }

        [TestMethod]
        public void Download_percent_never_leaves_zero_to_a_hundred()
        {
            Assert.AreEqual(0, Downloads.Percent(0, 0));
            Assert.AreEqual(50, Downloads.Percent(5, 10));
            Assert.AreEqual(100, Downloads.Percent(11, 10));
            Assert.AreEqual(0, Downloads.Percent(-1, 10));
        }

        [TestMethod]
        public void Download_size_is_readable()
        {
            Assert.AreEqual("1 MB", Downloads.SizeLabel(0));
            Assert.AreEqual("10 MB", Downloads.SizeLabel(10L * 1024 * 1024));
            Assert.AreEqual("1.5 GB", Downloads.SizeLabel((long)(1.5 * 1024 * 1024 * 1024)));
        }

        [TestMethod]
        public void A_download_record_survives_json()
        {
            var records = new List<DownloadRecordDto>
            {
                new DownloadRecordDto { ItemId = "i1", Name = "Film", Subtitle = "2020",
                                        FileName = "i1.mp4", RunTimeTicks = 100L, PositionTicks = 50L,
                                        SizeBytes = 2048, DownloadedAtTicks = 7L }
            };
            string text = Json.Serialize(records);
            var copy = Json.Deserialize<List<DownloadRecordDto>>(text);
            Assert.AreEqual(1, copy.Count);
            Assert.AreEqual("Film", copy[0].Name);
            Assert.AreEqual(50L, copy[0].PositionTicks.Value);
            Assert.AreEqual(2048L, copy[0].SizeBytes);
        }
    }
}
