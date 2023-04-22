namespace ScoopSearch.Indexer.Tests.Helpers;

public static class Disposable
{
    public static IDisposable Create(Action create, Action dispose)
    {
        return new Scope(create, dispose);
    }

    private class Scope : IDisposable
    {
        private readonly Action _dispose;

        public Scope(Action create, Action dispose)
        {
            _dispose = dispose;
            create();
        }

        public void Dispose()
        {
            _dispose();
        }
    }
}
