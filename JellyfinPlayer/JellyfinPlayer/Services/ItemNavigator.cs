using System;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    public static class ItemNavigator
    {
        public static void Open(BaseItemDto item)
        {
            if (item == null) return;
            if (item.Type == "Series")
            {
                NavigationService.Navigate(typeof(SeriesPage), item);
            }
            else if (item.Type == "Playlist")
            {
                NavigationService.Navigate(typeof(PlaylistPage), item);
            }
            else
            {
                NavigationService.Navigate(typeof(ItemDetailPage), item);
            }
        }
    }
}
