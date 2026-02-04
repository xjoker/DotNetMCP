using Mono.Cecil;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Identity;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 索引构建器 - 从程序集构建类型和成员索引
/// </summary>
public class IndexBuilder
{
    private readonly AssemblyContext _context;
    private readonly MemberIdGenerator _idGenerator;

    public IndexBuilder(AssemblyContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _idGenerator = new MemberIdGenerator(context.Mvid);
    }

    /// <summary>
    /// 构建类型索引
    /// </summary>
    public TypeIndex BuildTypeIndex()
    {
        var version = _context.Mvid.ToString("N");
        var index = new TypeIndex(version);

        foreach (var type in _context.Assembly.MainModule.Types)
        {
            if (ShouldSkipType(type))
                continue;

            var entry = CreateTypeEntry(type);
            index.Add(entry);

            // 处理嵌套类型
            foreach (var nestedType in type.NestedTypes)
            {
                if (!ShouldSkipType(nestedType))
                {
                    index.Add(CreateTypeEntry(nestedType));
                }
            }
        }

        return index;
    }

    /// <summary>
    /// 构建成员索引
    /// </summary>
    public MemberIndex BuildMemberIndex()
    {
        var version = _context.Mvid.ToString("N");
        var index = new MemberIndex(version);

        foreach (var type in _context.Assembly.MainModule.Types)
        {
            if (ShouldSkipType(type))
                continue;

            var typeId = _idGenerator.GenerateForType(type);
            AddTypeMembers(index, type, typeId);

            // 处理嵌套类型的成员
            foreach (var nestedType in type.NestedTypes)
            {
                if (!ShouldSkipType(nestedType))
                {
                    var nestedTypeId = _idGenerator.GenerateForType(nestedType);
                    AddTypeMembers(index, nestedType, nestedTypeId);
                }
            }
        }

        return index;
    }

    /// <summary>
    /// 构建完整索引（类型 + 成员）
    /// </summary>
    public (TypeIndex TypeIndex, MemberIndex MemberIndex) BuildFullIndex()
    {
        return (BuildTypeIndex(), BuildMemberIndex());
    }

    private void AddTypeMembers(MemberIndex index, TypeDefinition type, string typeId)
    {
        // 方法
        foreach (var method in type.Methods)
        {
            if (method.IsConstructor && method.Name == ".cctor")
                continue; // 跳过静态构造函数

            index.Add(CreateMethodEntry(method, typeId, type.FullName));
        }

        // 字段
        foreach (var field in type.Fields)
        {
            if (field.Name.StartsWith("<"))
                continue; // 跳过编译器生成的字段

            index.Add(CreateFieldEntry(field, typeId, type.FullName));
        }

        // 属性
        foreach (var property in type.Properties)
        {
            index.Add(CreatePropertyEntry(property, typeId, type.FullName));
        }

        // 事件
        foreach (var evt in type.Events)
        {
            index.Add(CreateEventEntry(evt, typeId, type.FullName));
        }
    }

    private TypeIndexEntry CreateTypeEntry(TypeDefinition type)
    {
        return new TypeIndexEntry
        {
            Id = _idGenerator.GenerateForType(type),
            Name = type.Name,
            FullName = type.FullName,
            Namespace = type.Namespace,
            Kind = GetTypeKind(type),
            Visibility = GetTypeVisibility(type),
            IsGeneric = type.HasGenericParameters,
            GenericParameterCount = type.GenericParameters.Count,
            BaseType = type.BaseType?.FullName,
            Interfaces = type.Interfaces.Select(i => i.InterfaceType.FullName).ToList(),
            MethodCount = type.Methods.Count,
            FieldCount = type.Fields.Count,
            PropertyCount = type.Properties.Count
        };
    }

