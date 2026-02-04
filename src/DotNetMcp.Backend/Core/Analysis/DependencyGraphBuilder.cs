using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;
using System.Text;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 依赖图构建器 - 分析程序集、命名空间和类型级别的依赖关系
/// </summary>
public class DependencyGraphBuilder
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    public DependencyGraphBuilder(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 构建程序集级别依赖图
    /// </summary>
    public DependencyGraph BuildAssemblyDependencies()
    {
        var nodes = new Dictionary<string, DependencyNode>();
        var edges = new List<DependencyEdge>();

        // 当前程序集作为根节点
        var rootId = _module.Assembly.Name.Name;
        nodes[rootId] = new DependencyNode
        {
            Id = rootId,
            Name = _module.Assembly.Name.Name,
            FullName = _module.Assembly.FullName,
            Type = DependencyNodeType.Assembly,
            IsExternal = false,
            Version = _module.Assembly.Name.Version?.ToString()
        };

        // 添加所有引用的程序集
        foreach (var asmRef in _module.AssemblyReferences)
        {
            var refId = asmRef.Name;
            if (!nodes.ContainsKey(refId))
            {
                nodes[refId] = new DependencyNode
                {
                    Id = refId,
                    Name = asmRef.Name,
                    FullName = asmRef.FullName,
                    Type = DependencyNodeType.Assembly,
                    IsExternal = true,
                    Version = asmRef.Version?.ToString()
                };
            }

            edges.Add(new DependencyEdge
            {
                FromId = rootId,
                ToId = refId,
                Kind = DependencyKind.Reference,
                Weight = 1
            });
        }

        return new DependencyGraph
        {
            RootId = rootId,
            Level = DependencyLevel.Assembly,
            Nodes = nodes,
            Edges = edges
        };
    }

    /// <summary>
    /// 构建命名空间级别依赖图
    /// </summary>
    public DependencyGraph BuildNamespaceDependencies()
    {
        var nodes = new Dictionary<string, DependencyNode>();
        var edgeMap = new Dictionary<string, DependencyEdge>();

        // 收集所有命名空间
        var namespaces = _module.Types
            .Where(t => !string.IsNullOrEmpty(t.Namespace))
            .Select(t => t.Namespace!)
            .Distinct()
            .ToList();

        foreach (var ns in namespaces)
        {
            nodes[ns] = new DependencyNode
            {
                Id = ns,
                Name = ns,
                FullName = ns,
                Type = DependencyNodeType.Namespace,
                IsExternal = false
            };
        }

        // 分析类型间的命名空间依赖
        foreach (var type in _module.Types)
        {
            if (string.IsNullOrEmpty(type.Namespace)) continue;
            var fromNs = type.Namespace;

            // 基类依赖
            AddNamespaceDependency(edgeMap, nodes, fromNs, type.BaseType, DependencyKind.Inheritance);

            // 接口依赖
            foreach (var iface in type.Interfaces)
            {
                AddNamespaceDependency(edgeMap, nodes, fromNs, iface.InterfaceType, DependencyKind.Implementation);
            }

            // 字段类型依赖
            foreach (var field in type.Fields)
            {
                AddNamespaceDependency(edgeMap, nodes, fromNs, field.FieldType, DependencyKind.Usage);
            }

            // 方法依赖
            foreach (var method in type.Methods)
            {
                AddNamespaceDependency(edgeMap, nodes, fromNs, method.ReturnType, DependencyKind.Usage);

                foreach (var param in method.Parameters)
                {
                    AddNamespaceDependency(edgeMap, nodes, fromNs, param.ParameterType, DependencyKind.Usage);
                }

                if (method.HasBody)
                {
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.Operand is MethodReference methodRef)
                        {
                            AddNamespaceDependency(edgeMap, nodes, fromNs, methodRef.DeclaringType, DependencyKind.Call);
                        }
                        else if (instruction.Operand is FieldReference fieldRef)
                        {
                            AddNamespaceDependency(edgeMap, nodes, fromNs, fieldRef.DeclaringType, DependencyKind.Usage);
                        }
                        else if (instruction.Operand is TypeReference typeRef)
                        {
                            AddNamespaceDependency(edgeMap, nodes, fromNs, typeRef, DependencyKind.Usage);
                        }
                    }
                }
            }
        }

        return new DependencyGraph
        {
            RootId = namespaces.FirstOrDefault() ?? "",
            Level = DependencyLevel.Namespace,
            Nodes = nodes,
            Edges = edgeMap.Values.ToList()
        };
    }

    /// <summary>
    /// 构建类型级别依赖图
    /// </summary>
    public DependencyGraph BuildTypeDependencies(string? rootTypeName = null, int maxDepth = 3)
    {
        var nodes = new Dictionary<string, DependencyNode>();
        var edgeMap = new Dictionary<string, DependencyEdge>();
        var visited = new HashSet<string>();

        var types = rootTypeName != null
            ? _module.Types.Where(t => t.FullName == rootTypeName).ToList()
            : _module.Types.Where(t => !t.Name.StartsWith("<")).ToList();

        foreach (var type in types)
        {
            AnalyzeTypeDependencies(type, nodes, edgeMap, visited, 0, maxDepth);
        }

        return new DependencyGraph
        {
            RootId = rootTypeName ?? _module.Assembly.Name.Name,
            Level = DependencyLevel.Type,
            Nodes = nodes,
            Edges = edgeMap.Values.ToList()
        };
    }

    /// <summary>
    /// 生成 Mermaid 格式的依赖图
    /// </summary>
    public string ToMermaid(DependencyGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        // 节点
        foreach (var node in graph.Nodes.Values.Take(50)) // 限制节点数量
        {
            var shape = node.IsExternal ? $"(({node.Name}))" : $"[{node.Name}]";
            var nodeId = SanitizeId(node.Id);
            sb.AppendLine($"    {nodeId}{shape}");
        }

        sb.AppendLine();

        // 边
        foreach (var edge in graph.Edges.Take(100)) // 限制边数量
        {
            var fromId = SanitizeId(edge.FromId);
            var toId = SanitizeId(edge.ToId);
            var label = edge.Weight > 1 ? $"|{edge.Weight}|" : "";
            sb.AppendLine($"    {fromId} -->{label} {toId}");
        }

        return sb.ToString();
    }

    private void AnalyzeTypeDependencies(
        TypeDefinition type,
        Dictionary<string, DependencyNode> nodes,
        Dictionary<string, DependencyEdge> edgeMap,
        HashSet<string> visited,
        int depth,
        int maxDepth)
    {
        if (depth > maxDepth || visited.Contains(type.FullName))
            return;

        visited.Add(type.FullName);

        // 添加当前类型节点
        if (!nodes.ContainsKey(type.FullName))
        {
            nodes[type.FullName] = new DependencyNode
            {
                Id = type.FullName,
                Name = type.Name,
                FullName = type.FullName,
                Type = DependencyNodeType.Type,
                IsExternal = false
            };
        }

        // 基类依赖
        if (type.BaseType != null && type.BaseType.FullName != "System.Object")
        {
            AddTypeDependency(edgeMap, nodes, type.FullName, type.BaseType, DependencyKind.Inheritance);
        }

        // 接口依赖
        foreach (var iface in type.Interfaces)
        {
            AddTypeDependency(edgeMap, nodes, type.FullName, iface.InterfaceType, DependencyKind.Implementation);
        }

        // 字段类型依赖
        foreach (var field in type.Fields)
        {
            AddTypeDependency(edgeMap, nodes, type.FullName, field.FieldType, DependencyKind.Usage);
        }

        // 方法依赖
        foreach (var method in type.Methods)
        {
            AddTypeDependency(edgeMap, nodes, type.FullName, method.ReturnType, DependencyKind.Usage);

            foreach (var param in method.Parameters)
            {
                AddTypeDependency(edgeMap, nodes, type.FullName, param.ParameterType, DependencyKind.Usage);
            }

            if (method.HasBody)
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is MethodReference methodRef)
                    {
                        AddTypeDependency(edgeMap, nodes, type.FullName, methodRef.DeclaringType, DependencyKind.Call);
                    }
                }
            }
        }
    }

    private void AddNamespaceDependency(
        Dictionary<string, DependencyEdge> edgeMap,
        Dictionary<string, DependencyNode> nodes,
        string fromNs,
        TypeReference? typeRef,
        DependencyKind kind)
    {
        if (typeRef == null) return;

        var toNs = GetNamespace(typeRef);
        if (string.IsNullOrEmpty(toNs) || toNs == fromNs) return;

        // 添加外部命名空间节点
        if (!nodes.ContainsKey(toNs))
        {
            nodes[toNs] = new DependencyNode
            {
                Id = toNs,
                Name = toNs,
                FullName = toNs,
                Type = DependencyNodeType.Namespace,
                IsExternal = !_module.Types.Any(t => t.Namespace == toNs)
            };
        }

        var edgeKey = $"{fromNs}->{toNs}";
        if (edgeMap.TryGetValue(edgeKey, out var existing))
        {
            edgeMap[edgeKey] = existing with { Weight = existing.Weight + 1 };
        }
        else
        {
            edgeMap[edgeKey] = new DependencyEdge
            {
                FromId = fromNs,
                ToId = toNs,
                Kind = kind,
                Weight = 1
            };
        }
    }

    private void AddTypeDependency(
        Dictionary<string, DependencyEdge> edgeMap,
        Dictionary<string, DependencyNode> nodes,
        string fromType,
        TypeReference? typeRef,
        DependencyKind kind)
    {
        if (typeRef == null) return;

        var toType = GetBaseTypeName(typeRef);
        if (string.IsNullOrEmpty(toType) || toType == fromType) return;
        if (toType.StartsWith("System.") && kind == DependencyKind.Usage) return; // 忽略系统类型的普通使用

        // 添加目标类型节点
        if (!nodes.ContainsKey(toType))
        {
            nodes[toType] = new DependencyNode
            {
                Id = toType,
                Name = typeRef.Name,
                FullName = toType,
                Type = DependencyNodeType.Type,
                IsExternal = !_module.Types.Any(t => t.FullName == toType)
            };
        }

        var edgeKey = $"{fromType}->{toType}:{kind}";
        if (edgeMap.TryGetValue(edgeKey, out var existing))
        {
            edgeMap[edgeKey] = existing with { Weight = existing.Weight + 1 };
        }
        else
        {
            edgeMap[edgeKey] = new DependencyEdge
            {
                FromId = fromType,
                ToId = toType,
                Kind = kind,
                Weight = 1
            };
        }
    }

    private static string? GetNamespace(TypeReference typeRef)
    {
        if (typeRef is GenericInstanceType git)
            return git.ElementType.Namespace;
        if (typeRef is ArrayType at)
            return GetNamespace(at.ElementType);
        return typeRef.Namespace;
    }

    private static string GetBaseTypeName(TypeReference typeRef)
    {
        if (typeRef is GenericInstanceType git)
            return git.ElementType.FullName;
        if (typeRef is ArrayType at)
            return GetBaseTypeName(at.ElementType);
        return typeRef.FullName;
    }

    private static string SanitizeId(string id)
    {
        return id.Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace("`", "_");
    }
}

