using DotNetMcp.Backend.Core.Identity;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Identity;

public class MemberIdCodecTests
{
    private readonly Guid _testMvid = Guid.Parse("a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6");

    [Fact]
    public void Encode_ShouldProduceCorrectFormat()
    {
        // Arrange
        var token = 0x06001234;
        var kind = MemberKind.Method;

        // Act
        var result = MemberIdCodec.Encode(_testMvid, token, kind);

        // Assert
        Assert.Equal("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M", result);
    }

    [Fact]
    public void Decode_ValidMemberId_ShouldSucceed()
    {
        // Arrange
        var memberId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M";

        // Act
        var result = MemberIdCodec.Decode(memberId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(_testMvid, result.Mvid);
        Assert.Equal(0x06001234, result.MetadataToken);
        Assert.Equal(MemberKind.Method, result.Kind);
    }

    [Fact]
    public void Decode_InvalidFormat_ShouldFail()
    {
        // Arrange
        var invalidMemberId = "invalid";

        // Act
        var result = MemberIdCodec.Decode(invalidMemberId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData(MemberKind.Type, "T")]
    [InlineData(MemberKind.Method, "M")]
    [InlineData(MemberKind.Field, "F")]
    [InlineData(MemberKind.Property, "P")]
    [InlineData(MemberKind.Event, "E")]
    public void Encode_AllKinds_ShouldUseCorrectSuffix(MemberKind kind, string expectedSuffix)
    {
        // Act
        var result = MemberIdCodec.Encode(_testMvid, 0x06000001, kind);

        // Assert
        Assert.EndsWith($":{expectedSuffix}", result);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalMvid = Guid.NewGuid();
        var originalToken = 0x02000123;
        var originalKind = MemberKind.Type;

        // Act
        var encoded = MemberIdCodec.Encode(originalMvid, originalToken, originalKind);
        var decoded = MemberIdCodec.Decode(encoded);

        // Assert
        Assert.True(decoded.IsSuccess);
        Assert.Equal(originalMvid, decoded.Mvid);
        Assert.Equal(originalToken, decoded.MetadataToken);
        Assert.Equal(originalKind, decoded.Kind);
    }

    [Fact]
    public void IsValid_ValidMemberId_ShouldReturnTrue()
    {
        // Arrange
        var validMemberId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M";

        // Act & Assert
        Assert.True(MemberIdCodec.IsValid(validMemberId));
    }

    [Fact]
    public void IsValid_InvalidMemberId_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.False(MemberIdCodec.IsValid("invalid"));
        Assert.False(MemberIdCodec.IsValid(""));
        Assert.False(MemberIdCodec.IsValid(null!));
    }

    [Fact]
    public void BelongsToAssembly_MatchingMvid_ShouldReturnTrue()
    {
        // Arrange
        var memberId = MemberIdCodec.Encode(_testMvid, 0x06001234, MemberKind.Method);

        // Act & Assert
        Assert.True(MemberIdCodec.BelongsToAssembly(memberId, _testMvid));
    }

    [Fact]
    public void BelongsToAssembly_DifferentMvid_ShouldReturnFalse()
    {
        // Arrange
        var memberId = MemberIdCodec.Encode(_testMvid, 0x06001234, MemberKind.Method);
        var differentMvid = Guid.NewGuid();

        // Act & Assert
        Assert.False(MemberIdCodec.BelongsToAssembly(memberId, differentMvid));
    }
}
