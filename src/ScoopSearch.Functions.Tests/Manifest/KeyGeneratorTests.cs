using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ScoopSearch.Functions.Data;
using ScoopSearch.Functions.Manifest;

namespace ScoopSearch.Functions.Tests.Manifest;

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
        // Arrange + Act + Assert
        _sut.Dispose();
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
