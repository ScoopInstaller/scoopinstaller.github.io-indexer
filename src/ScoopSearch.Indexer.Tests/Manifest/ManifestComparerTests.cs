using FluentAssertions;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Manifest;
using Faker = ScoopSearch.Indexer.Tests.Helpers.Faker;

namespace ScoopSearch.Indexer.Tests.Manifest;

public class ManifestComparerTests
{
    [Theory]
    [CombinatorialData]
    public void Equals_Succeeds(
        [CombinatorialValues("foo", "bar", "FOO")] string id,
        [CombinatorialValues(null, "commitHash", "commitHashFoo")] string? commitHash,
        [CombinatorialValues(1, 2)] int stars,
        [CombinatorialValues(null, 0, 1)] int? officialRepository,
        [CombinatorialValues("foo", "bar", "FOO")] string duplicateOf)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;

        var manifestInfo1 = CreateManifestInfo(id, commitHash, stars, officialRepository, duplicateOf);
        var manifestInfo2 = CreateManifestInfo("foo", "commitHash", 2, 1, "foo");

        // Act
        var resultIdEquals = manifestIdComparer.Equals(manifestInfo1, manifestInfo2);
        var resultExactEquals = manifestExactComparer.Equals(manifestInfo1, manifestInfo2);

        // Assert
        resultIdEquals.Should().Be(id == manifestInfo2.Id);
        resultExactEquals.Should().Be(id == manifestInfo2.Id
                                   && commitHash == manifestInfo2.Metadata.Sha
                                   && stars == manifestInfo2.Metadata.RepositoryStars
                                   && officialRepository == manifestInfo2.Metadata.OfficialRepositoryNumber
                                   && duplicateOf == manifestInfo2.Metadata.DuplicateOf);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Equals_Nulls_ReturnsFalse(bool xNull, bool yNull, bool expectedEquals)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;
        var x = xNull ? null : (ManifestInfo)Faker.CreateManifestInfo();
        var y = yNull ? null : (ManifestInfo)Faker.CreateManifestInfo();

        // Act
        var resultIdEquals = manifestIdComparer.Equals(x, y);
        var resultExactEquals = manifestExactComparer.Equals(x, y);

        // Assert
        resultIdEquals.Should().Be(expectedEquals);
        resultExactEquals.Should().Be(expectedEquals);
    }

    [Theory]
    [CombinatorialData]
    public void GetHashCode_Succeeds(
        [CombinatorialValues("foo", "bar", "FOO")] string id,
        [CombinatorialValues(null, "commitHash", "commitHashFoo")] string? commitHash,
        [CombinatorialValues(1, 2)] int stars,
        [CombinatorialValues(null, 0, 1)] int? officialRepository,
        [CombinatorialValues("foo", "bar", "FOO")] string duplicateOf)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;
        var manifestInfo = CreateManifestInfo(id, commitHash, stars, officialRepository, duplicateOf);

        // Act
        var resultIdHashCode = manifestIdComparer.GetHashCode(manifestInfo);
        var resultExactHashCode = manifestExactComparer.GetHashCode(manifestInfo);

        // Assert
        resultIdHashCode.Should().Be(id.GetHashCode());
        resultExactHashCode.Should().Be(HashCode.Combine(id, commitHash, stars, officialRepository, duplicateOf));
    }

    private static ManifestInfo CreateManifestInfo(string id, string? commitHash, int stars, int? officialRepository, string duplicateOf)
    {
        return Faker.CreateManifestInfo(_ => _
                .RuleFor(manifestMetadata => manifestMetadata.Sha, commitHash)
                .RuleFor(manifestMetadata => manifestMetadata.RepositoryStars, stars)
                .RuleFor(manifestMetadata => manifestMetadata.OfficialRepositoryNumber, officialRepository)
                .RuleFor(manifestMetadata => manifestMetadata.DuplicateOf, duplicateOf))
            .RuleFor(manifestInfo => manifestInfo.Id, id);
    }
}
