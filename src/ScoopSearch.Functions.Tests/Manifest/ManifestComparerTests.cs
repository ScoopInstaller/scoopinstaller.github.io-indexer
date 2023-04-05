using FluentAssertions;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Manifest;
using ScoopSearch.Functions.Tests.Helpers;

namespace ScoopSearch.Functions.Tests.Manifest;

public class ManifestComparerTests
{
    [Theory]
    [MemberData(nameof(GetManifestsInfoEqualsTestCases))]
    public void Equals_Succeeds(ManifestInfo? manifestInfo1, ManifestInfo? manifestInfo2, bool expectedIdEquals, bool expectedExactEquals)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;

        // Arrange + Act
        var resultIdEquals = manifestIdComparer.Equals(manifestInfo1, manifestInfo2);
        var resultExactEquals = manifestExactComparer.Equals(manifestInfo1, manifestInfo2);

        // Assert
        resultIdEquals.Should().Be(expectedIdEquals);
        resultExactEquals.Should().Be(expectedExactEquals);
    }

    public static IEnumerable<object?[]> GetManifestsInfoEqualsTestCases()
    {
        // manifestInfo1, manifestInfo2, expectedIdEquals, expectedExactEquals
        yield return new object?[] { null, null, true, true };
        yield return new object?[] { null, ("foo", "sha", 123).ToManifestInfo(), false, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), null, false, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), true, true };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("foo", "sha", 321).ToManifestInfo(), true, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("foo", "ahs", 123).ToManifestInfo(), true, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("foo", "ahs", 321).ToManifestInfo(), true, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("bar", "sha", 123).ToManifestInfo(), false, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("bar", "sha", 321).ToManifestInfo(), false, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("bar", "ahs", 123).ToManifestInfo(), false, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("bar", "ahs", 321).ToManifestInfo(), false, false };
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), true, true };
        yield return new object?[] { ("foo", "sha", 321).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), true, false };
        yield return new object?[] { ("foo", "ahs", 123).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), true, false };
        yield return new object?[] { ("foo", "ahs", 321).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), true, false };
        yield return new object?[] { ("bar", "sha", 123).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), false, false };
        yield return new object?[] { ("bar", "sha", 321).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), false, false };
        yield return new object?[] { ("bar", "ahs", 123).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), false, false };
        yield return new object?[] { ("bar", "ahs", 321).ToManifestInfo(), ("foo", "sha", 123).ToManifestInfo(), false, false };
    }

    [Theory]
    [MemberData(nameof(GetManifestsInfoGetHashCodeTestCases))]
    public void GetHashCode_Succeeds(ManifestInfo manifestInfo, int expectedIdHashCode, int expectedExactHashCode)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;

        // Arrange + Act
        var resultIdHashCode = manifestIdComparer.GetHashCode(manifestInfo);
        var resultExactHashCode = manifestExactComparer.GetHashCode(manifestInfo);

        // Assert
        resultIdHashCode.Should().Be(expectedIdHashCode);
        resultExactHashCode.Should().Be(expectedExactHashCode);
    }

    public static IEnumerable<object?[]> GetManifestsInfoGetHashCodeTestCases()
    {
        // manifestInfo, idHashCode, exactHashCode
        yield return new object?[] { ("foo", "sha", 123).ToManifestInfo(), "foo".GetHashCode(), HashCode.Combine("foo", "sha", 123) };
        yield return new object?[] { ("foo", "sha", 321).ToManifestInfo(), "foo".GetHashCode(), HashCode.Combine("foo", "sha", 321) };
        yield return new object?[] { ("foo", "ahs", 123).ToManifestInfo(), "foo".GetHashCode(), HashCode.Combine("foo", "ahs", 123) };
        yield return new object?[] { ("foo", "ahs", 321).ToManifestInfo(), "foo".GetHashCode(), HashCode.Combine("foo", "ahs", 321) };
        yield return new object?[] { ("bar", "sha", 123).ToManifestInfo(), "bar".GetHashCode(), HashCode.Combine("bar", "sha", 123) };
        yield return new object?[] { ("bar", "sha", 321).ToManifestInfo(), "bar".GetHashCode(), HashCode.Combine("bar", "sha", 321) };
        yield return new object?[] { ("bar", "ahs", 123).ToManifestInfo(), "bar".GetHashCode(), HashCode.Combine("bar", "ahs", 123) };
        yield return new object?[] { ("bar", "ahs", 321).ToManifestInfo(), "bar".GetHashCode(), HashCode.Combine("bar", "ahs", 321) };
    }
}
