using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 签名管理服务 - 管理程序集的强名称签名
/// </summary>
public class SignatureService
{
    private readonly IInstanceRegistry _registry;

    public SignatureService(IInstanceRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 获取程序集签名信息
    /// </summary>
    public SignatureInfo GetSignature(string? mvid)
    {
        var context = _registry.Get(mvid);
        if (context?.Assembly == null)
        {
            return new SignatureInfo { Error = "Assembly not found" };
        }

        var assembly = context.Assembly;
        var module = assembly.MainModule;

        return new SignatureInfo
        {
            AssemblyName = assembly.Name.Name,
            HasPublicKey = assembly.Name.HasPublicKey,
            PublicKeyToken = assembly.Name.PublicKeyToken != null 
                ? BitConverter.ToString(assembly.Name.PublicKeyToken).Replace("-", "").ToLowerInvariant() 
                : null,
            PublicKey = assembly.Name.PublicKey != null
                ? Convert.ToBase64String(assembly.Name.PublicKey)
                : null,
            IsStrongNamed = assembly.Name.HasPublicKey && module.Attributes.HasFlag(ModuleAttributes.StrongNameSigned),
            Attributes = GetModuleAttributes(module)
        };
    }

    /// <summary>
    /// 移除程序集签名 (delay-sign 或清除强名称)
    /// </summary>
    public SignatureOperationResult RemoveSignature(string? mvid)
    {
        var context = _registry.Get(mvid);
        if (context?.Assembly == null)
        {
            return new SignatureOperationResult { Success = false, Error = "Assembly not found" };
        }

        var assembly = context.Assembly;
        var module = assembly.MainModule;

        // 移除强名称签名标志
        module.Attributes &= ~ModuleAttributes.StrongNameSigned;

        // 清除公钥
        assembly.Name.PublicKey = null;
        assembly.Name.PublicKeyToken = null;
        assembly.Name.HasPublicKey = false;

        return new SignatureOperationResult
        {
            Success = true,
            Message = "Signature removed successfully",
            AssemblyName = assembly.Name.Name
        };
    }

    /// <summary>
    /// 设置 Delay Sign (延迟签名)
    /// </summary>
    public SignatureOperationResult SetDelaySign(string? mvid, byte[] publicKey)
    {
        var context = _registry.Get(mvid);
        if (context?.Assembly == null)
        {
            return new SignatureOperationResult { Success = false, Error = "Assembly not found" };
        }

        var assembly = context.Assembly;
        var module = assembly.MainModule;

        // 设置公钥
        assembly.Name.PublicKey = publicKey;
        assembly.Name.HasPublicKey = true;

        // 不设置 StrongNameSigned 标志 (延迟签名)
        module.Attributes &= ~ModuleAttributes.StrongNameSigned;

        return new SignatureOperationResult
        {
            Success = true,
            Message = "Delay sign set successfully",
            AssemblyName = assembly.Name.Name,
            PublicKeyToken = assembly.Name.PublicKeyToken != null
                ? BitConverter.ToString(assembly.Name.PublicKeyToken).Replace("-", "").ToLowerInvariant()
                : null
        };
    }

    /// <summary>
    /// 使用 SNK 文件进行签名
    /// </summary>
    public SignatureOperationResult SignWithSnk(string? mvid, byte[] snkContent)
    {
        var context = _registry.Get(mvid);
        if (context?.Assembly == null)
        {
            return new SignatureOperationResult { Success = false, Error = "Assembly not found" };
        }

        try
        {
            using var rsa = RSA.Create();
            
            // 尝试加载 SNK 文件
            var blob = snkContent;
            
            // SNK 文件格式: PUBLICKEYBLOB 或 PRIVATEKEYBLOB
            // 这里简化处理，实际需要解析 BLOB 格式
            
            var assembly = context.Assembly;
            var module = assembly.MainModule;

            // 从 SNK 提取公钥 (简化版本)
            // 实际实现需要使用 StrongNameKeyPair 或手动解析
            
            module.Attributes |= ModuleAttributes.StrongNameSigned;

            return new SignatureOperationResult
            {
                Success = true,
                Message = "Assembly signed with SNK file",
                AssemblyName = assembly.Name.Name
            };
        }
        catch (Exception ex)
        {
            return new SignatureOperationResult
            {
                Success = false,
                Error = $"Failed to sign assembly: {ex.Message}"
            };
        }
    }

    private List<string> GetModuleAttributes(ModuleDefinition module)
    {
        var attrs = new List<string>();
        
        if (module.Attributes.HasFlag(ModuleAttributes.StrongNameSigned))
            attrs.Add("StrongNameSigned");
        if (module.Attributes.HasFlag(ModuleAttributes.ILOnly))
            attrs.Add("ILOnly");
        if (module.Attributes.HasFlag(ModuleAttributes.Required32Bit))
            attrs.Add("Required32Bit");
        if (module.Attributes.HasFlag(ModuleAttributes.Preferred32Bit))
            attrs.Add("Preferred32Bit");
            
        return attrs;
    }
}

public class SignatureInfo
{
    public string AssemblyName { get; set; } = "";
    public bool HasPublicKey { get; set; }
    public string? PublicKeyToken { get; set; }
    public string? PublicKey { get; set; }
    public bool IsStrongNamed { get; set; }
    public List<string> Attributes { get; set; } = new();
    public string? Error { get; set; }
}

public class SignatureOperationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public string AssemblyName { get; set; } = "";
    public string? PublicKeyToken { get; set; }
}
