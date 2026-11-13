using System;
using Microsoft.Phone.Controls;

namespace JellyfinPlayer.Services
{
    public static class NavigationService
    {
        // Set by a page that wants the hardware back button for itself before the
        // frame handles it - the player uses it to leave fullscreen first.
        // True means the page consumed the press.
        public static Func<bool> BackInterceptor;

        public static PhoneApplicationFrame RootFrame
        {
            get { return App.RootFrame as PhoneApplicationFrame; }
        }

        public static void Navigate(Type pageType, object parameter)
        {
            var f = RootFrame;
            if (f != null) f.Navigate(new Uri(NavigationUri(pageType, parameter), UriKind.Relative));
        }

        // Silverlight navigation only carries a query string, so a DTO is stashed
        // in the frame's NavigationContext-backed state instead of being serialised
        // into the URL. The pending parameter is consumed once, by the target page.
        private static object _pending;

        public static void SetPending(object parameter)
        {
            _pending = parameter;
        }

        public static object ConsumePending()
        {
            var value = _pending;
            _pending = null;
            return value;
        }

        // Navigation resolves the XAML by its path inside the assembly, so a page
        // in Pages/ is "/Pages/Name.xaml"; only the login page sits at the root.
        private static string NavigationUri(Type pageType, object parameter)
        {
            SetPending(parameter);
            string name = pageType.Name;
            if (pageType == typeof(MainPage)) return "/MainPage.xaml";
            return "/Pages/" + name + ".xaml";
        }

        public static void GoBack()
        {
            var f = RootFrame;
            if (f != null && f.CanGoBack) f.GoBack();
        }

        // A switch of account must not leave the previous user's screens behind
        // it: going back would walk into another account's library.
        public static void ClearBackStack()
        {
            var f = RootFrame;
            if (f != null)
            {
                while (f.RemoveBackEntry() != null)
                {
                }
            }
        }

        public static bool CanGoBack
        {
            get
            {
                var f = RootFrame;
                return f != null && f.CanGoBack;
            }
        }
    }
}
