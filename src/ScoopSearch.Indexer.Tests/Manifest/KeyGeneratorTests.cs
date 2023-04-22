using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ScoopSearch.Indexer.Data;
using ScoopSearch.Indexer.Manifest;

namespace ScoopSearch.Indexer.Tests.Manifest;

public class KeyGeneratorTests
{
    private readonly KeyGenerator _sut;

    public KeyGeneratorTests()
    {
        _sut = new KeyGenerator();
    }

    [Fact]
    public void Dispose_Succeeds()
    {
        // Arrange + Act
        Action act = () => _sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Generate_Succeeds()
    {
        // Arrange
        var repository = "repository";
        var branchName = "branchName";
        var filePath = "filePath";
        var manifestMetadata = new ManifestMetadata(repository, branchName, filePath, "authorName", "authorMail", DateTimeOffset.Now, "sha");
        var hashData = SHA1.HashData(Encoding.UTF8.GetBytes(repository + branchName + filePath));
        var expectedKey = string.Concat(hashData.Select(_ => _.ToString("x2")));

        // Act
        var result = _sut.Generate(manifestMetadata);

        // Assert
        result.Should().Be(expectedKey);
    }
}
