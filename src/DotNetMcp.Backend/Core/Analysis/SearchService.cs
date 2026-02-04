using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 搜索服务 - 统一的搜索入口
/// </summary>
public class SearchService
{
    private readonly TypeIndex _typeIndex;
    private readonly MemberIndex _memberIndex;

    public SearchService(TypeIndex typeIndex, MemberIndex memberIndex)
    {
        _typeIndex = typeIndex ?? throw new ArgumentNullException(nameof(typeIndex));
        _memberIndex = memberIndex ?? throw new ArgumentNullException(nameof(memberIndex));
    }

    /// <summary>
    /// 统一搜索（类型 + 成员）
    /// </summary>
    public SearchResult Search(SearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Keyword))
            return SearchResult.Empty();

        var types = query.IncludeTypes
            ? _typeIndex.Search(query.Keyword, query.Limit)
            : Array.Empty<TypeIndexEntry>();

        var members = query.IncludeMembers
            ? _memberIndex.Search(query.Keyword, query.MemberKindFilter, query.Limit)
            : Array.Empty<MemberIndexEntry>();

        return new SearchResult
        {
            Types = types,
            Members = members,
            TotalTypeCount = types.Count,
            TotalMemberCount = members.Count
        };
    }

    /// <summary>
    /// 搜索类型
    /// </summary>
    public IReadOnlyList<TypeIndexEntry> SearchTypes(string keyword, int limit = 50)
        => _typeIndex.Search(keyword, limit);

    /// <summary>
    /// 搜索成员
    /// </summary>
    public IReadOnlyList<MemberIndexEntry> SearchMembers(string keyword, MemberKind? kind = null, int limit = 50)
        => _memberIndex.Search(keyword, kind, limit);

    /// <summary>
    /// 按命名空间浏览
    /// </summary>
    public IReadOnlyList<TypeIndexEntry> BrowseNamespace(string ns)
        => _typeIndex.FindByNamespace(ns);

    /// <summary>
    /// 获取类型详情（包含成员）
    /// </summary>
    public TypeDetail? GetTypeDetail(string typeId)
    {
        var type = _typeIndex.GetById(typeId);
        if (type == null) return null;

        var members = _memberIndex.GetMembersOfType(typeId);
        return new TypeDetail
        {
            Type = type,
            Methods = members.Where(m => m.Kind == MemberKind.Method).ToList(),
            Fields = members.Where(m => m.Kind == MemberKind.Field).ToList(),
            Properties = members.Where(m => m.Kind == MemberKind.Property).ToList(),
            Events = members.Where(m => m.Kind == MemberKind.Event).ToList()
        };
    }
}

/// <summary>
/// 搜索查询
/// </summary>
public record SearchQuery
{
    public required string Keyword { get; init; }
    public bool IncludeTypes { get; init; } = true;
    public bool IncludeMembers { get; init; } = true;
    public MemberKind? MemberKindFilter { get; init; }
    public int Limit { get; init; } = 50;
}

/// <summary>
/// 搜索结果
/// </summary>
public record SearchResult
{
    public required IReadOnlyList<TypeIndexEntry> Types { get; init; }
    public required IReadOnlyList<MemberIndexEntry> Members { get; init; }
    public int TotalTypeCount { get; init; }
    public int TotalMemberCount { get; init; }

    public static SearchResult Empty() => new()
    {
        Types = Array.Empty<TypeIndexEntry>(),
        Members = Array.Empty<MemberIndexEntry>()
    };
}

/// <summary>
/// 类型详情
/// </summary>
public record TypeDetail
{
    public required TypeIndexEntry Type { get; init; }
    public required IReadOnlyList<MemberIndexEntry> Methods { get; init; }
    public required IReadOnlyList<MemberIndexEntry> Fields { get; init; }
    public required IReadOnlyList<MemberIndexEntry> Properties { get; init; }
    public required IReadOnlyList<MemberIndexEntry> Events { get; init; }
}
