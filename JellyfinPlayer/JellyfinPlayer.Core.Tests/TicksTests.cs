using System;
using JellyfinPlayer.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JellyfinPlayer.Core.Tests
{
    [TestClass]
    public class TicksTests
    {
        [TestMethod]
        public void ToTimeSpan_one_second()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(1), Ticks.ToTimeSpan(10000000));
        }

        [TestMethod]
        public void Round_trip_preserves_ticks()
        {
            Assert.AreEqual(12345L, Ticks.FromTimeSpan(Ticks.ToTimeSpan(12345L)));
        }

        [TestMethod]
        public void Format_null_is_empty()
        {
            Assert.AreEqual("", Ticks.Format(null));
        }

        [TestMethod]
        public void Format_minutes_seconds()
        {
            Assert.AreEqual("1:30", Ticks.Format(900000000L));
        }

        [TestMethod]
        public void Format_hours_minutes_seconds()
        {
            Assert.AreEqual("1:00:00", Ticks.Format(36000000000L));
        }

        [TestMethod]
        public void FormatDuration_uses_same_rules()
        {
            Assert.AreEqual("2:05", Ticks.FormatDuration(1250000000L));
        }

        [TestMethod]
        public void FormatSpan_minutes_seconds()
        {
            Assert.AreEqual("2:05", Ticks.FormatSpan(TimeSpan.FromSeconds(125)));
        }

        [TestMethod]
        public void FormatSpan_hours_minutes_seconds()
        {
            Assert.AreEqual("1:00:03", Ticks.FormatSpan(TimeSpan.FromSeconds(3603)));
        }

        [TestMethod]
        public void FormatSpan_negative_is_zero()
        {
            Assert.AreEqual("0:00", Ticks.FormatSpan(TimeSpan.FromSeconds(-5)));
        }
    }
}
