using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Modification;

/// <summary>
/// 类型工厂 - 创建新的类型定义
/// </summary>
public class TypeFactory
{
    private readonly ModuleDefinition _module;

    public TypeFactory(ModuleDefinition module)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
    }

    /// <summary>
    /// 创建类
    /// </summary>
    public TypeDefinition CreateClass(string @namespace, string name, TypeAttributes? attributes = null)
    {
        var attrs = attributes ?? (TypeAttributes.Public | TypeAttributes.Class);
        var baseType = _module.ImportReference(typeof(object));
        return new TypeDefinition(@namespace, name, attrs, baseType);
    }

    /// <summary>
    /// 创建接口
    /// </summary>
    public TypeDefinition CreateInterface(string @namespace, string name)
    {
        var attrs = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
        return new TypeDefinition(@namespace, name, attrs, null);
    }

    /// <summary>
    /// 创建结构体
    /// </summary>
    public TypeDefinition CreateStruct(string @namespace, string name)
    {
        var attrs = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout;
        var baseType = _module.ImportReference(typeof(ValueType));
        return new TypeDefinition(@namespace, name, attrs, baseType);
    }

    /// <summary>
    /// 创建枚举
    /// </summary>
    public TypeDefinition CreateEnum(string @namespace, string name, params (string name, int value)[] values)
    {
        var attrs = TypeAttributes.Public | TypeAttributes.Sealed;
        var baseType = _module.ImportReference(typeof(Enum));
        var enumType = new TypeDefinition(@namespace, name, attrs, baseType);

        // 添加 value__ 字段
        var valueField = new FieldDefinition("value__",
            FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
            _module.ImportReference(typeof(int)));
        enumType.Fields.Add(valueField);

        // 添加枚举成员
        foreach (var (memberName, memberValue) in values)
        {
            var field = new FieldDefinition(memberName,
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
                enumType)
            {
                Constant = memberValue
            };
            enumType.Fields.Add(field);
        }

        return enumType;
    }

    /// <summary>
    /// 创建方法
    /// </summary>
    public MethodDefinition CreateMethod(
        string name,
        TypeReference returnType,
        MethodAttributes? attributes = null,
        params (string name, TypeReference type)[] parameters)
    {
        var attrs = attributes ?? (MethodAttributes.Public);
        var method = new MethodDefinition(name, attrs, returnType);

        foreach (var (paramName, paramType) in parameters)
        {
            method.Parameters.Add(new ParameterDefinition(paramName, ParameterAttributes.None, paramType));
        }

        return method;
    }

    /// <summary>
    /// 创建构造函数
    /// </summary>
    public MethodDefinition CreateConstructor(params (string name, TypeReference type)[] parameters)
    {
        var attrs = MethodAttributes.Public |
                   MethodAttributes.HideBySig |
                   MethodAttributes.SpecialName |
                   MethodAttributes.RTSpecialName;

        var ctor = new MethodDefinition(".ctor", attrs, _module.ImportReference(typeof(void)));

        foreach (var (paramName, paramType) in parameters)
        {
            ctor.Parameters.Add(new ParameterDefinition(paramName, ParameterAttributes.None, paramType));
        }

        return ctor;
    }

    /// <summary>
    /// 创建字段
    /// </summary>
    public FieldDefinition CreateField(string name, TypeReference type, FieldAttributes? attributes = null)
    {
        var attrs = attributes ?? FieldAttributes.Private;
        return new FieldDefinition(name, attrs, type);
    }

    /// <summary>
    /// 创建属性
    /// </summary>
    public PropertyDefinition CreateProperty(string name, TypeReference type)
    {
        return new PropertyDefinition(name, PropertyAttributes.None, type);
    }

    /// <summary>
    /// 创建自动属性（带 getter/setter）
    /// </summary>
    public (PropertyDefinition Property, FieldDefinition BackingField, MethodDefinition Getter, MethodDefinition Setter)
        CreateAutoProperty(TypeDefinition declaringType, string name, TypeReference type, bool isPublic = true)
    {
        // 备份字段
        var backingFieldName = $"<{name}>k__BackingField";
        var backingField = new FieldDefinition(backingFieldName, FieldAttributes.Private, type);

        // Getter
        var getterAttrs = (isPublic ? MethodAttributes.Public : MethodAttributes.Private)
                        | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
        var getter = new MethodDefinition($"get_{name}", getterAttrs, type);

        // Setter
        var setterAttrs = (isPublic ? MethodAttributes.Public : MethodAttributes.Private)
                        | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
        var setter = new MethodDefinition($"set_{name}", setterAttrs, _module.ImportReference(typeof(void)));
        setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, type));

        // 属性
        var property = new PropertyDefinition(name, PropertyAttributes.None, type)
        {
            GetMethod = getter,
            SetMethod = setter
        };

        return (property, backingField, getter, setter);
    }

    /// <summary>
    /// 导入类型引用
    /// </summary>
    public TypeReference ImportType<T>() => _module.ImportReference(typeof(T));

    /// <summary>
    /// 导入类型引用
    /// </summary>
    public TypeReference ImportType(Type type) => _module.ImportReference(type);
}
