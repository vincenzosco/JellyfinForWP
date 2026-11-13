using System;
using System.Text;

namespace JellyfinPlayer.Shared
{
    // Turns a non-Jellyfin answer into a short, classified error. Before this,
    // a server that answered with a web page put the whole page (or a raw
    // SerializationException message) in front of the user.
    public static class ResponseFailures
    {
        private const int MaxSnippetLength = 120;
        private const string NotJsonText = "The address answered with a web page instead of Jellyfin JSON. Check the address and the port.";

        // An HTML (or XML) body never starts with JSON; anything else is left to
        // the deserializer.
        public static bool LooksLikeHtml(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            return body.TrimStart().StartsWith("<", StringComparison.Ordinal);
        }

        // Never returns the response body when that body is a web page.
        public static string DescribeHttpFailure(int statusCode, string contentType, string body)
        {
            var message = new StringBuilder("The server answered HTTP " + statusCode.ToString() + ".");
            if (LooksLikeHtml(body) || (contentType != null && contentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                message.Append(" It returned a web page instead of the Jellyfin API - check the address and the port.");
            }
            else if (!string.IsNullOrEmpty(body))
            {
                message.Append(" ").Append(Snippet(body));
            }
            return message.ToString();
        }

        // Deserializes a response body, classifying anything that is not
        // Jellyfin JSON instead of letting SerializationException escape.
        public static T Parse<T>(string body)
        {
            if (LooksLikeHtml(body)) throw NotJson(null);
            try
            {
                return Json.Deserialize<T>(body);
            }
            catch (Exception ex)
            {
                throw NotJson(ex);
            }
        }

        private static JellyfinException NotJson(Exception inner)
        {
            return new JellyfinException(NotJsonText, 0, JellyfinErrorKind.NonJsonResponse, inner);
        }

        private static string Snippet(string body)
        {
            string collapsed = Collapse(body);
            if (collapsed.Length <= MaxSnippetLength) return collapsed;
            return collapsed.Substring(0, MaxSnippetLength) + "...";
        }

        private static string Collapse(string body)
        {
            var sb = new StringBuilder();
            bool previousWasSpace = false;
            foreach (char c in body)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!previousWasSpace) sb.Append(' ');
                    previousWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    previousWasSpace = false;
                }
            }
            return sb.ToString().Trim();
        }
    }
}