#region 数据结构

/// <summary>
/// 依赖图
/// </summary>
public record DependencyGraph
{
    public required string RootId { get; init; }
    public required DependencyLevel Level { get; init; }
    public required Dictionary<string, DependencyNode> Nodes { get; init; }
    public required List<DependencyEdge> Edges { get; init; }

    public int TotalNodes => Nodes.Count;
    public int ExternalNodes => Nodes.Values.Count(n => n.IsExternal);
    public int InternalNodes => TotalNodes - ExternalNodes;
    public int TotalEdges => Edges.Count;
}

/// <summary>
/// 依赖节点
/// </summary>
public record DependencyNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required DependencyNodeType Type { get; init; }
    public required bool IsExternal { get; init; }
    public string? Version { get; init; }
}

/// <summary>
/// 依赖边
/// </summary>
public record DependencyEdge
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required DependencyKind Kind { get; init; }
    public int Weight { get; init; } = 1;
}

/// <summary>
/// 依赖级别
/// </summary>
public enum DependencyLevel
{
    Assembly,
    Namespace,
    Type,
    Method
}

/// <summary>
/// 依赖节点类型
/// </summary>
public enum DependencyNodeType
{
    Assembly,
    Namespace,
    Type,
    Method
}

/// <summary>
/// 依赖类型
/// </summary>
public enum DependencyKind
{
    Reference,
    Inheritance,
    Implementation,
    Usage,
    Call
}

#endregion
