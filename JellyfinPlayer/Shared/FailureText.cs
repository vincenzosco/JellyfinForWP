using System;

namespace JellyfinPlayer.Shared
{
    // Turns a failure into the sentence shown to the user. The lookup resolves a
    // resource key, so the decision lives here (testable without a device) while
    // the text lives in Resources.resx.
    public static class FailureText
    {
        // The saved token of this account is no longer accepted: the caller should
        // ask for the password again instead of printing an HTTP failure. The
        // status is the whole signal - requiring the error kind as well would tie
        // the re-login path to every throw site remembering to classify itself.
        public static bool IsUnauthorized(Exception ex)
        {
            var failure = ex as JellyfinException;
            if (failure == null) return false;
            return failure.StatusCode == 401 || failure.StatusCode == 403;
        }

        public static string Describe(Exception ex, Func<string, string> lookup)
        {
            var failure = ex as JellyfinException;
            if (failure == null) return string.Format(lookup("ErrorGeneric"), ex.Message);
            switch (failure.Kind)
            {
                case JellyfinErrorKind.HttpFailure:
                    return string.Format(lookup("ErrorHttp"), failure.StatusCode);
                case JellyfinErrorKind.NonJsonResponse:
                    return lookup("ErrorNotJson");
                case JellyfinErrorKind.Timeout:
                    return lookup("ErrorTimeout");
                case JellyfinErrorKind.Unreachable:
                    return lookup("ErrorUnreachable");
                default:
                    // Already user-facing text (a playback limitation, say): wrapping
                    // it as "unexpected error" would double the sentence.
                    return failure.Message;
            }
        }
    }
}
