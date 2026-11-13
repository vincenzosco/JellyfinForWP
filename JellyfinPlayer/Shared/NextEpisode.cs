using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    // The episode that follows the one that just ended. Pure: the caller supplies
    // the series' episodes, so the ordering rule is tested on the desktop rather
    // than discovered on a phone after the credits.
    public static class NextEpisode
    {
        public static BaseItemDto Find(BaseItemDto current, IEnumerable<BaseItemDto> episodes)
        {
            if (current == null || episodes == null) return null;
            if (!current.ParentIndexNumber.HasValue || !current.IndexNumber.HasValue) return null;

            int season = current.ParentIndexNumber.Value;
            int number = current.IndexNumber.Value;
            BaseItemDto best = null;
            foreach (var candidate in episodes)
            {
                if (candidate == null || candidate.Id == current.Id) continue;
                if (!candidate.ParentIndexNumber.HasValue || !candidate.IndexNumber.HasValue) continue;
                int candidateSeason = candidate.ParentIndexNumber.Value;
                int candidateNumber = candidate.IndexNumber.Value;
                // Same season: only a later episode counts. Later season: anything.
                if (candidateSeason == season && candidateNumber <= number) continue;
                if (candidateSeason < season) continue;
                if (best == null || Before(candidate, best)) best = candidate;
            }
            return best;
        }

        private static bool Before(BaseItemDto left, BaseItemDto right)
        {
            if (left.ParentIndexNumber.Value != right.ParentIndexNumber.Value)
            {
                return left.ParentIndexNumber.Value < right.ParentIndexNumber.Value;
            }
            return left.IndexNumber.Value < right.IndexNumber.Value;
        }
    }
}
