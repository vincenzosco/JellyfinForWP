using System;

namespace JellyfinPlayer.Shared
{
    public class JellyfinException : Exception
    {
        private readonly int _statusCode;
        private readonly JellyfinErrorKind _kind;

        public int StatusCode
        {
            get { return _statusCode; }
        }

        // Why the request failed, so the app can pick a localized sentence
        // without inspecting the message body.
        public JellyfinErrorKind Kind
        {
            get { return _kind; }
        }

        public JellyfinException(string message, int statusCode,
                                 JellyfinErrorKind kind = JellyfinErrorKind.Unknown,
                                 Exception innerException = null)
            : base(message, innerException)
        {
            _statusCode = statusCode;
            _kind = kind;
        }
    }
}
