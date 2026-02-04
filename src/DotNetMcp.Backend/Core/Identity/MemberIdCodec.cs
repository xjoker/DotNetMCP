namespace DotNetMcp.Backend.Core.Identity;

/// <summary>
/// 成员 ID 编解码器
/// 
/// MemberId 格式: {mvid}:{token}:{kind}
/// - mvid: 模块版本 ID (32 字符十六进制，无连字符)
/// - token: 元数据 Token (8 字符十六进制)  
/// - kind: 成员类型 (T=Type, M=Method, F=Field, P=Property, E=Event)
/// 
/// 示例: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M
/// </summary>
public static class MemberIdCodec
{
    private const char Separator = ':';

    /// <summary>
    /// 编码成员 ID
    /// </summary>
    /// <param name="mvid">模块版本 ID</param>
    /// <param name="metadataToken">元数据 Token</param>
    /// <param name="kind">成员类型</param>
    /// <returns>编码后的 MemberId 字符串</returns>
    public static string Encode(Guid mvid, int metadataToken, MemberKind kind)
    {
        var mvidHex = mvid.ToString("N"); // 无连字符格式
        var tokenHex = metadataToken.ToString("X8"); // 8 位十六进制
        var kindChar = GetKindChar(kind);

        return $"{mvidHex}{Separator}{tokenHex}{Separator}{kindChar}";
    }

    /// <summary>
    /// 解码成员 ID
    /// </summary>
    /// <param name="memberId">MemberId 字符串</param>
    /// <returns>解码结果，失败时 Success 为 false</returns>
    public static MemberIdDecodeResult Decode(string memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return MemberIdDecodeResult.Failure("MemberId cannot be null or empty");
        }

        var parts = memberId.Split(Separator);
        if (parts.Length != 3)
        {
            return MemberIdDecodeResult.Failure($"Invalid MemberId format: expected 3 parts, got {parts.Length}");
        }

        // 解析 MVID
        if (parts[0].Length != 32 || !Guid.TryParse(parts[0], out var mvid))
        {
            return MemberIdDecodeResult.Failure($"Invalid MVID: {parts[0]}");
        }

        // 解析 Token
        if (parts[1].Length != 8 || !int.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var token))
        {
            return MemberIdDecodeResult.Failure($"Invalid token: {parts[1]}");
        }

        // 解析 Kind
        if (parts[2].Length != 1 || !TryParseKind(parts[2][0], out var kind))
        {
            return MemberIdDecodeResult.Failure($"Invalid kind: {parts[2]}");
        }

        return MemberIdDecodeResult.Success(mvid, token, kind);
    }

    /// <summary>
    /// 验证 MemberId 格式是否有效
    /// </summary>
    public static bool IsValid(string memberId)
    {
        return Decode(memberId).IsSuccess;
    }

    /// <summary>
    /// 从 MemberId 提取 MVID
    /// </summary>
    public static Guid? ExtractMvid(string memberId)
    {
        var result = Decode(memberId);
        return result.IsSuccess ? result.Mvid : null;
    }

    /// <summary>
    /// 检查 MemberId 是否属于指定的程序集
    /// </summary>
    public static bool BelongsToAssembly(string memberId, Guid assemblyMvid)
    {
        var result = Decode(memberId);
        return result.IsSuccess && result.Mvid == assemblyMvid;
    }

    private static char GetKindChar(MemberKind kind) => kind switch
    {
        MemberKind.Type => 'T',
        MemberKind.Method => 'M',
        MemberKind.Field => 'F',
        MemberKind.Property => 'P',
        MemberKind.Event => 'E',
        _ => throw new ArgumentException($"Unknown member kind: {kind}")
    };

    private static bool TryParseKind(char c, out MemberKind kind)
    {
        kind = c switch
        {
            'T' => MemberKind.Type,
            'M' => MemberKind.Method,
            'F' => MemberKind.Field,
            'P' => MemberKind.Property,
            'E' => MemberKind.Event,
            _ => MemberKind.Type // 默认值，但会返回 false
        };
        return c is 'T' or 'M' or 'F' or 'P' or 'E';
    }
}

/// <summary>
/// 成员类型
/// </summary>
public enum MemberKind
{
    Type,
    Method,
    Field,
    Property,
    Event
}

/// <summary>
/// MemberId 解码结果
/// </summary>
public record MemberIdDecodeResult
{
    public bool IsSuccess { get; init; }
    public Guid Mvid { get; init; }
    public int MetadataToken { get; init; }
    public MemberKind Kind { get; init; }
    public string? ErrorMessage { get; init; }

    public static MemberIdDecodeResult Success(Guid mvid, int token, MemberKind kind)
        => new() { IsSuccess = true, Mvid = mvid, MetadataToken = token, Kind = kind };

    public static MemberIdDecodeResult Failure(string message)
        => new() { IsSuccess = false, ErrorMessage = message };
}
