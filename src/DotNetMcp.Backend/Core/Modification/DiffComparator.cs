using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Modification;

/// <summary>
/// 差异对比器 - 对比两个程序集或类型的差异
/// </summary>
public class DiffComparator
{
    /// <summary>
    /// 对比两个程序集
    /// </summary>
    public AssemblyDiff CompareAssemblies(AssemblyDefinition original, AssemblyDefinition modified)
    {
        var diff = new AssemblyDiff
        {
            OriginalName = original.Name.Name,
            ModifiedName = modified.Name.Name,
            TypeDiffs = new List<TypeDiff>()
        };

        var originalTypes = original.MainModule.Types.ToDictionary(t => t.FullName);
        var modifiedTypes = modified.MainModule.Types.ToDictionary(t => t.FullName);

        // 查找新增的类型
        foreach (var type in modifiedTypes.Values)
        {
            if (!originalTypes.ContainsKey(type.FullName))
            {
                diff.TypeDiffs.Add(new TypeDiff
                {
                    TypeName = type.FullName,
                    DiffType = DiffType.Added,
                    MemberDiffs = new List<MemberDiff>()
                });
            }
        }

        // 查找删除的类型
        foreach (var type in originalTypes.Values)
        {
            if (!modifiedTypes.ContainsKey(type.FullName))
            {
                diff.TypeDiffs.Add(new TypeDiff
                {
                    TypeName = type.FullName,
                    DiffType = DiffType.Removed,
                    MemberDiffs = new List<MemberDiff>()
                });
            }
        }

        // 对比相同的类型
        foreach (var originalType in originalTypes.Values)
        {
            if (modifiedTypes.TryGetValue(originalType.FullName, out var modifiedType))
            {
                var typeDiff = CompareTypes(originalType, modifiedType);
                if (typeDiff.MemberDiffs.Count > 0 || typeDiff.DiffType != DiffType.Unchanged)
                {
                    diff.TypeDiffs.Add(typeDiff);
                }
            }
        }

        return diff;
    }

    /// <summary>
    /// 对比两个类型
    /// </summary>
    public TypeDiff CompareTypes(TypeDefinition original, TypeDefinition modified)
    {
        var diff = new TypeDiff
        {
            TypeName = original.FullName,
            DiffType = DiffType.Unchanged,
            MemberDiffs = new List<MemberDiff>()
        };

        // 对比方法
        var originalMethods = original.Methods.ToDictionary(m => GetMethodSignature(m));
        var modifiedMethods = modified.Methods.ToDictionary(m => GetMethodSignature(m));

        foreach (var method in modifiedMethods.Values)
        {
            var sig = GetMethodSignature(method);
            if (!originalMethods.ContainsKey(sig))
            {
                diff.MemberDiffs.Add(new MemberDiff
                {
                    MemberName = method.Name,
                    MemberType = "Method",
                    DiffType = DiffType.Added
                });
            }
        }

        foreach (var method in originalMethods.Values)
        {
            var sig = GetMethodSignature(method);
            if (!modifiedMethods.ContainsKey(sig))
            {
                diff.MemberDiffs.Add(new MemberDiff
                {
                    MemberName = method.Name,
                    MemberType = "Method",
                    DiffType = DiffType.Removed
                });
            }
            else if (HasMethodBodyChanged(method, modifiedMethods[sig]))
            {
                diff.MemberDiffs.Add(new MemberDiff
                {
                    MemberName = method.Name,
                    MemberType = "Method",
                    DiffType = DiffType.Modified
                });
            }
        }

        // 对比字段
        var originalFields = original.Fields.ToDictionary(f => f.Name);
        var modifiedFields = modified.Fields.ToDictionary(f => f.Name);

        foreach (var field in modifiedFields.Values)
        {
            if (!originalFields.ContainsKey(field.Name))
            {
                diff.MemberDiffs.Add(new MemberDiff
                {
                    MemberName = field.Name,
                    MemberType = "Field",
                    DiffType = DiffType.Added
                });
            }
        }

        foreach (var field in originalFields.Values)
        {
            if (!modifiedFields.ContainsKey(field.Name))
            {
                diff.MemberDiffs.Add(new MemberDiff
                {
                    MemberName = field.Name,
                    MemberType = "Field",
                    DiffType = DiffType.Removed
                });
            }
        }

        if (diff.MemberDiffs.Count > 0)
        {
            diff.DiffType = DiffType.Modified;
        }

        return diff;
    }

    private static string GetMethodSignature(MethodDefinition method)
    {
        var parameters = string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName));
        return $"{method.ReturnType.FullName} {method.Name}({parameters})";
    }

    private static bool HasMethodBodyChanged(MethodDefinition original, MethodDefinition modified)
    {
        if (!original.HasBody && !modified.HasBody) return false;
        if (original.HasBody != modified.HasBody) return true;
        if (original.Body.Instructions.Count != modified.Body.Instructions.Count) return true;

        for (int i = 0; i < original.Body.Instructions.Count; i++)
        {
            var origInstr = original.Body.Instructions[i];
            var modInstr = modified.Body.Instructions[i];

            if (origInstr.OpCode != modInstr.OpCode)
                return true;
        }

        return false;
    }
}

/// <summary>
/// 程序集差异
/// </summary>
public class AssemblyDiff
{
    public required string OriginalName { get; set; }
    public required string ModifiedName { get; set; }
    public required List<TypeDiff> TypeDiffs { get; set; }

    public int TotalChanges => TypeDiffs.Sum(t => t.MemberDiffs.Count + (t.DiffType != DiffType.Unchanged ? 1 : 0));
}

/// <summary>
/// 类型差异
/// </summary>
public class TypeDiff
{
    public required string TypeName { get; set; }
    public required DiffType DiffType { get; set; }
    public required List<MemberDiff> MemberDiffs { get; set; }
}

/// <summary>
/// 成员差异
/// </summary>
public class MemberDiff
{
    public required string MemberName { get; set; }
    public required string MemberType { get; set; }
    public required DiffType DiffType { get; set; }
}

/// <summary>
/// 差异类型
/// </summary>
public enum DiffType
{
    Unchanged,
    Added,
    Removed,
    Modified
}
