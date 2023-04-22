using FluentAssertions;
using ScoopSearch.Functions.Data;

namespace ScoopSearch.Functions.Tests.Manifest;

public class ManifestInfoDeserializationTests
{
    [Theory]
    [MemberData(nameof(DeserializeLicenseTestCases))]
    public void Deserialize_License_ReturnsSucceeds(string jsonContent, string expectedResult)
    {
        // Arrange
        jsonContent = $"{{ {jsonContent} }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.License.Should().Be(expectedResult);
    }

    public static IEnumerable<object[]> DeserializeLicenseTestCases
    {
        get
        {
            yield return new object[] { @"""license"": ""foo""", "foo" };
            yield return new object[] { @"""license"": [ ""foo"", ""bar"" ]", "foo, bar" };
            yield return new object[] { @"""license"": { ""identifier"": ""foo"" }", "foo" };
            yield return new object[] { @"""license"": { ""url"": ""bar"" }", "bar" };
            yield return new object[] { @"""license"": { ""identifier"": ""foo"", ""url"": ""bar"" }", "foo" };
            yield return new object[] { @"""license"": [ { ""identifier"": ""foo"", ""url"": ""bar"" } ]", "foo" };
            yield return new object[] { @"""license"": [ { ""identifier"": ""foo"" }, { ""identifier"": ""bar"" } ]", "foo, bar" };
            yield return new object[] { @"""license"": [ { ""identifier"": ""foo"" }, { ""url"": ""bar"" } ]", "foo, bar" };
        }
    }

    [Theory]
    [InlineData(@"""description"": ""foo""", "foo")]
    [InlineData(@"""description"": [ ""foo"" ]", "foo")]
    [InlineData(@"""description"": [ ""foo"", ""bar"" ]", "foo bar")]
    [InlineData(@"""description"": [ ""foo"", """", ""bar"" ]", "foo \n bar")]
    public void Deserialize_Description_Succeeds(string jsonContent, string expectedDescription)
    {
        // Arrange
        jsonContent = $"{{ {jsonContent} }}";

        // Act
        var result = ManifestInfo.Deserialize(jsonContent, "foo", new ManifestMetadata());

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be(expectedDescription.Replace("\n", Environment.NewLine));
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
