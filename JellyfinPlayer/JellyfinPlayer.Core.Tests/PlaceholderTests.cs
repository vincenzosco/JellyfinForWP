using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class PlaceholderTests
    {
        [TestMethod]
        public void Add_sums_two_integers()
        {
            Assert.AreEqual(5, JellyfinPlayer.Shared.Placeholder.Add(2, 3));
        }
    }
}
