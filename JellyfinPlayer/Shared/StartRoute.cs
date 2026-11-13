namespace JellyfinPlayer.Shared
{
    // Where the app opens. A saved login goes straight to the library: the token
    // identifies the user, so asking for the password on every launch would throw
    // away the session that was stored. When the token is no longer accepted, Home
    // is the screen that notices the 401 and asks for the password again with the
    // account already filled in.
    public static class StartRoute
    {
        public const string Login = "/MainPage.xaml";
        public const string Library = "/Pages/HomePage.xaml";

        public static string For(bool loggedIn)
        {
            return loggedIn ? Library : Login;
        }
    }
}
