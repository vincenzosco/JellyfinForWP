using JellyfinPlayer.Shared;

namespace JellyfinPlayer.Core.Tests
{
    // An in-memory IAccountStorage: isolated storage cannot be reached from a
    // desktop test.
    public class FakeAccountStorage : IAccountStorage
    {
        private string _saved;

        public FakeAccountStorage(string initial = null)
        {
            _saved = initial;
        }

        public string Saved
        {
            get { return _saved; }
        }

        public bool ThrowOnLoad { get; set; }
        public bool ThrowOnSave { get; set; }

        public string Load()
        {
            if (ThrowOnLoad) throw new System.Exception("storage unavailable");
            return _saved;
        }

        public void Save(string json)
        {
            if (ThrowOnSave) throw new System.Exception("storage full");
            _saved = json;
        }
    }
}
