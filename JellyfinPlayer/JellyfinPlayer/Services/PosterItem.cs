using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // View model for the lists; keeps XAML binding simple (no converters).
    public class PosterItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ImageSource Poster { get; set; }
        public BaseItemDto Raw { get; set; }

        public static List<PosterItem> Build(IList<BaseItemDto> items, string baseUrl, string token)
        {
            var result = new List<PosterItem>();
            if (items == null) return result;
            foreach (var item in items)
            {
                result.Add(One(item, baseUrl, token));
            }
            return result;
        }

        public static PosterItem One(BaseItemDto item, string baseUrl, string token)
        {
            var p = new PosterItem();
            p.Id = item.Id;
            p.Raw = item;
            p.Title = item.Name;
            p.Subtitle = SubtitleFor(item);
            if (ImageHelper.HasPrimaryImage(item))
            {
                p.Poster = new BitmapImage(ImageHelper.ImageUrl(baseUrl, token, item.Id, 300));
            }
            return p;
        }

        private static string SubtitleFor(BaseItemDto item)
        {
            if (item.Type == "Episode")
            {
                string number = "";
                if (item.ParentIndexNumber.HasValue) number += "S" + item.ParentIndexNumber.Value.ToString("00");
                if (item.IndexNumber.HasValue) number += "E" + item.IndexNumber.Value.ToString("00");
                if (!string.IsNullOrEmpty(item.SeriesName)) return item.SeriesName + " " + number;
                return number;
            }
            if (item.ProductionYear.HasValue) return item.ProductionYear.Value.ToString();
            return "";
        }
    }
}
