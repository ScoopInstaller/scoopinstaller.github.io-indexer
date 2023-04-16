using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ScoopSearch.Functions;

public static class Extensions
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
}
