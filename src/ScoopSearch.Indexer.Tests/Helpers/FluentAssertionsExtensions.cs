using System.Linq.Expressions;
using Moq;

namespace ScoopSearch.Indexer.Tests.Helpers;

public static class FluentExtensions
{
    public static TValue Matcher<TValue>(Action<TValue> action, ITestOutputHelper testOutputHelper)
    {
        return Match.Create(
            (Predicate<TValue>)(actual => Matcher(action, actual, testOutputHelper)),
            (Expression<Func<TValue>>)(() => Matcher(action, testOutputHelper)));
    }

    private static bool Matcher<TValue>(Action<TValue> action, TValue actual, ITestOutputHelper testOutputHelper)
    {
        try
        {
            action(actual);
            return true;
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteLine("Actual and expected of type {0} are not equal. Details:", typeof(TValue));
            testOutputHelper.WriteLine(ex.ToString());
            return false;
        }
    }
}
