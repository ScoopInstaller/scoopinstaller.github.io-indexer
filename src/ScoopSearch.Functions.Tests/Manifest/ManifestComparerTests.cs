using FluentAssertions;
using Newtonsoft.Json;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Manifest;

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
        yield return new object?[] { null, Create("foo", "sha", 123), false, false };
        yield return new object?[] { Create("foo", "sha", 123), null, false, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("foo", "sha", 123), true, true };
        yield return new object?[] { Create("foo", "sha", 123), Create("foo", "sha", 321), true, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("foo", "ahs", 123), true, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("foo", "ahs", 321), true, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("bar", "sha", 123), false, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("bar", "sha", 321), false, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("bar", "ahs", 123), false, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("bar", "ahs", 321), false, false };
        yield return new object?[] { Create("foo", "sha", 123), Create("foo", "sha", 123), true, true };
        yield return new object?[] { Create("foo", "sha", 321), Create("foo", "sha", 123), true, false };
        yield return new object?[] { Create("foo", "ahs", 123), Create("foo", "sha", 123), true, false };
        yield return new object?[] { Create("foo", "ahs", 321), Create("foo", "sha", 123), true, false };
        yield return new object?[] { Create("bar", "sha", 123), Create("foo", "sha", 123), false, false };
        yield return new object?[] { Create("bar", "sha", 321), Create("foo", "sha", 123), false, false };
        yield return new object?[] { Create("bar", "ahs", 123), Create("foo", "sha", 123), false, false };
        yield return new object?[] { Create("bar", "ahs", 321), Create("foo", "sha", 123), false, false };
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
        yield return new object?[] { Create("foo", "sha", 123), "foo".GetHashCode(), HashCode.Combine("foo", "sha", 123) };
        yield return new object?[] { Create("foo", "sha", 321), "foo".GetHashCode(), HashCode.Combine("foo", "sha", 321) };
        yield return new object?[] { Create("foo", "ahs", 123), "foo".GetHashCode(), HashCode.Combine("foo", "ahs", 123) };
        yield return new object?[] { Create("foo", "ahs", 321), "foo".GetHashCode(), HashCode.Combine("foo", "ahs", 321) };
        yield return new object?[] { Create("bar", "sha", 123), "bar".GetHashCode(), HashCode.Combine("bar", "sha", 123) };
        yield return new object?[] { Create("bar", "sha", 321), "bar".GetHashCode(), HashCode.Combine("bar", "sha", 321) };
        yield return new object?[] { Create("bar", "ahs", 123), "bar".GetHashCode(), HashCode.Combine("bar", "ahs", 123) };
        yield return new object?[] { Create("bar", "ahs", 321), "bar".GetHashCode(), HashCode.Combine("bar", "ahs", 321) };
    }

    private static ManifestInfo Create(string id, string sha, int repositoryStars)
    {
        return JsonConvert.DeserializeObject<ManifestInfo>(@$"{{ ""Id"": ""{id}"", ""Metadata"": {{ ""Sha"": ""{sha}"", ""RepositoryStars"": {repositoryStars} }} }}");
    }
}
