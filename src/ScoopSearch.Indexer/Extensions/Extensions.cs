using System.Security.Cryptography;
using System.Text.Json;

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
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(@this));
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    public static T? Deserialize<T>(this Task<string> @this)
    {
        if (@this.IsCompletedSuccessfully)
        {
            return JsonSerializer.Deserialize<T>(@this.Result);
        }

        return default;
    }
}
