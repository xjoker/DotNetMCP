using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;
using System.Collections.Concurrent;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 访问性级别
/// </summary>
public enum Accessibility
{
    Private = 0,
    FamilyAndAssembly = 1,  // protected internal (C# 7.2+)
    Assembly = 2,           // internal
    Family = 3,             // protected
    FamilyOrAssembly = 4,   // protected internal
    Public = 5
}

/// <summary>
/// 作用域限定的交叉引用分析器 - 基于访问性限制搜索范围
/// 参考 dnSpy ScopedWhereUsedAnalyzer 实现
/// </summary>
public class ScopedCrossReferenceAnalyzer
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;
    private readonly ConcurrentDictionary<string, byte> _foundReferences = new();
    private readonly List<ModuleDefinition> _friendAssemblies = new();

    public ScopedCrossReferenceAnalyzer(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
        LoadFriendAssemblies();
    }

    #region 友元程序集支持

    private void LoadFriendAssemblies()
    {
        // 解析 InternalsVisibleTo 属性
        foreach (var attr in _module.Assembly.CustomAttributes)
        {
            if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
            {
                var arg = attr.ConstructorArguments.FirstOrDefault();
                if (arg.Value is string friendName)
                {
                    // 存储友元程序集名称（实际实现中需要解析并加载）
                    // 这里简化处理，记录名称用于后续判断
                }
            }
        }
    }

    #endregion

    #region 访问性计算

    private static Accessibility GetMethodAccessibility(MethodDefinition method)
    {
        if (method == null) return Accessibility.Private;

        return (method.Attributes & MethodAttributes.MemberAccessMask) switch
        {
            MethodAttributes.Private => Accessibility.Private,
            MethodAttributes.FamANDAssem => Accessibility.FamilyAndAssembly,
            MethodAttributes.Assembly => Accessibility.Assembly,
            MethodAttributes.Family => Accessibility.Family,
            MethodAttributes.FamORAssem => Accessibility.FamilyOrAssembly,
            MethodAttributes.Public => Accessibility.Public,
            _ => Accessibility.Private
        };
    }

    private static Accessibility GetFieldAccessibility(FieldDefinition field)
    {
        if (field == null) return Accessibility.Private;

        return (field.Attributes & FieldAttributes.FieldAccessMask) switch
        {
            FieldAttributes.Private => Accessibility.Private,
            FieldAttributes.FamANDAssem => Accessibility.FamilyAndAssembly,
            FieldAttributes.Assembly => Accessibility.Assembly,
            FieldAttributes.Family => Accessibility.Family,
            FieldAttributes.FamORAssem => Accessibility.FamilyOrAssembly,
            FieldAttributes.Public => Accessibility.Public,
            _ => Accessibility.Private
        };
    }

    private static Accessibility GetTypeAccessibility(TypeDefinition type)
    {
        if (type == null) return Accessibility.Private;

        if (type.IsNested)
        {
            return (type.Attributes & TypeAttributes.VisibilityMask) switch
            {
                TypeAttributes.NestedPrivate => Accessibility.Private,
                TypeAttributes.NestedFamANDAssem => Accessibility.FamilyAndAssembly,
                TypeAttributes.NestedAssembly => Accessibility.Assembly,
                TypeAttributes.NestedFamily => Accessibility.Family,
                TypeAttributes.NestedFamORAssem => Accessibility.FamilyOrAssembly,
                TypeAttributes.NestedPublic => Accessibility.Public,
                _ => Accessibility.Private
            };
        }

        return type.IsPublic ? Accessibility.Public : Accessibility.Assembly;
    }

    private Accessibility GetEffectiveAccessibility(IMemberDefinition member)
    {
        Accessibility memberAccess = member switch
        {
            MethodDefinition m => GetMethodAccessibility(m),
            FieldDefinition f => GetFieldAccessibility(f),
            PropertyDefinition p => Math.Max(
                (int)GetMethodAccessibility(p.GetMethod),
                (int)GetMethodAccessibility(p.SetMethod)) switch
            {
                var x => (Accessibility)x
            },
            EventDefinition e => Math.Max(
                (int)GetMethodAccessibility(e.AddMethod),
                (int)GetMethodAccessibility(e.RemoveMethod)) switch
            {
                var x => (Accessibility)x
            },
            TypeDefinition t => GetTypeAccessibility(t),
            _ => Accessibility.Public
        };

        // 考虑声明类型的访问性
        var declaringType = member switch
        {
            MethodDefinition m => m.DeclaringType,
            FieldDefinition f => f.DeclaringType,
            PropertyDefinition p => p.DeclaringType,
            EventDefinition e => e.DeclaringType,
            TypeDefinition t => t.DeclaringType,
            _ => null
        };

        if (declaringType != null)
        {
            var typeAccess = GetTypeAccessibility(declaringType);
            // 成员的有效访问性不能超过其声明类型
            if ((int)typeAccess < (int)memberAccess)
            {
                memberAccess = typeAccess;
            }
        }

        return memberAccess;
    }

    #endregion

    #region 作用域搜索

    private IEnumerable<TypeDefinition> GetSearchScope(Accessibility accessibility, TypeDefinition? declaringType)
    {
        switch (accessibility)
        {
            case Accessibility.Private:
                // 只搜索声明类型及其嵌套类型
                if (declaringType != null)
                {
                    yield return declaringType;
                    foreach (var nested in GetAllNestedTypes(declaringType))
                    {
                        yield return nested;
                    }
                }
                yield break;

            case Accessibility.FamilyAndAssembly:
            case Accessibility.Assembly:
                // 搜索当前程序集所有类型
                foreach (var type in GetAllTypesInModule(_module))
                {
                    yield return type;
                }
                // 搜索友元程序集
                foreach (var friendModule in _friendAssemblies)
                {
                    foreach (var type in GetAllTypesInModule(friendModule))
                    {
                        yield return type;
                    }
                }
                yield break;

            case Accessibility.Family:
                // 搜索派生类型（需要全局搜索）
                foreach (var type in GetAllTypesInModule(_module))
                {
                    yield return type;
                }
                yield break;

            case Accessibility.FamilyOrAssembly:
            case Accessibility.Public:
            default:
                // 全局搜索
                foreach (var type in GetAllTypesInModule(_module))
                {
                    yield return type;
                }
                yield break;
        }
    }

    private static IEnumerable<TypeDefinition> GetAllTypesInModule(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            yield return type;
            foreach (var nested in GetAllNestedTypes(type))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<TypeDefinition> GetAllNestedTypes(TypeDefinition type)
    {
        foreach (var nested in type.NestedTypes)
        {
            yield return nested;
            foreach (var deepNested in GetAllNestedTypes(nested))
            {
                yield return deepNested;
            }
        }
    }

    #endregion

    #region 增强的交叉引用分析

    /// <summary>
    /// 查找所有调用指定方法的位置（带访问性优化和取消支持）
    /// </summary>
    public IReadOnlyList<CrossReference> FindCallsToMethod(
        MethodDefinition targetMethod,
        CancellationToken cancellationToken = default)
    {
        var references = new List<CrossReference>();
        _foundReferences.Clear();

        var accessibility = GetEffectiveAccessibility(targetMethod);
        var searchScope = GetSearchScope(accessibility, targetMethod.DeclaringType);

        var targetName = $"{targetMethod.DeclaringType.FullName}.{targetMethod.Name}";
        var isDllImport = targetMethod.IsPInvokeImpl;
        string? dllImportName = null;
        string? dllImportModule = null;

        // DllImport 特殊处理
        if (isDllImport && targetMethod.PInvokeInfo != null)
        {
            dllImportName = string.IsNullOrEmpty(targetMethod.PInvokeInfo.EntryPoint)
                ? targetMethod.Name
                : targetMethod.PInvokeInfo.EntryPoint;
            dllImportModule = NormalizeModuleName(targetMethod.PInvokeInfo.Module?.Name);
        }

        foreach (var type in searchScope)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call ||
                        instruction.OpCode == OpCodes.Callvirt ||
                        instruction.OpCode == OpCodes.Newobj ||
                        instruction.OpCode == OpCodes.Ldftn ||
                        instruction.OpCode == OpCodes.Ldvirtftn)
                    {
                        if (instruction.Operand is MethodReference methodRef)
                        {
                            bool isMatch = false;
                            var refName = $"{methodRef.DeclaringType.FullName}.{methodRef.Name}";

                            if (refName == targetName)
                            {
                                isMatch = true;
                            }
                            else if (isDllImport && methodRef.Resolve()?.IsPInvokeImpl == true)
                            {
                                // DllImport 方法特殊匹配
                                var refResolved = methodRef.Resolve();
                                if (refResolved?.PInvokeInfo != null)
                                {
                                    var refDllName = string.IsNullOrEmpty(refResolved.PInvokeInfo.EntryPoint)
                                        ? refResolved.Name
                                        : refResolved.PInvokeInfo.EntryPoint;
                                    var refDllModule = NormalizeModuleName(refResolved.PInvokeInfo.Module?.Name);

                                    if (refDllName == dllImportName && refDllModule == dllImportModule)
                                    {
                                        isMatch = true;
                                    }
                                }
                            }

                            if (isMatch)
                            {
                                var refKey = $"{type.FullName}.{method.Name}:{instruction.Offset}";
                                if (_foundReferences.TryAdd(refKey, 0))
                                {
                                    references.Add(CreateReference(
                                        type, method.Name,
                                        ReferenceKind.MethodCall,
                                        targetName,
                                        instruction.Offset
                                    ));
                                }
                            }
                        }
                    }
                }
            }
        }

        return references;
    }

    /// <summary>
    /// 查找字段的所有读写位置（带访问性优化和取消支持）
    /// </summary>
    public IReadOnlyList<CrossReference> FindFieldUsages(
        FieldDefinition targetField,
        CancellationToken cancellationToken = default)
    {
        var references = new List<CrossReference>();
        _foundReferences.Clear();

        var accessibility = GetEffectiveAccessibility(targetField);
        var searchScope = GetSearchScope(accessibility, targetField.DeclaringType);

        var targetName = $"{targetField.DeclaringType.FullName}.{targetField.Name}";

        foreach (var type in searchScope)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    ReferenceKind? kind = null;

                    if (instruction.OpCode == OpCodes.Ldfld || instruction.OpCode == OpCodes.Ldsfld ||
                        instruction.OpCode == OpCodes.Ldflda || instruction.OpCode == OpCodes.Ldsflda)
                    {
                        kind = ReferenceKind.FieldRead;
                    }
                    else if (instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Stsfld)
                    {
                        kind = ReferenceKind.FieldWrite;
                    }

                    if (kind.HasValue && instruction.Operand is FieldReference fieldRef)
                    {
                        var refName = $"{fieldRef.DeclaringType.FullName}.{fieldRef.Name}";
                        if (refName == targetName)
                        {
                            var refKey = $"{type.FullName}.{method.Name}:{instruction.Offset}:{kind}";
                            if (_foundReferences.TryAdd(refKey, 0))
                            {
                                references.Add(CreateReference(
                                    type, method.Name,
                                    kind.Value,
                                    targetName,
                                    instruction.Offset
                                ));
                            }
                        }
                    }
                }
            }
        }

        return references;
    }

    /// <summary>
    /// 查找所有引用指定类型的位置（带访问性优化和取消支持）
    /// </summary>
    public IReadOnlyList<CrossReference> FindReferencesToType(
        TypeDefinition targetType,
        CancellationToken cancellationToken = default)
    {
        var references = new List<CrossReference>();
        _foundReferences.Clear();

        var accessibility = GetEffectiveAccessibility(targetType);
        var searchScope = GetSearchScope(accessibility, targetType.DeclaringType);

        var targetName = targetType.FullName;

        foreach (var type in searchScope)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 检查继承
            if (type.BaseType?.FullName == targetName)
            {
                AddUniqueReference(references, type, null, ReferenceKind.Inheritance, targetName);
            }

            // 检查接口
            foreach (var iface in type.Interfaces)
            {
                if (iface.InterfaceType.FullName == targetName)
                {
                    AddUniqueReference(references, type, null, ReferenceKind.InterfaceImplementation, targetName);
                }
            }

            // 检查字段类型
            foreach (var field in type.Fields)
            {
                if (ContainsType(field.FieldType, targetName))
                {
                    AddUniqueReference(references, type, field.Name, ReferenceKind.FieldType, targetName);
                }
            }

            // 检查方法
            foreach (var method in type.Methods)
            {
                // 返回类型
                if (ContainsType(method.ReturnType, targetName))
                {
                    AddUniqueReference(references, type, method.Name, ReferenceKind.ReturnType, targetName);
                }

                // 参数类型
                foreach (var param in method.Parameters)
                {
                    if (ContainsType(param.ParameterType, targetName))
                    {
                        AddUniqueReference(references, type, method.Name, ReferenceKind.ParameterType, targetName);
                        break;
                    }
                }

                // 局部变量
                if (method.HasBody)
                {
                    foreach (var variable in method.Body.Variables)
                    {
                        if (ContainsType(variable.VariableType, targetName))
                        {
                            AddUniqueReference(references, type, method.Name, ReferenceKind.LocalVariable, targetName);
                            break;
                        }
                    }

                    // 方法体中的类型使用
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.Operand is TypeReference typeRef && typeRef.FullName == targetName)
                        {
                            AddUniqueReference(references, type, method.Name, ReferenceKind.TypeReference, targetName);
                            break;
                        }
                    }
                }
            }
        }

        return references;
    }

    /// <summary>
    /// 查找虚方法的所有重写
    /// </summary>
    public IReadOnlyList<CrossReference> FindMethodOverrides(
        MethodDefinition targetMethod,
        CancellationToken cancellationToken = default)
    {
        var references = new List<CrossReference>();

        if (!targetMethod.IsVirtual && !targetMethod.IsAbstract)
        {
            return references;
        }

        var declaringType = targetMethod.DeclaringType;

        foreach (var type in GetAllTypesInModule(_module))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 检查是否是派生类
            if (!IsSubclassOf(type, declaringType))
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (method.IsVirtual &&
                    method.Name == targetMethod.Name &&
                    HasMatchingSignature(method, targetMethod))
                {
                    references.Add(CreateReference(
                        type, method.Name,
                        ReferenceKind.Override,
                        $"{declaringType.FullName}.{targetMethod.Name}"
                    ));
                }
            }
        }

        return references;
    }

    /// <summary>
    /// 查找接口方法的所有实现
    /// </summary>
    public IReadOnlyList<CrossReference> FindInterfaceImplementations(
        MethodDefinition interfaceMethod,
        CancellationToken cancellationToken = default)
    {
        var references = new List<CrossReference>();

        if (interfaceMethod.DeclaringType?.IsInterface != true)
        {
            return references;
        }

        var interfaceType = interfaceMethod.DeclaringType;

        foreach (var type in GetAllTypesInModule(_module))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 检查是否实现了该接口
            if (!type.Interfaces.Any(i => i.InterfaceType.FullName == interfaceType.FullName))
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                // 显式实现
                if (method.Overrides.Any(o =>
                    o.DeclaringType.FullName == interfaceType.FullName &&
                    o.Name == interfaceMethod.Name))
                {
                    references.Add(CreateReference(
                        type, method.Name,
                        ReferenceKind.InterfaceImplementation,
                        $"{interfaceType.FullName}.{interfaceMethod.Name}"
                    ));
                }
                // 隐式实现
                else if (method.Name == interfaceMethod.Name &&
                         method.IsPublic &&
                         HasMatchingSignature(method, interfaceMethod))
                {
                    references.Add(CreateReference(
                        type, method.Name,
                        ReferenceKind.InterfaceImplementation,
                        $"{interfaceType.FullName}.{interfaceMethod.Name}"
                    ));
                }
            }
        }

        return references;
    }

    #endregion

    #region 辅助方法

    private void AddUniqueReference(
        List<CrossReference> references,
        TypeDefinition sourceType,
        string? sourceMember,
        ReferenceKind kind,
        string targetName,
        int? ilOffset = null)
    {
        var refKey = $"{sourceType.FullName}.{sourceMember}:{kind}:{ilOffset}";
        if (_foundReferences.TryAdd(refKey, 0))
        {
            references.Add(CreateReference(sourceType, sourceMember, kind, targetName, ilOffset));
        }
    }

    private CrossReference CreateReference(
        TypeDefinition sourceType,
        string? sourceMember,
        ReferenceKind kind,
        string targetName,
        int? ilOffset = null)
    {
        return new CrossReference
        {
            SourceTypeId = _idGenerator.GenerateForType(sourceType),
            SourceTypeName = sourceType.FullName,
            SourceMemberName = sourceMember,
            Kind = kind,
            TargetName = targetName,
            ILOffset = ilOffset
        };
    }

    private static bool ContainsType(TypeReference typeRef, string targetName)
    {
        if (typeRef.FullName == targetName) return true;

        if (typeRef is GenericInstanceType genericType)
        {
            foreach (var arg in genericType.GenericArguments)
            {
                if (ContainsType(arg, targetName)) return true;
            }
        }

        if (typeRef is ArrayType arrayType)
        {
            return ContainsType(arrayType.ElementType, targetName);
        }

        if (typeRef is ByReferenceType byRefType)
        {
            return ContainsType(byRefType.ElementType, targetName);
        }

        if (typeRef is PointerType ptrType)
        {
            return ContainsType(ptrType.ElementType, targetName);
        }

        return false;
    }

    private static bool IsSubclassOf(TypeDefinition type, TypeDefinition baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.FullName == baseType.FullName)
            {
                return true;
            }
            current = current.Resolve()?.BaseType;
        }
        return false;
    }

    private static bool HasMatchingSignature(MethodDefinition m1, MethodDefinition m2)
    {
        if (m1.Parameters.Count != m2.Parameters.Count)
            return false;

        for (int i = 0; i < m1.Parameters.Count; i++)
        {
            if (m1.Parameters[i].ParameterType.FullName != m2.Parameters[i].ParameterType.FullName)
                return false;
        }

        return m1.ReturnType.FullName == m2.ReturnType.FullName;
    }

    private static string NormalizeModuleName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        // 移除常见后缀
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);
        else if (name.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 3);
        else if (name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 6);

        // 移除 lib 前缀（Linux）
        if (name.StartsWith("lib", StringComparison.Ordinal))
            name = name.Substring(3);

        return name;
    }

    #endregion
}

// Note: ReferenceKind enum is defined in CrossReferenceAnalyzer.cs
// Extended values (TypeReference, Override) are added there

