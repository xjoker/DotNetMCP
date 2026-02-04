using DotNetMcp.Backend.Core.Identity;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Identity;

public class LocationIdCodecTests
{
    private readonly Guid _testMvid = Guid.Parse("a1b2c3d4-e5f6-a7b8-c9d0-e1f2a3b4c5d6");

    [Fact]
    public void Encode_ShouldProduceCorrectFormat()
    {
        // Arrange
        var memberId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M";
        var offset = 0x001A;

        // Act
        var result = LocationIdCodec.Encode(memberId, offset);

        // Assert
        Assert.Equal("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@001A", result);
    }

    [Fact]
    public void Decode_ValidLocationId_ShouldSucceed()
    {
        // Arrange
        var locationId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@001A";

        // Act
        var result = LocationIdCodec.Decode(locationId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M", result.MemberId);
        Assert.Equal(0x001A, result.IlOffset);
        Assert.NotNull(result.MemberInfo);
        Assert.Equal(MemberKind.Method, result.MemberInfo!.Kind);
    }

    [Fact]
    public void Decode_MissingAtSymbol_ShouldFail()
    {
        // Arrange
        var invalidLocationId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M";

        // Act
        var result = LocationIdCodec.Decode(invalidLocationId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("missing '@'", result.ErrorMessage);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var memberId = MemberIdCodec.Encode(_testMvid, 0x06001234, MemberKind.Method);
        var offset = 0x00FF;

        // Act
        var encoded = LocationIdCodec.Encode(memberId, offset);
        var decoded = LocationIdCodec.Decode(encoded);

        // Assert
        Assert.True(decoded.IsSuccess);
        Assert.Equal(memberId, decoded.MemberId);
        Assert.Equal(offset, decoded.IlOffset);
    }

    [Fact]
    public void IsValid_ValidLocationId_ShouldReturnTrue()
    {
        // Arrange
        var validLocationId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@001A";

        // Act & Assert
        Assert.True(LocationIdCodec.IsValid(validLocationId));
    }

    [Fact]
    public void ExtractMemberId_ValidLocationId_ShouldReturnMemberId()
    {
        // Arrange
        var expectedMemberId = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M";
        var locationId = $"{expectedMemberId}@001A";

        // Act
        var result = LocationIdCodec.ExtractMemberId(locationId);

        // Assert
        Assert.Equal(expectedMemberId, result);
    }

    [Fact]
    public void Encode_WithComponents_ShouldProduceCorrectFormat()
    {
        // Act
        var result = LocationIdCodec.Encode(_testMvid, 0x06001234, MemberKind.Method, 0x001A);

        // Assert
        Assert.Equal("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@001A", result);
    }
}
