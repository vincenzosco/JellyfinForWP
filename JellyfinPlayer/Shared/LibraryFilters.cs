using System.Collections.Generic;

namespace JellyfinPlayer.Shared
{
    // One row of the genre panel. The first row is always "all genres", which is
    // no filter at all - its Id is null.
    public class GenreChoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsAll { get; set; }
    }

    public static class LibraryFilters
    {
        public static List<GenreChoice> GenreChoices(List<BaseItemDto> genres, string allLabel)
        {
            var choices = new List<GenreChoice>();
            choices.Add(new GenreChoice { Name = allLabel, IsAll = true });
            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    // A genre without an id cannot be filtered on, and a nameless
                    // one would show an empty row.
                    if (genre == null || string.IsNullOrEmpty(genre.Id)) continue;
                    choices.Add(new GenreChoice
                    {
                        Id = genre.Id,
                        Name = string.IsNullOrEmpty(genre.Name) ? genre.Id : genre.Name
                    });
                }
            }
            return choices;
        }
    }
}
