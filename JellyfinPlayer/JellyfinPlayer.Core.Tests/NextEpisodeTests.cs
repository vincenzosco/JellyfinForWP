using System.Collections.Generic;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class NextEpisodeTests
    {
        private BaseItemDto Episode(string id, int? season, int? number)
        {
            return new BaseItemDto { Id = id, Type = "Episode", ParentIndexNumber = season, IndexNumber = number };
        }

        [TestMethod]
        public void Picks_the_next_episode_in_the_same_season()
        {
            var current = Episode("e1", 1, 1);
            var episodes = new List<BaseItemDto>
            {
                Episode("e3", 1, 3), current, Episode("e2", 1, 2)
            };
            Assert.AreEqual("e2", NextEpisode.Find(current, episodes).Id);
        }

        [TestMethod]
        public void Jumps_to_the_first_episode_of_the_next_season()
        {
            var current = Episode("e4", 1, 4);
            var episodes = new List<BaseItemDto>
            {
                current, Episode("s2e2", 2, 2), Episode("s2e1", 2, 1)
            };
            Assert.AreEqual("s2e1", NextEpisode.Find(current, episodes).Id);
        }

        [TestMethod]
        public void Returns_nothing_after_the_last_episode()
        {
            var current = Episode("e4", 1, 4);
            Assert.IsNull(NextEpisode.Find(current, new List<BaseItemDto> { current }));
        }

        [TestMethod]
        public void Returns_nothing_without_episode_numbers()
        {
            var current = Episode("e1", null, null);
            var episodes = new List<BaseItemDto> { Episode("e2", 1, 2) };
            Assert.IsNull(NextEpisode.Find(current, episodes));
        }

        [TestMethod]
        public void Ignores_specials_and_items_before_the_current_one()
        {
            var current = Episode("e2", 1, 2);
            var episodes = new List<BaseItemDto>
            {
                Episode("special", 0, 1), Episode("e1", 1, 1), Episode("e4", 1, 4)
            };
            Assert.AreEqual("e4", NextEpisode.Find(current, episodes).Id);
        }

        [TestMethod]
        public void Nothing_in_nothing_out()
        {
            Assert.IsNull(NextEpisode.Find(null, new List<BaseItemDto>()));
            Assert.IsNull(NextEpisode.Find(Episode("e1", 1, 1), null));
        }
    }
}
