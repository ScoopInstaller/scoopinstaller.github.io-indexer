using FluentAssertions;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Tests.Helpers;

namespace ScoopSearch.Indexer.Tests.Manifest;

public class ManifestInfoDeserializationTests
{
    [Fact]
    public void Deserialize_Id_Succeeds()
    {
        // Arrange
        var key = "foo";

        // + Act
        var result = ManifestInfo.Deserialize("{}", key, new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("foo");
    }

    [Fact]
    public void Deserialize_Metadata_Succeeds()
    {
        // Arrange
        var manifestMetadata = new ManifestMetadata();

        // + Act
        var result = ManifestInfo.Deserialize("{}", "foo", manifestMetadata);

        // Assert
        result.Should().NotBeNull();
        result!.Metadata.Should().BeSameAs(manifestMetadata);
    }

    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("FOO", "FOO")]
    [InlineData("bucket/foo", "foo")]
    public void Deserialize_Name_Succeeds(string filePath, string expectedResult)
    {
        // Arrange
        var manifestMetadata = Faker.CreateManifestMetadata()
            .RuleFor(manifestMetadata => manifestMetadata.FilePath, filePath);

        // Act
        var result = ManifestInfo.Deserialize("{}", "foo", manifestMetadata);

        // Assert
        result.Should().BeOfType<ManifestInfo>();
        result!.Name.Should().Be(expectedResult);
        result.NamePartial.Should().Be(expectedResult);
        result.NameSortable.Should().Be(expectedResult.ToLowerInvariant());
        result.NameSuffix.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(@"""license"": """"", "")]
    [InlineData(@"""license"": ""foo""", "foo")]
    [InlineData(@"""license"": [ ""foo"", ""bar"" ]", "foo, bar")]
    [InlineData(@"""license"": { ""identifier"": ""foo"" }", "foo")]
    [InlineData(@"""license"": { ""url"": ""bar"" }", "bar")]
    [InlineData(@"""license"": { ""identifier"": ""foo"", ""url"": ""bar"" }", "foo")]
    [InlineData(@"""license"": [ { ""identifier"": ""foo"", ""url"": ""bar"" } ]", "foo")]
    [InlineData(@"""license"": [ { ""identifier"": ""foo"" }, { ""identifier"": ""bar"" } ]", "foo, bar")]
    [InlineData(@"""license"": [ { ""identifier"": ""foo"" }, { ""url"": ""bar"" } ]", "foo, bar")]
    public void Deserialize_License_ReturnsSucceeds(string jsonContent, string? expectedResult)
    {
        // Arrange
        jsonContent = $"{{ {jsonContent} }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.License.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(@"""description"": """"", "")]
    [InlineData(@"""description"": ""foo""", "foo")]
    [InlineData(@"""description"": [ ""foo"" ]", "foo")]
    [InlineData(@"""description"": [ ""foo"", ""bar"" ]", "foo bar")]
    [InlineData(@"""description"": [ ""foo"", """", ""bar"" ]", "foo \n bar")]
    public void Deserialize_Description_Succeeds(string jsonContent, string? expectedResult)
    {
        // Arrange
        jsonContent = $"{{ {jsonContent} }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be(expectedResult?.Replace("\n", Environment.NewLine));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(@"""version"": """"", "")]
    [InlineData(@"""version"": ""v1""", "v1")]
    [InlineData(@"""version"": ""1.2.3""", "1.2.3")]
    public void Deserialize_Version_Succeeds(string jsonContent, string? expectedResult)
    {
        // Arrange
        jsonContent = $"{{ {jsonContent} }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(@"""homepage"": """"", "")]
    [InlineData(@"""homepage"": ""foo""", "foo")]
    [InlineData(@"""homepage"": ""https://www.example.COM""", "https://www.example.COM")]
    public void Deserialize_HomePage_Succeeds(string jsonContent, string? expectedResult)
    {
        // Arrange
        jsonContent = $"{{ {jsonContent} }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.Homepage.Should().Be(expectedResult);
    }

    [Fact]
    public void Deserialize_Comments_Succeeds()
    {
        // Arrange
        var jsonContent = $@"{{
// foo comment
""property"": ""foo"",
}}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Deserialize_TrailingComma_Succeeds()
    {
        // Arrange
        var jsonContent = $@"{{ ""property"": ""foo"", }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
    }
}
