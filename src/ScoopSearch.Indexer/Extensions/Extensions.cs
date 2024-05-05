using System.Security.Cryptography;

namespace ScoopSearch.Indexer.Extensions;

internal static class Extensions
{
    public static void ForEach<T>(this IEnumerable<T> @this, Action<T> action)
    {
        foreach (var element in @this)
        {
            action(element);
        }
    }

    public static string Sha1Sum(this string @this)
    {
        var hash = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(@this));
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }
}
