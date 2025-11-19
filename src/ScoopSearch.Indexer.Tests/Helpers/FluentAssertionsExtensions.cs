using System.Linq.Expressions;
using Moq;

namespace ScoopSearch.Indexer.Tests.Helpers;

public static class FluentExtensions
{
    public static TValue Matcher<TValue>(Action<TValue> action)
    {
        return Match.Create(
            (Predicate<TValue>)(actual => Matcher(action, actual)),
            (Expression<Func<TValue>>)(() => Matcher(action)));
    }

    private static bool Matcher<TValue>(Action<TValue> action, TValue actual)
    {
        try
        {
            action(actual);
            return true;
        }
        catch (Exception ex)
        {
            TestContext.Current.SendDiagnosticMessage("Actual and expected of type {0} are not equal. Details:", typeof(TValue));
            TestContext.Current.SendDiagnosticMessage(ex.ToString());
            return false;
        }
    }
}
