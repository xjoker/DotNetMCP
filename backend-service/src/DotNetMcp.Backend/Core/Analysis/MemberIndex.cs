using Mono.Cecil;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 成员索引 - 存储程序集中所有成员（方法、字段、属性、事件）的索引
/// </summary>
public class MemberIndex
{
    private readonly Dictionary<string, MemberIndexEntry> _byId = new();
    private readonly Dictionary<string, List<MemberIndexEntry>> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MemberIndexEntry>> _byDeclaringType = new();
    private readonly List<MemberIndexEntry> _allMembers = new();
    private readonly object _lock = new();

    /// <summary>
    /// 索引版本
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 成员总数
    /// </summary>
    public int Count => _allMembers.Count;

    public MemberIndex(string version)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>
    /// 添加成员到索引
    /// </summary>
    public void Add(MemberIndexEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        lock (_lock)
        {
            _byId[entry.Id] = entry;
            _allMembers.Add(entry);

            // 按名称索引
            if (!_byName.TryGetValue(entry.Name, out var nameList))
            {
                nameList = new List<MemberIndexEntry>();
                _byName[entry.Name] = nameList;
            }
            nameList.Add(entry);

            // 按声明类型索引
            if (!_byDeclaringType.TryGetValue(entry.DeclaringTypeId, out var typeList))
            {
                typeList = new List<MemberIndexEntry>();
                _byDeclaringType[entry.DeclaringTypeId] = typeList;
            }
            typeList.Add(entry);
        }
    }

    /// <summary>
    /// 通过 ID 查找成员
    /// </summary>
    public MemberIndexEntry? GetById(string id)
    {
        lock (_lock)
        {
            return _byId.TryGetValue(id, out var entry) ? entry : null;
        }
    }

    /// <summary>
    /// 通过名称查找成员
    /// </summary>
    public IReadOnlyList<MemberIndexEntry> FindByName(string name)
    {
        lock (_lock)
        {
            return _byName.TryGetValue(name, out var list)
                ? list.ToList()
                : Array.Empty<MemberIndexEntry>();
        }
    }

    /// <summary>
    /// 获取类型的所有成员
    /// </summary>
    public IReadOnlyList<MemberIndexEntry> GetMembersOfType(string typeId)
    {
        lock (_lock)
        {
            return _byDeclaringType.TryGetValue(typeId, out var list)
                ? list.ToList()
                : Array.Empty<MemberIndexEntry>();
        }
    }

    /// <summary>
    /// 搜索成员
    /// </summary>
    public IReadOnlyList<MemberIndexEntry> Search(string keyword, MemberKind? kindFilter = null, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Array.Empty<MemberIndexEntry>();

        lock (_lock)
        {
            var query = _allMembers
                .Where(m => m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                           m.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (kindFilter.HasValue)
            {
                query = query.Where(m => m.Kind == kindFilter.Value);
            }

            return query.Take(limit).ToList();
        }
    }

    /// <summary>
    /// 获取所有方法
    /// </summary>
    public IReadOnlyList<MemberIndexEntry> GetAllMethods()
    {
        lock (_lock)
        {
            return _allMembers.Where(m => m.Kind == MemberKind.Method).ToList();
        }
    }

    /// <summary>
    /// 按种类统计成员数量
    /// </summary>
    public Dictionary<MemberKind, int> GetStatsByKind()
    {
        lock (_lock)
        {
            return _allMembers
                .GroupBy(m => m.Kind)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}

/// <summary>
/// 成员索引条目
/// </summary>
public record MemberIndexEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string DeclaringTypeId { get; init; }
    public required string DeclaringTypeName { get; init; }
    public required MemberKind Kind { get; init; }
    public required MemberVisibility Visibility { get; init; }
    public required bool IsStatic { get; init; }
    public required bool IsVirtual { get; init; }
    public required bool IsAbstract { get; init; }
    public required string? ReturnType { get; init; }
    public required IReadOnlyList<ParameterInfo> Parameters { get; init; }
}

/// <summary>
/// 参数信息
/// </summary>
public record ParameterInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool IsOptional { get; init; }
}

/// <summary>
/// 成员可见性
/// </summary>
public enum MemberVisibility
{
    Public,
    Private,
    Protected,
    Internal,
    ProtectedInternal,
    PrivateProtected
}
