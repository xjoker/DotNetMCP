using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using System.Text;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 依赖图构建器 - 分析程序集和类型之间的依赖关系
/// </summary>
public class DependencyGraphBuilder
{
    /// <summary>
    /// 构建程序集级依赖图
    /// </summary>
    public AssemblyDependencyGraph BuildAssemblyDependencies(AssemblyContext context)
    {
        var assembly = context.Assembly!;
        var graph = new AssemblyDependencyGraph
        {
            RootAssembly = assembly.Name.Name,
            Version = assembly.Name.Version?.ToString() ?? "0.0.0"
        };

        // 收集程序集引用
        foreach (var reference in assembly.MainModule.AssemblyReferences)
        {
            graph.Dependencies.Add(new AssemblyReference
            {
                Name = reference.Name,
                Version = reference.Version?.ToString() ?? "0.0.0",
                IsSystem = IsSystemAssembly(reference.Name)
            });
        }

        // 收集模块引用
        foreach (var moduleRef in assembly.MainModule.ModuleReferences)
        {
            graph.NativeModules.Add(moduleRef.Name);
        }

        return graph;
    }

    /// <summary>
    /// 构建类型级依赖图
    /// </summary>
    public TypeDependencyGraph BuildTypeDependencies(AssemblyContext context, string typeName, int maxDepth = 3)
    {
        var assembly = context.Assembly!;
        var type = FindType(assembly.MainModule, typeName);
        
        if (type == null)
        {
            return new TypeDependencyGraph
            {
                RootType = typeName,
                Error = $"Type '{typeName}' not found"
            };
        }

        var graph = new TypeDependencyGraph
        {
            RootType = type.FullName,
            Namespace = type.Namespace
        };

        var visited = new HashSet<string>();
        CollectTypeDependencies(type, graph, visited, 0, maxDepth);

        return graph;
    }

    private void CollectTypeDependencies(TypeDefinition type, TypeDependencyGraph graph, HashSet<string> visited, int depth, int maxDepth)
    {
        if (depth >= maxDepth || visited.Contains(type.FullName))
            return;

        visited.Add(type.FullName);

        // 基类依赖
        if (type.BaseType != null && type.BaseType.FullName != "System.Object")
        {
            var dep = CreateDependency(type.BaseType, DependencyKind.Inheritance);
            if (!graph.Dependencies.Any(d => d.TargetType == dep.TargetType && d.Kind == dep.Kind))
            {
                graph.Dependencies.Add(dep);
            }
        }

        // 接口依赖
        foreach (var iface in type.Interfaces)
        {
            var dep = CreateDependency(iface.InterfaceType, DependencyKind.Interface);
            if (!graph.Dependencies.Any(d => d.TargetType == dep.TargetType && d.Kind == dep.Kind))
            {
                graph.Dependencies.Add(dep);
            }
        }

        // 字段类型依赖
        foreach (var field in type.Fields)
        {
            var dep = CreateDependency(field.FieldType, DependencyKind.Field);
            if (!IsSystemType(dep.TargetType) && !graph.Dependencies.Any(d => d.TargetType == dep.TargetType && d.Kind == dep.Kind))
            {
                graph.Dependencies.Add(dep);
            }
        }

        // 方法参数和返回类型依赖
        foreach (var method in type.Methods)
        {
            // 返回类型
            if (!method.ReturnType.FullName.StartsWith("System."))
            {
                var dep = CreateDependency(method.ReturnType, DependencyKind.MethodReturn);
                if (!IsSystemType(dep.TargetType) && !graph.Dependencies.Any(d => d.TargetType == dep.TargetType))
                {
                    graph.Dependencies.Add(dep);
                }
            }

            // 参数类型
            foreach (var param in method.Parameters)
            {
                var dep = CreateDependency(param.ParameterType, DependencyKind.MethodParameter);
                if (!IsSystemType(dep.TargetType) && !graph.Dependencies.Any(d => d.TargetType == dep.TargetType))
                {
                    graph.Dependencies.Add(dep);
                }
            }
        }
    }

    private TypeDependency CreateDependency(TypeReference typeRef, DependencyKind kind)
    {
        var resolvedName = GetResolvedTypeName(typeRef);
        return new TypeDependency
        {
            TargetType = resolvedName,
            Kind = kind,
            IsExternal = typeRef.Scope?.Name != typeRef.Module?.Assembly?.Name?.Name
        };
    }

    private string GetResolvedTypeName(TypeReference typeRef)
    {
        // 处理泛型类型
        if (typeRef is GenericInstanceType generic)
        {
            var baseName = generic.ElementType.FullName;
            var args = string.Join(", ", generic.GenericArguments.Select(GetResolvedTypeName));
            return $"{baseName}<{args}>";
        }

        // 处理数组类型
        if (typeRef is ArrayType array)
        {
            return $"{GetResolvedTypeName(array.ElementType)}[]";
        }

        return typeRef.FullName;
    }

