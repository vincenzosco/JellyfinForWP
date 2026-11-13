using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class JsonTests
    {
        [TestMethod]
        public void Deserialize_server_info()
        {
            var dto = Json.Deserialize<ServerInfoDto>("{\"ServerName\":\"Home\",\"Version\":\"10.9.10\",\"Id\":\"abc\"}");
            Assert.AreEqual("Home", dto.ServerName);
            Assert.AreEqual("10.9.10", dto.Version);
            Assert.AreEqual("abc", dto.Id);
        }

        [TestMethod]
        public void Deserialize_query_result_empty()
        {
            var q = Json.Deserialize<QueryResultDto<BaseItemDto>>("{\"Items\":[],\"TotalRecordCount\":0}");
            Assert.IsNotNull(q.Items);
            Assert.AreEqual(0, q.Items.Count);
            Assert.AreEqual(0, q.TotalRecordCount);
        }

        [TestMethod]
        public void Serialize_progress_contains_ticks()
        {
            string s = Json.Serialize(new PlaybackProgressDto { ItemId = "i", PositionTicks = 5 });
            Assert.IsTrue(s.Contains("\"PositionTicks\":5"), s);
        }

        [TestMethod]
        public void Deserialize_ignores_unknown_fields()
        {
            var dto = Json.Deserialize<ServerInfoDto>("{\"ServerName\":\"H\",\"Whatever\":42}");
            Assert.AreEqual("H", dto.ServerName);
        }

        [TestMethod]
        public void Deserialize_image_tags_object()
        {
            var item = Json.Deserialize<BaseItemDto>("{\"Id\":\"x\",\"ImageTags\":{\"Primary\":\"t1\",\"Logo\":\"l1\"}}");
            Assert.AreEqual("t1", item.ImageTags.Primary);
        }
    }
}
