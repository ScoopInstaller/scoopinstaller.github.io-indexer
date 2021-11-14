using System;
using System.Collections.Generic;

namespace ScoopSearch.Functions
{
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
}
