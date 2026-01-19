using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Identity;

/// <summary>
/// 成员 ID 生成器 - 从 Cecil 成员定义生成 MemberId
/// </summary>
public class MemberIdGenerator
{
    private readonly Guid _mvid;

    public MemberIdGenerator(Guid mvid)
    {
        _mvid = mvid;
    }

    /// <summary>
    /// 为类型生成 MemberId
    /// </summary>
    public string GenerateForType(TypeDefinition type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return MemberIdCodec.Encode(_mvid, type.MetadataToken.ToInt32(), MemberKind.Type);
    }

    /// <summary>
    /// 为方法生成 MemberId
    /// </summary>
    public string GenerateForMethod(MethodDefinition method)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method));

        return MemberIdCodec.Encode(_mvid, method.MetadataToken.ToInt32(), MemberKind.Method);
    }

    /// <summary>
    /// 为字段生成 MemberId
    /// </summary>
    public string GenerateForField(FieldDefinition field)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));

        return MemberIdCodec.Encode(_mvid, field.MetadataToken.ToInt32(), MemberKind.Field);
    }

    /// <summary>
    /// 为属性生成 MemberId
    /// </summary>
    public string GenerateForProperty(PropertyDefinition property)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        return MemberIdCodec.Encode(_mvid, property.MetadataToken.ToInt32(), MemberKind.Property);
    }

    /// <summary>
    /// 为事件生成 MemberId
    /// </summary>
    public string GenerateForEvent(EventDefinition evt)
    {
        if (evt == null)
            throw new ArgumentNullException(nameof(evt));

        return MemberIdCodec.Encode(_mvid, evt.MetadataToken.ToInt32(), MemberKind.Event);
    }

    /// <summary>
    /// 为方法中的位置生成 LocationId
    /// </summary>
    public string GenerateLocationId(MethodDefinition method, int ilOffset)
    {
        var memberId = GenerateForMethod(method);
        return LocationIdCodec.Encode(memberId, ilOffset);
    }

    /// <summary>
    /// 根据 IMemberDefinition 自动识别类型并生成 MemberId
    /// </summary>
    public string Generate(IMemberDefinition member)
    {
        return member switch
        {
            TypeDefinition type => GenerateForType(type),
            MethodDefinition method => GenerateForMethod(method),
            FieldDefinition field => GenerateForField(field),
            PropertyDefinition property => GenerateForProperty(property),
            EventDefinition evt => GenerateForEvent(evt),
            _ => throw new ArgumentException($"Unsupported member type: {member.GetType().Name}")
        };
    }
}
