namespace ScoopSearch.Indexer.Extensions;

public static class Extensions
{
    public static void ForEach<T>(this IEnumerable<T> @this, Action<T> action)
    {
        foreach (var element in @this)
        {
            action(element);
        }
    }
}