    private TypeDefinition? FindType(ModuleDefinition module, string typeName)
    {
        foreach (var type in module.Types)
        {
            if (type.FullName == typeName) return type;
            foreach (var nested in type.NestedTypes)
            {
                if (nested.FullName == typeName) return nested;
            }
        }
        return null;
    }

    private bool IsSystemAssembly(string name)
    {
        return name.StartsWith("System") || 
               name.StartsWith("Microsoft") || 
               name == "mscorlib" || 
               name == "netstandard";
    }

    private bool IsSystemType(string typeName)
    {
        return typeName.StartsWith("System.") || 
               typeName.StartsWith("Microsoft.") ||
               typeName == "System.Object" ||
               typeName == "System.ValueType";
    }

    /// <summary>
    /// 生成程序集依赖的 Mermaid 图
    /// </summary>
    public string GenerateAssemblyMermaid(AssemblyDependencyGraph graph, bool includeSystem = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");
        
        var rootId = SanitizeId(graph.RootAssembly);
        sb.AppendLine($"    {rootId}[\"{graph.RootAssembly}<br/>v{graph.Version}\"]");
        sb.AppendLine($"    style {rootId} fill:#4CAF50,color:#fff");

        foreach (var dep in graph.Dependencies)
        {
            if (!includeSystem && dep.IsSystem) continue;

            var depId = SanitizeId(dep.Name);
            var style = dep.IsSystem ? "fill:#9E9E9E" : "fill:#2196F3";
            sb.AppendLine($"    {depId}[\"{dep.Name}<br/>v{dep.Version}\"]");
            sb.AppendLine($"    style {depId} {style}");
            sb.AppendLine($"    {rootId} --> {depId}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成类型依赖的 Mermaid 图
    /// </summary>
    public string GenerateTypeMermaid(TypeDependencyGraph graph)
    {
        if (!string.IsNullOrEmpty(graph.Error))
        {
            return $"graph TD\n    error[\"Error: {graph.Error}\"]";
        }

        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        var rootId = SanitizeId(graph.RootType);
        sb.AppendLine($"    {rootId}[\"{SimplifyTypeName(graph.RootType)}\"]");
        sb.AppendLine($"    style {rootId} fill:#4CAF50,color:#fff");

        foreach (var dep in graph.Dependencies)
        {
            var depId = SanitizeId(dep.TargetType);
            var arrow = GetArrowStyle(dep.Kind);
            var style = dep.IsExternal ? "fill:#FF9800" : "fill:#2196F3";
            
            sb.AppendLine($"    {depId}[\"{SimplifyTypeName(dep.TargetType)}\"]");
            sb.AppendLine($"    style {depId} {style}");
            sb.AppendLine($"    {rootId} {arrow} {depId}");
        }

        // 添加图例
        sb.AppendLine("    subgraph Legend");
        sb.AppendLine("        L1[\"Inheritance\"] -.->|extends| L2[\" \"]");
        sb.AppendLine("        L3[\"Implementation\"] -.->|implements| L4[\" \"]");
        sb.AppendLine("    end");

        return sb.ToString();
    }

    private string GetArrowStyle(DependencyKind kind)
    {
        return kind switch
        {
            DependencyKind.Inheritance => "-->|extends|",
            DependencyKind.Interface => "-.->|implements|",
            DependencyKind.Field => "-->|field|",
            DependencyKind.MethodReturn => "-->|returns|",
            DependencyKind.MethodParameter => "-->|param|",
            _ => "-->"
        };
    }

    private string SanitizeId(string name)
    {
        return name.Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "");
    }

    private string SimplifyTypeName(string fullName)
    {
        // 移除命名空间，只保留类型名
        var lastDot = fullName.LastIndexOf('.');
        return lastDot > 0 ? fullName[(lastDot + 1)..] : fullName;
    }
}

#region Models

public class AssemblyDependencyGraph
{
    public string RootAssembly { get; set; } = "";
    public string Version { get; set; } = "";
    public List<AssemblyReference> Dependencies { get; set; } = new();
    public List<string> NativeModules { get; set; } = new();
}

public class AssemblyReference
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public bool IsSystem { get; set; }
}

public class TypeDependencyGraph
{
    public string RootType { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string? Error { get; set; }
    public List<TypeDependency> Dependencies { get; set; } = new();
}

public class TypeDependency
{
    public string TargetType { get; set; } = "";
    public DependencyKind Kind { get; set; }
    public bool IsExternal { get; set; }
}

public enum DependencyKind
{
    Inheritance,
    Interface,
    Field,
    MethodReturn,
    MethodParameter,
    LocalVariable
}

#endregion
