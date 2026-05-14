using Mono.Cecil;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 继承关系分析器 - 分析类型的继承链、派生关系、接口实现和方法覆盖
/// </summary>
public class InheritanceAnalyzer
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    public InheritanceAnalyzer(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 查找一个类型的所有基类链（含接口）
    /// </summary>
    public List<TypeRef> FindBaseTypes(TypeDefinition type, bool includeInterfaces = true)
    {
        var result = new List<TypeRef>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // 遍历基类链
        var current = type.BaseType;
        while (current != null)
        {
            if (!visited.Add(current.FullName)) break;

            TypeDefinition? resolved = null;
            bool isExternal = false;
            try
            {
                resolved = current.Resolve();
                isExternal = resolved == null || resolved.Module != _module;
            }
            catch
            {
                isExternal = true;
            }

            result.Add(new TypeRef
            {
                Id = resolved != null && !isExternal
                    ? _idGenerator.GenerateForType(resolved)
                    : $"ext:{current.FullName}",
                FullName = current.FullName,
                Namespace = current.Namespace,
                Kind = resolved != null ? GetTypeKind(resolved) : "class",
                IsExternal = isExternal
            });

            if (resolved == null) break;
            current = resolved.BaseType;
        }

        // 递归收集接口
        if (includeInterfaces)
        {
            CollectInterfaces(type, result, visited);
        }

        return result;
    }

    private void CollectInterfaces(TypeDefinition type, List<TypeRef> result, HashSet<string> visited)
    {
        foreach (var iface in type.Interfaces)
        {
            var ifaceRef = iface.InterfaceType;
            if (!visited.Add(ifaceRef.FullName)) continue;

            TypeDefinition? resolved = null;
            bool isExternal = false;
            try
            {
                resolved = ifaceRef.Resolve();
                isExternal = resolved == null || resolved.Module != _module;
            }
            catch
            {
                isExternal = true;
            }

            result.Add(new TypeRef
            {
                Id = resolved != null && !isExternal
                    ? _idGenerator.GenerateForType(resolved)
                    : $"ext:{ifaceRef.FullName}",
                FullName = ifaceRef.FullName,
                Namespace = ifaceRef.Namespace,
                Kind = "interface",
                IsExternal = isExternal
            });

            if (resolved != null)
            {
                CollectInterfaces(resolved, result, visited);
            }
        }
    }

    /// <summary>
    /// 查找继承自指定类型的所有派生类型（当前 module 内）
    /// </summary>
    public List<TypeRef> FindDerivedTypes(TypeDefinition baseType, bool directOnly = false)
    {
        var allTypes = GetAllModuleTypes();
        var result = new List<TypeRef>();

        if (directOnly)
        {
            // 仅直接子类
            foreach (var t in allTypes)
            {
                if (t == baseType) continue;
                if (IsDirectSubtype(t, baseType))
                {
                    result.Add(BuildTypeRef(t));
                }
            }
        }
        else
        {
            // 递归：收集所有层级的子类
            var directChildren = new HashSet<TypeDefinition>();
            foreach (var t in allTypes)
            {
                if (t != baseType && IsDirectSubtype(t, baseType))
                    directChildren.Add(t);
            }

            var queue = new Queue<TypeDefinition>(directChildren);
            var visited = new HashSet<string>(StringComparer.Ordinal) { baseType.FullName };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current.FullName)) continue;

                result.Add(BuildTypeRef(current));

                // 找当前类型的直接子类
                foreach (var t in allTypes)
                {
                    if (!visited.Contains(t.FullName) && IsDirectSubtype(t, current))
                        queue.Enqueue(t);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 查找接口的所有实现（当前 module 内）
    /// </summary>
    public List<TypeRef> GetImplementations(TypeDefinition interfaceType)
    {
        var result = new List<TypeRef>();
        var ifaceName = interfaceType.FullName;

        foreach (var t in GetAllModuleTypes())
        {
            if (t == interfaceType) continue;
            if (t.Interfaces.Any(i => i.InterfaceType.FullName == ifaceName))
            {
                result.Add(BuildTypeRef(t));
            }
        }

        return result;
    }

    /// <summary>
    /// 查找方法的所有覆盖（当前 module 内派生类的同名 virtual override）
    /// </summary>
    public List<MethodRef> GetOverrides(MethodDefinition method)
    {
        if (!method.IsVirtual)
            return new List<MethodRef>();

        var result = new List<MethodRef>();
        var declaringType = method.DeclaringType;
        var derivedTypes = FindDerivedTypes(declaringType, directOnly: false);
        var derivedTypeNames = new HashSet<string>(derivedTypes.Select(t => t.FullName), StringComparer.Ordinal);

        foreach (var t in GetAllModuleTypes())
        {
            if (!derivedTypeNames.Contains(t.FullName)) continue;

            foreach (var m in t.Methods)
            {
                if (!m.IsVirtual) continue;
                if (m.Name != method.Name) continue;
                if (m.Parameters.Count != method.Parameters.Count) continue;

                // 检查参数类型签名匹配
                bool paramsMatch = true;
                for (int i = 0; i < m.Parameters.Count; i++)
                {
                    if (m.Parameters[i].ParameterType.FullName != method.Parameters[i].ParameterType.FullName)
                    {
                        paramsMatch = false;
                        break;
                    }
                }

                if (paramsMatch)
                {
                    result.Add(BuildMethodRef(m));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 查找同类型的方法重载（同名但不同参数）
    /// </summary>
    public List<MethodRef> GetOverloads(MethodDefinition method)
    {
        var result = new List<MethodRef>();
        var declaringType = method.DeclaringType;

        foreach (var m in declaringType.Methods)
        {
            if (m == method) continue;
            if (m.Name != method.Name) continue;

            result.Add(BuildMethodRef(m));
        }

        return result;
    }

    // ------- 辅助 -------

    private IEnumerable<TypeDefinition> GetAllModuleTypes()
    {
        foreach (var t in _module.Types)
        {
            yield return t;
            foreach (var nested in GetAllNestedTypes(t))
                yield return nested;
        }
    }

    private static IEnumerable<TypeDefinition> GetAllNestedTypes(TypeDefinition type)
    {
        foreach (var nested in type.NestedTypes)
        {
            yield return nested;
            foreach (var deep in GetAllNestedTypes(nested))
                yield return deep;
        }
    }

    private static bool IsDirectSubtype(TypeDefinition t, TypeDefinition baseType)
    {
        // 检查基类
        if (t.BaseType != null)
        {
            try
            {
                var resolved = t.BaseType.Resolve();
                if (resolved != null && resolved.FullName == baseType.FullName)
                    return true;
            }
            catch { /* 无法 Resolve，跳过 */ }

            // 按名称匹配（处理跨模块情况）
            if (t.BaseType.FullName == baseType.FullName)
                return true;
        }

        // 检查接口（当 baseType 是接口时）
        if (baseType.IsInterface)
        {
            foreach (var iface in t.Interfaces)
            {
                if (iface.InterfaceType.FullName == baseType.FullName)
                    return true;
            }
        }

        return false;
    }

    private TypeRef BuildTypeRef(TypeDefinition type)
    {
        return new TypeRef
        {
            Id = _idGenerator.GenerateForType(type),
            FullName = type.FullName,
            Namespace = string.IsNullOrEmpty(type.Namespace) ? null : type.Namespace,
            Kind = GetTypeKind(type),
            IsExternal = false
        };
    }

    private MethodRef BuildMethodRef(MethodDefinition method)
    {
        var paramStr = string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name));
        return new MethodRef
        {
            Id = _idGenerator.GenerateForMethod(method),
            TypeFullName = method.DeclaringType.FullName,
            MethodName = method.Name,
            Signature = $"{method.ReturnType.Name} {method.Name}({paramStr})"
        };
    }

    private static string GetTypeKind(TypeDefinition type)
    {
        if (type.IsInterface) return "interface";
        if (type.IsEnum) return "enum";
        if (type.IsValueType) return "struct";
        return "class";
    }
}

/// <summary>
/// 类型引用信息
/// </summary>
public record TypeRef
{
    public required string Id { get; init; }
    public required string FullName { get; init; }
    public string? Namespace { get; init; }
    public required string Kind { get; init; } // class/interface/struct/enum
    public bool IsExternal { get; init; }  // 在引用程序集而不在当前 module
}

/// <summary>
/// 方法引用信息
/// </summary>
public record MethodRef
{
    public required string Id { get; init; }
    public required string TypeFullName { get; init; }
    public required string MethodName { get; init; }
    public required string Signature { get; init; }
}
