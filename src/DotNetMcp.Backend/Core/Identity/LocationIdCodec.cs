namespace DotNetMcp.Backend.Core.Identity;

/// <summary>
/// 位置 ID 编解码器
/// 
/// LocationId 格式: {memberId}@{offset}
/// - memberId: 成员 ID
/// - offset: IL 偏移量 (4 字符十六进制)
/// 
/// 示例: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@001A
/// </summary>
public static class LocationIdCodec
{
    private const char LocationSeparator = '@';

    /// <summary>
    /// 编码位置 ID
    /// </summary>
    /// <param name="memberId">成员 ID</param>
    /// <param name="ilOffset">IL 偏移量</param>
    /// <returns>编码后的 LocationId 字符串</returns>
    public static string Encode(string memberId, int ilOffset)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            throw new ArgumentException("MemberId cannot be null or empty", nameof(memberId));

        var offsetHex = ilOffset.ToString("X4"); // 4 位十六进制
        return $"{memberId}{LocationSeparator}{offsetHex}";
    }

    /// <summary>
    /// 从成员信息编码位置 ID
    /// </summary>
    public static string Encode(Guid mvid, int metadataToken, MemberKind kind, int ilOffset)
    {
        var memberId = MemberIdCodec.Encode(mvid, metadataToken, kind);
        return Encode(memberId, ilOffset);
    }

    /// <summary>
    /// 解码位置 ID
    /// </summary>
    /// <param name="locationId">LocationId 字符串</param>
    /// <returns>解码结果</returns>
    public static LocationIdDecodeResult Decode(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return LocationIdDecodeResult.Failure("LocationId cannot be null or empty");
        }

        var atIndex = locationId.LastIndexOf(LocationSeparator);
        if (atIndex == -1)
        {
            return LocationIdDecodeResult.Failure("Invalid LocationId format: missing '@' separator");
        }

        var memberId = locationId[..atIndex];
        var offsetPart = locationId[(atIndex + 1)..];

        // 验证 MemberId
        var memberResult = MemberIdCodec.Decode(memberId);
        if (!memberResult.IsSuccess)
        {
            return LocationIdDecodeResult.Failure($"Invalid MemberId: {memberResult.ErrorMessage}");
        }

        // 解析偏移量
        if (!int.TryParse(offsetPart, System.Globalization.NumberStyles.HexNumber, null, out var offset))
        {
            return LocationIdDecodeResult.Failure($"Invalid offset: {offsetPart}");
        }

        return LocationIdDecodeResult.Success(memberId, offset, memberResult);
    }

    /// <summary>
    /// 验证 LocationId 格式是否有效
    /// </summary>
    public static bool IsValid(string locationId)
    {
        return Decode(locationId).IsSuccess;
    }

    /// <summary>
    /// 从 LocationId 提取 MemberId
    /// </summary>
    public static string? ExtractMemberId(string locationId)
    {
        var result = Decode(locationId);
        return result.IsSuccess ? result.MemberId : null;
    }
}

/// <summary>
/// LocationId 解码结果
/// </summary>
public record LocationIdDecodeResult
{
    public bool IsSuccess { get; init; }
    public string? MemberId { get; init; }
    public int IlOffset { get; init; }
    public MemberIdDecodeResult? MemberInfo { get; init; }
    public string? ErrorMessage { get; init; }

    public static LocationIdDecodeResult Success(string memberId, int offset, MemberIdDecodeResult memberInfo)
        => new() { IsSuccess = true, MemberId = memberId, IlOffset = offset, MemberInfo = memberInfo };

    public static LocationIdDecodeResult Failure(string message)
        => new() { IsSuccess = false, ErrorMessage = message };
}
