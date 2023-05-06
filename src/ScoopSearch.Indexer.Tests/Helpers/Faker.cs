using Bogus;
using ScoopSearch.Indexer.Data;

namespace ScoopSearch.Indexer.Tests.Helpers;

public static class Faker
{
    public static Faker<ManifestInfo> CreateManifestInfo(Action<Faker<ManifestMetadata>>? configureMetadata = null)
    {
        var manifestMetadata = CreateManifestMetadata();
        configureMetadata?.Invoke(manifestMetadata);

        var faker = new Faker<ManifestInfo>()
            .StrictMode(true)
            .RuleFor(_ => _.Id, f => f.Random.Hash())
            .RuleFor(_ => _.Name, f => f.Lorem.Word())
            .RuleFor(_ => _.NameSortable, (f, o) => o.Name?.ToLowerInvariant())
            .RuleFor(_ => _.NamePartial, (f, o) => o.Name)
            .RuleFor(_ => _.NameSuffix, (f, o) => o.Name)
            .RuleFor(_ => _.Description, f => f.Lorem.Sentences())
            .RuleFor(_ => _.Homepage, f => f.Internet.Url())
            .RuleFor(_ => _.License, f => f.PickRandomParam("MIT", "BSD", "GPL", "Custom"))
            .RuleFor(_ => _.Version, f => f.System.Semver())
            .RuleFor(_ => _.Metadata, manifestMetadata);

        return faker;
    }

    public static Faker<ManifestMetadata> CreateManifestMetadata()
    {
        var faker = new Faker<ManifestMetadata>()
            .StrictMode(true)
            .RuleFor(_ => _.Sha, f => f.Random.Hash())
            .RuleFor(_ => _.RepositoryStars, f => f.Random.Int(0, 1000))
            .RuleFor(_ => _.OfficialRepositoryNumber, f => f.Random.Int(0, 1))
            .RuleFor(_ => _.Repository, f => f.Internet.UrlRootedPath())
            .RuleFor(_ => _.OfficialRepository, f => f.Random.Bool())
            .RuleFor(_ => _.BranchName, f => f.Lorem.Word())
            .RuleFor(_ => _.FilePath, f => f.System.FilePath())
            .RuleFor(_ => _.AuthorName, f => f.Name.FullName())
            .RuleFor(_ => _.AuthorMail, f => f.Internet.Email())
            .RuleFor(_ => _.Committed, f => f.Date.RecentOffset())
            .RuleFor(_ => _.ManifestHash, f => f.Random.Hash())
            .RuleFor(_ => _.DuplicateOf, f => null);

        return faker;
    }
}
