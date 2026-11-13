using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    // What the player is asked to play: one item, plus the playlist it came from so
    // the entries after it continue without a trip back to the playlist screen.
    public class PlayRequest
    {
        public BaseItemDto Item { get; set; }
        public List<BaseItemDto> Queue { get; set; }
        public int Index { get; set; }
    }

    // A playlist plays as a queue: the entry that was tapped, then whatever follows
    // in the playlist. The player asks this class what comes next, so the rule is
    // covered on the desktop instead of only on a device.
    public static class PlayQueue
    {
        public static BaseItemDto At(IList<BaseItemDto> items, int index)
        {
            if (items == null || index < 0 || index >= items.Count) return null;
            return items[index];
        }

        // -1 means "this was the last entry"; the caller then falls back to its
        // own continuation rule (an episode's next episode).
        public static int NextIndex(IList<BaseItemDto> items, int currentIndex)
        {
            if (items == null) return -1;
            int candidate = currentIndex + 1;
            if (candidate < 0 || candidate >= items.Count) return -1;
            return candidate;
        }
    }
}
