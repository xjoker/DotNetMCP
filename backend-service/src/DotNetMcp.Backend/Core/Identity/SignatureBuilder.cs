using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Identity;

/// <summary>
/// 签名构建器 - 生成类型和方法的规范化签名
/// 
/// 用于处理泛型类型的唯一标识，确保同一泛型实例化具有稳定的 ID。
/// </summary>
public static class SignatureBuilder
{
    /// <summary>
    /// 构建类型签名
    /// </summary>
    /// <param name="typeRef">类型引用</param>
    /// <returns>规范化的类型签名</returns>
    public static string BuildTypeSignature(TypeReference typeRef)
    {
        if (typeRef == null)
            throw new ArgumentNullException(nameof(typeRef));

        var sb = new StringBuilder();
        BuildTypeSignatureCore(typeRef, sb);
        return sb.ToString();
    }

    /// <summary>
    /// 构建方法签名
    /// </summary>
    /// <param name="methodRef">方法引用</param>
    /// <returns>规范化的方法签名</returns>
    public static string BuildMethodSignature(MethodReference methodRef)
    {
        if (methodRef == null)
            throw new ArgumentNullException(nameof(methodRef));

        var sb = new StringBuilder();

        // 返回类型
        BuildTypeSignatureCore(methodRef.ReturnType, sb);
        sb.Append(' ');

        // 方法名
        sb.Append(methodRef.Name);

        // 泛型参数
        if (methodRef is GenericInstanceMethod genericMethod)
        {
            sb.Append('<');
            for (int i = 0; i < genericMethod.GenericArguments.Count; i++)
            {
                if (i > 0) sb.Append(',');
                BuildTypeSignatureCore(genericMethod.GenericArguments[i], sb);
            }
            sb.Append('>');
        }
        else if (methodRef.HasGenericParameters)
        {
            sb.Append('<');
            for (int i = 0; i < methodRef.GenericParameters.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(methodRef.GenericParameters[i].Name);
            }
            sb.Append('>');
        }

        // 参数列表
        sb.Append('(');
        for (int i = 0; i < methodRef.Parameters.Count; i++)
        {
            if (i > 0) sb.Append(',');
            BuildTypeSignatureCore(methodRef.Parameters[i].ParameterType, sb);
        }
        sb.Append(')');

        return sb.ToString();
    }

    /// <summary>
    /// 计算签名的哈希值（用于生成短 ID）
    /// </summary>
    /// <param name="signature">签名字符串</param>
    /// <returns>64位哈希的十六进制表示（16字符）</returns>
    public static string ComputeSignatureHash(string signature)
    {
        var bytes = Encoding.UTF8.GetBytes(signature);
        var hash = XxHash64(bytes);
        return hash.ToString("X16");
    }

    /// <summary>
    /// 为泛型实例化生成唯一 ID 后缀
    /// </summary>
    /// <param name="genericInstance">泛型实例类型</param>
    /// <returns>ID 后缀</returns>
    public static string BuildGenericInstanceSuffix(GenericInstanceType genericInstance)
    {
        var sb = new StringBuilder();
        sb.Append('<');
        for (int i = 0; i < genericInstance.GenericArguments.Count; i++)
        {
            if (i > 0) sb.Append(',');
            BuildTypeSignatureCore(genericInstance.GenericArguments[i], sb);
        }
        sb.Append('>');
        return ComputeSignatureHash(sb.ToString());
    }

    private static void BuildTypeSignatureCore(TypeReference typeRef, StringBuilder sb)
    {
        switch (typeRef)
        {
            case GenericInstanceType genericInstance:
                sb.Append(genericInstance.ElementType.FullName);
                sb.Append('<');
                for (int i = 0; i < genericInstance.GenericArguments.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    BuildTypeSignatureCore(genericInstance.GenericArguments[i], sb);
                }
                sb.Append('>');
                break;

            case GenericParameter genericParam:
                sb.Append(genericParam.Name);
                break;

            case ArrayType arrayType:
                BuildTypeSignatureCore(arrayType.ElementType, sb);
                sb.Append('[');
                for (int i = 1; i < arrayType.Rank; i++) sb.Append(',');
                sb.Append(']');
                break;

            case ByReferenceType byRefType:
                BuildTypeSignatureCore(byRefType.ElementType, sb);
                sb.Append('&');
                break;

            case PointerType pointerType:
                BuildTypeSignatureCore(pointerType.ElementType, sb);
                sb.Append('*');
                break;

            case RequiredModifierType reqModType:
                BuildTypeSignatureCore(reqModType.ElementType, sb);
                sb.Append(" modreq(");
                sb.Append(reqModType.ModifierType.FullName);
                sb.Append(')');
                break;

            case OptionalModifierType optModType:
                BuildTypeSignatureCore(optModType.ElementType, sb);
                sb.Append(" modopt(");
                sb.Append(optModType.ModifierType.FullName);
                sb.Append(')');
                break;

            default:
                sb.Append(typeRef.FullName);
                break;
        }
    }

    /// <summary>
    /// 简化的 xxHash64 实现
    /// </summary>
    private static ulong XxHash64(byte[] data)
    {
        // 使用 .NET 内置的 SHA256 并取前 8 字节作为简化实现
        // 生产环境可替换为完整的 xxHash64 实现
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToUInt64(hash, 0);
    }
}
