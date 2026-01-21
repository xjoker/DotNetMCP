using Mono.Cecil;
using Mono.Cecil.Cil;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 交叉引用分析器 - 分析类型和成员之间的引用关系
/// </summary>
public class CrossReferenceAnalyzer
{
    private readonly ModuleDefinition _module;
    private readonly MemberIdGenerator _idGenerator;

    public CrossReferenceAnalyzer(ModuleDefinition module, Guid mvid)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _idGenerator = new MemberIdGenerator(mvid);
    }

    /// <summary>
    /// 查找所有引用指定类型的位置
    /// </summary>
    public IReadOnlyList<CrossReference> FindReferencesToType(TypeDefinition targetType)
    {
        var references = new List<CrossReference>();
        var targetName = targetType.FullName;

        foreach (var type in _module.Types)
        {
            // 检查基类
            if (type.BaseType?.FullName == targetName)
            {
                references.Add(CreateReference(type, null, ReferenceKind.Inheritance, targetName));
            }

            // 检查接口
            foreach (var iface in type.Interfaces)
            {
                if (iface.InterfaceType.FullName == targetName)
                {
                    references.Add(CreateReference(type, null, ReferenceKind.InterfaceImplementation, targetName));
                }
            }

            // 检查字段类型
            foreach (var field in type.Fields)
            {
                if (ContainsType(field.FieldType, targetName))
                {
                    references.Add(CreateReference(type, field.Name, ReferenceKind.FieldType, targetName));
                }
            }

            // 检查方法
            foreach (var method in type.Methods)
            {
                // 返回类型
                if (ContainsType(method.ReturnType, targetName))
                {
                    references.Add(CreateReference(type, method.Name, ReferenceKind.ReturnType, targetName));
                }

                // 参数类型
                foreach (var param in method.Parameters)
                {
                    if (ContainsType(param.ParameterType, targetName))
                    {
                        references.Add(CreateReference(type, method.Name, ReferenceKind.ParameterType, targetName));
                        break;
                    }
                }

                // 局部变量和方法体中的使用
                if (method.HasBody)
                {
                    foreach (var variable in method.Body.Variables)
                    {
                        if (ContainsType(variable.VariableType, targetName))
                        {
                            references.Add(CreateReference(type, method.Name, ReferenceKind.LocalVariable, targetName));
                            break;
                        }
                    }
                }
            }
        }

        return references;
    }

    /// <summary>
    /// 查找所有调用指定方法的位置
    /// </summary>
    public IReadOnlyList<CrossReference> FindCallsToMethod(MethodDefinition targetMethod)
    {
        var references = new List<CrossReference>();
        var targetName = $"{targetMethod.DeclaringType.FullName}.{targetMethod.Name}";

        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call ||
                        instruction.OpCode == OpCodes.Callvirt ||
                        instruction.OpCode == OpCodes.Newobj)
                    {
                        if (instruction.Operand is MethodReference methodRef)
                        {
                            var refName = $"{methodRef.DeclaringType.FullName}.{methodRef.Name}";
                            if (refName == targetName)
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

        return references;
    }

    /// <summary>
    /// 查找字段的所有读写位置
    /// </summary>
    public IReadOnlyList<CrossReference> FindFieldUsages(FieldDefinition targetField)
    {
        var references = new List<CrossReference>();
        var targetName = $"{targetField.DeclaringType.FullName}.{targetField.Name}";

        foreach (var type in _module.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Ldfld ||
                        instruction.OpCode == OpCodes.Ldsfld)
                    {
                        if (instruction.Operand is FieldReference fieldRef)
                        {
                            var refName = $"{fieldRef.DeclaringType.FullName}.{fieldRef.Name}";
                            if (refName == targetName)
                            {
                                references.Add(CreateReference(
                                    type, method.Name,
                                    ReferenceKind.FieldRead,
                                    targetName,
                                    instruction.Offset
                                ));
                            }
                        }
                    }
                    else if (instruction.OpCode == OpCodes.Stfld ||
                             instruction.OpCode == OpCodes.Stsfld)
                    {
                        if (instruction.Operand is FieldReference fieldRef)
                        {
                            var refName = $"{fieldRef.DeclaringType.FullName}.{fieldRef.Name}";
                            if (refName == targetName)
                            {
                                references.Add(CreateReference(
                                    type, method.Name,
                                    ReferenceKind.FieldWrite,
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

        return false;
    }
}

/// <summary>
/// 交叉引用
/// </summary>
public record CrossReference
{
    public required string SourceTypeId { get; init; }
    public required string SourceTypeName { get; init; }
    public string? SourceMemberName { get; init; }
    public required ReferenceKind Kind { get; init; }
    public required string TargetName { get; init; }
    public int? ILOffset { get; init; }
}

/// <summary>
/// 引用类型
/// </summary>
public enum ReferenceKind
{
    Inheritance,
    InterfaceImplementation,
    FieldType,
    ReturnType,
    ParameterType,
    LocalVariable,
    MethodCall,
    FieldRead,
    FieldWrite,
    TypeReference,  // 方法体中的类型引用
    Override        // 虚方法重写
}
