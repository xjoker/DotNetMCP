using Mono.Cecil;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 类型索引 - 存储程序集中所有类型的索引信息
/// </summary>
public class TypeIndex
{
    private readonly Dictionary<string, TypeIndexEntry> _byId = new();
    private readonly Dictionary<string, List<TypeIndexEntry>> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TypeIndexEntry>> _byNamespace = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TypeIndexEntry> _allTypes = new();
    private readonly object _lock = new();

    /// <summary>
    /// 索引版本
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 类型总数
    /// </summary>
    public int Count => _allTypes.Count;

    public TypeIndex(string version)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    /// <summary>
    /// 添加类型到索引
    /// </summary>
    public void Add(TypeIndexEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        lock (_lock)
        {
            _byId[entry.Id] = entry;
            _allTypes.Add(entry);

            // 按名称索引
            if (!_byName.TryGetValue(entry.Name, out var nameList))
            {
                nameList = new List<TypeIndexEntry>();
                _byName[entry.Name] = nameList;
            }
            nameList.Add(entry);

            // 按命名空间索引
            if (!string.IsNullOrEmpty(entry.Namespace))
            {
                if (!_byNamespace.TryGetValue(entry.Namespace, out var nsList))
                {
                    nsList = new List<TypeIndexEntry>();
                    _byNamespace[entry.Namespace] = nsList;
                }
                nsList.Add(entry);
            }
        }
    }

    /// <summary>
    /// 通过 ID 查找类型
    /// </summary>
    public TypeIndexEntry? GetById(string id)
    {
        lock (_lock)
        {
            return _byId.TryGetValue(id, out var entry) ? entry : null;
        }
    }

    /// <summary>
    /// 通过名称查找类型（可能有多个同名类型）
    /// </summary>
    public IReadOnlyList<TypeIndexEntry> FindByName(string name)
    {
        lock (_lock)
        {
            return _byName.TryGetValue(name, out var list)
                ? list.ToList()
                : Array.Empty<TypeIndexEntry>();
        }
    }

    /// <summary>
    /// 通过命名空间查找类型
    /// </summary>
    public IReadOnlyList<TypeIndexEntry> FindByNamespace(string ns)
    {
        lock (_lock)
        {
            return _byNamespace.TryGetValue(ns, out var list)
                ? list.ToList()
                : Array.Empty<TypeIndexEntry>();
        }
    }

    /// <summary>
    /// 搜索类型（名称或命名空间包含关键词）
    /// </summary>
    public IReadOnlyList<TypeIndexEntry> Search(string keyword, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Array.Empty<TypeIndexEntry>();

        lock (_lock)
        {
            return _allTypes
                .Where(t => t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                           (t.Namespace?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                           t.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
        }
    }

    /// <summary>
    /// 获取所有类型
    /// </summary>
    public IReadOnlyList<TypeIndexEntry> GetAll()
    {
        lock (_lock)
        {
            return _allTypes.ToList();
        }
    }

    /// <summary>
    /// 获取所有命名空间
    /// </summary>
    public IReadOnlyList<string> GetNamespaces()
    {
        lock (_lock)
        {
            return _byNamespace.Keys.OrderBy(n => n).ToList();
        }
    }
}

/// <summary>
/// 类型索引条目
/// </summary>
public record TypeIndexEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string? Namespace { get; init; }
    public required TypeKind Kind { get; init; }
    public required TypeVisibility Visibility { get; init; }
    public required bool IsGeneric { get; init; }
    public required int GenericParameterCount { get; init; }
    public required string? BaseType { get; init; }
    public required IReadOnlyList<string> Interfaces { get; init; }
    public required int MethodCount { get; init; }
    public required int FieldCount { get; init; }
    public required int PropertyCount { get; init; }
}

/// <summary>
/// 类型种类
/// </summary>
public enum TypeKind
{
    Class,
    Interface,
    Struct,
    Enum,
    Delegate
}

/// <summary>
/// 类型可见性
/// </summary>
public enum TypeVisibility
{
    Public,
    Internal,
    Private,
    Protected,
    ProtectedInternal
}
