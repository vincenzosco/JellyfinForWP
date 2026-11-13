namespace JellyfinPlayer.Shared
{
    // Keeps AccountStore free of the phone APIs: the app binds this to isolated
    // storage, the tests to memory. Same pattern as IHttpTransport.
    public interface IAccountStorage
    {
        string Load();
        void Save(string json);
    }
}
