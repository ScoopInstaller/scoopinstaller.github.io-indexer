using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using ScoopSearch.Functions.Manifest;
using ScoopSearch.Functions.Tests.Helpers;

namespace ScoopSearch.Functions.Tests.Manifest;

public class KeyGeneratorTests
{
    private readonly KeyGenerator _sut;

    public KeyGeneratorTests()
    {
        _sut = new KeyGenerator();
    }

    [Fact]
    public void Generate_Succeeds()
    {
        // Arrange
        var manifestMetadata = Faker.CreateManifestMetadata().Generate();

        // Act
        var result = _sut.Generate(manifestMetadata);

        // Assert
        var hashData = SHA1.HashData(Encoding.UTF8.GetBytes(manifestMetadata.Repository + manifestMetadata.BranchName + manifestMetadata.FilePath));
        var expectedKey = string.Concat(hashData.Select(_ => _.ToString("x2")));
        result.Should().Be(expectedKey);
    }
}
