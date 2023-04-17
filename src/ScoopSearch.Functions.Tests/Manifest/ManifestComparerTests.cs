using FluentAssertions;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Manifest;
using Faker = ScoopSearch.Functions.Tests.Helpers.Faker;

namespace ScoopSearch.Functions.Tests.Manifest;

public class ManifestComparerTests
{
    [Theory]
    [CombinatorialData]
    public void Equals_Succeeds(
        [CombinatorialValues("foo", "bar", "FOO")] string id,
        [CombinatorialValues(null, "commitHash", "commitHashFoo")] string? commitHash,
        [CombinatorialValues(1, 2)] int stars,
        [CombinatorialValues(null, "manifestHash", "manifestHashFoo")] string? manifestHash,
        [CombinatorialValues(null, 0, 1)] int? officialRepository)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;

        var manifestInfo1 = CreateManifestInfo(id, commitHash, stars, manifestHash, officialRepository);
        var manifestInfo2 = CreateManifestInfo("foo", "commitHash", 2, "manifestHash", 1);

        // Act
        var resultIdEquals = manifestIdComparer.Equals(manifestInfo1, manifestInfo2);
        var resultExactEquals = manifestExactComparer.Equals(manifestInfo1, manifestInfo2);

        // Assert
        resultIdEquals.Should().Be(id == manifestInfo2.Id);
        resultExactEquals.Should().Be(id == manifestInfo2.Id
                                   && commitHash == manifestInfo2.Metadata.Sha
                                   && stars == manifestInfo2.Metadata.RepositoryStars
                                   && manifestHash == manifestInfo2.Metadata.ManifestHash
                                   && officialRepository == manifestInfo2.Metadata.OfficialRepositoryNumber);
    }

    [Theory]
    [CombinatorialData]
    public void GetHashCode_Succeeds(
        [CombinatorialValues("foo", "bar", "FOO")] string id,
        [CombinatorialValues(null, "commitHash", "commitHashFoo")] string? commitHash,
        [CombinatorialValues(1, 2)] int stars,
        [CombinatorialValues(null, "manifestHash", "manifestHashFoo")] string? manifestHash,
        [CombinatorialValues(null, 0, 1)] int? officialRepository)
    {
        // Arrange
        var manifestIdComparer = ManifestComparer.ManifestIdComparer;
        var manifestExactComparer = ManifestComparer.ManifestExactComparer;
        var manifestInfo = CreateManifestInfo(id, commitHash, stars, manifestHash, officialRepository);

        // Act
        var resultIdHashCode = manifestIdComparer.GetHashCode(manifestInfo);
        var resultExactHashCode = manifestExactComparer.GetHashCode(manifestInfo);

        // Assert
        resultIdHashCode.Should().Be(id.GetHashCode());
        resultExactHashCode.Should().Be(HashCode.Combine(id, commitHash, manifestHash, stars, officialRepository));
    }

    private static ManifestInfo CreateManifestInfo(string id, string? commitHash, int stars, string? manifestHash, int? officialRepository)
    {
        return Faker.CreateManifestInfo(_ => _
                .RuleFor(f => f.Sha, commitHash)
                .RuleFor(f => f.RepositoryStars, stars)
                .RuleFor(f => f.ManifestHash, manifestHash)
                .RuleFor(f => f.OfficialRepositoryNumber, officialRepository))
            .RuleFor(f => f.Id, id);
    }
}
