using System;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    public static class ImageHelper
    {
        // BitmapImage cannot set HTTP headers, so image URLs carry ?ApiKey= (the
        // parameter Jellyfin 12 accepts; the old api_key spelling is rejected).
        public static Uri ImageUrl(string baseUrl, string token, string itemId, int maxHeight)
        {
            return new Uri(new JellyfinUrls(baseUrl, token).PrimaryImage(itemId, maxHeight));
        }

        public static Uri BackdropUrl(string baseUrl, string token, string itemId, int maxWidth)
        {
            return new Uri(new JellyfinUrls(baseUrl, token).BackdropImage(itemId, maxWidth));
        }

        public static bool HasPrimaryImage(BaseItemDto item)
        {
            return item != null && item.ImageTags != null && !string.IsNullOrEmpty(item.ImageTags.Primary);
        }

        public static bool HasBackdropImage(BaseItemDto item)
        {
            return item != null && item.BackdropImageTags != null && item.BackdropImageTags.Count > 0;
        }
    }
}
