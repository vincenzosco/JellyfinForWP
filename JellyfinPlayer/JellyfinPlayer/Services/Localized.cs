using System;
using System.Globalization;
using System.Resources;
using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Services
{
    // Every user-visible string the code builds at run time. English (en-US) is the
    // neutral resource and the fallback; the device UI culture selects the it-IT
    // satellite when present. The strings live in Resources.resx / Resources.it-IT.resx.
    public static class Localized
    {
        private static readonly ResourceManager Manager = new ResourceManager("JellyfinPlayer.Resources", typeof(Localized).Assembly);

        public static string Text(string key)
        {
            var value = Manager.GetString(key, CultureInfo.CurrentUICulture);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Text(key), args);
        }

        // The user-facing sentence for a failed request, chosen from the
        // classification the shared layer attached to it (FailureText owns the
        // decision, so it is covered by the desktop tests). A 401 stays a
        // credentials message and is handled by the caller.
        public static string DescribeError(Exception ex)
        {
            return FailureText.Describe(ex, Text);
        }
    }
}