    private MemberIndexEntry CreateMethodEntry(MethodDefinition method, string typeId, string typeName)
    {
        return new MemberIndexEntry
        {
            Id = _idGenerator.GenerateForMethod(method),
            Name = method.Name,
            FullName = $"{typeName}.{method.Name}",
            DeclaringTypeId = typeId,
            DeclaringTypeName = typeName,
            Kind = MemberKind.Method,
            Visibility = GetMethodVisibility(method),
            IsStatic = method.IsStatic,
            IsVirtual = method.IsVirtual,
            IsAbstract = method.IsAbstract,
            ReturnType = method.ReturnType.FullName,
            Parameters = method.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.ParameterType.FullName,
                IsOptional = p.IsOptional
            }).ToList()
        };
    }

    private MemberIndexEntry CreateFieldEntry(FieldDefinition field, string typeId, string typeName)
    {
        return new MemberIndexEntry
        {
            Id = _idGenerator.GenerateForField(field),
            Name = field.Name,
            FullName = $"{typeName}.{field.Name}",
            DeclaringTypeId = typeId,
            DeclaringTypeName = typeName,
            Kind = MemberKind.Field,
            Visibility = GetFieldVisibility(field),
            IsStatic = field.IsStatic,
            IsVirtual = false,
            IsAbstract = false,
            ReturnType = field.FieldType.FullName,
            Parameters = Array.Empty<ParameterInfo>()
        };
    }

    private MemberIndexEntry CreatePropertyEntry(PropertyDefinition property, string typeId, string typeName)
    {
        var getMethod = property.GetMethod;
        return new MemberIndexEntry
        {
            Id = _idGenerator.GenerateForProperty(property),
            Name = property.Name,
            FullName = $"{typeName}.{property.Name}",
            DeclaringTypeId = typeId,
            DeclaringTypeName = typeName,
            Kind = MemberKind.Property,
            Visibility = getMethod != null ? GetMethodVisibility(getMethod) : MemberVisibility.Private,
            IsStatic = getMethod?.IsStatic ?? property.SetMethod?.IsStatic ?? false,
            IsVirtual = getMethod?.IsVirtual ?? false,
            IsAbstract = getMethod?.IsAbstract ?? false,
            ReturnType = property.PropertyType.FullName,
            Parameters = property.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.ParameterType.FullName,
                IsOptional = p.IsOptional
            }).ToList()
        };
    }

    private MemberIndexEntry CreateEventEntry(EventDefinition evt, string typeId, string typeName)
    {
        var addMethod = evt.AddMethod;
        return new MemberIndexEntry
        {
            Id = _idGenerator.GenerateForEvent(evt),
            Name = evt.Name,
            FullName = $"{typeName}.{evt.Name}",
            DeclaringTypeId = typeId,
            DeclaringTypeName = typeName,
            Kind = MemberKind.Event,
            Visibility = addMethod != null ? GetMethodVisibility(addMethod) : MemberVisibility.Private,
            IsStatic = addMethod?.IsStatic ?? false,
            IsVirtual = addMethod?.IsVirtual ?? false,
            IsAbstract = addMethod?.IsAbstract ?? false,
            ReturnType = evt.EventType.FullName,
            Parameters = Array.Empty<ParameterInfo>()
        };
    }

    private static bool ShouldSkipType(TypeDefinition type)
    {
        return type.Name == "<Module>" || 
               type.Name.StartsWith("<") ||
               type.Name.Contains("__");
    }

    private static TypeKind GetTypeKind(TypeDefinition type)
    {
        if (type.IsInterface) return TypeKind.Interface;
        if (type.IsEnum) return TypeKind.Enum;
        if (type.IsValueType) return TypeKind.Struct;
        if (type.BaseType?.FullName == "System.MulticastDelegate") return TypeKind.Delegate;
        return TypeKind.Class;
    }

    private static TypeVisibility GetTypeVisibility(TypeDefinition type)
    {
        if (type.IsPublic || type.IsNestedPublic) return TypeVisibility.Public;
        if (type.IsNestedPrivate) return TypeVisibility.Private;
        if (type.IsNestedFamily) return TypeVisibility.Protected;
        if (type.IsNestedFamilyOrAssembly) return TypeVisibility.ProtectedInternal;
        return TypeVisibility.Internal;
    }

    private static MemberVisibility GetMethodVisibility(MethodDefinition method)
    {
        if (method.IsPublic) return MemberVisibility.Public;
        if (method.IsPrivate) return MemberVisibility.Private;
        if (method.IsFamily) return MemberVisibility.Protected;
        if (method.IsFamilyOrAssembly) return MemberVisibility.ProtectedInternal;
        if (method.IsFamilyAndAssembly) return MemberVisibility.PrivateProtected;
        return MemberVisibility.Internal;
    }

    private static MemberVisibility GetFieldVisibility(FieldDefinition field)
    {
        if (field.IsPublic) return MemberVisibility.Public;
        if (field.IsPrivate) return MemberVisibility.Private;
        if (field.IsFamily) return MemberVisibility.Protected;
        if (field.IsFamilyOrAssembly) return MemberVisibility.ProtectedInternal;
        if (field.IsFamilyAndAssembly) return MemberVisibility.PrivateProtected;
        return MemberVisibility.Internal;
    }
}
