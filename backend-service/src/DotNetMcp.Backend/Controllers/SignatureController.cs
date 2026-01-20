using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 签名 API 控制器 - 管理程序集签名
/// </summary>
[ApiController]
[Route("signature")]
public class SignatureController : ControllerBase
{
    private readonly SignatureService _signatureService;

    public SignatureController(SignatureService signatureService)
    {
        _signatureService = signatureService;
    }

    /// <summary>
    /// 获取程序集签名信息
    /// </summary>
    [HttpGet]
    public IActionResult GetSignature([FromQuery] string? mvid = null)
    {
        var info = _signatureService.GetSignature(mvid);
        
        if (!string.IsNullOrEmpty(info.Error))
        {
            return BadRequest(new { success = false, error_code = "SIGNATURE_ERROR", message = info.Error });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                assembly_name = info.AssemblyName,
                has_public_key = info.HasPublicKey,
                public_key_token = info.PublicKeyToken,
                is_strong_named = info.IsStrongNamed,
                attributes = info.Attributes
            }
        });
    }

    /// <summary>
    /// 移除程序集签名
    /// </summary>
    [HttpPost("remove")]
    public IActionResult RemoveSignature([FromBody] SignatureRequest request)
    {
        var result = _signatureService.RemoveSignature(request.Mvid);
        
        if (!result.Success)
        {
            return BadRequest(new { success = false, error_code = "SIGNATURE_ERROR", message = result.Error });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                assembly_name = result.AssemblyName,
                message = result.Message
            }
        });
    }

    /// <summary>
    /// 设置延迟签名
    /// </summary>
    [HttpPost("delay-sign")]
    public async Task<IActionResult> SetDelaySign([FromForm] DelaySignRequest request)
    {
        if (request.PublicKeyFile == null || request.PublicKeyFile.Length == 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Public key file required" });
        }

        using var stream = new MemoryStream();
        await request.PublicKeyFile.CopyToAsync(stream);
        var publicKey = stream.ToArray();

        var result = _signatureService.SetDelaySign(request.Mvid, publicKey);
        
        if (!result.Success)
        {
            return BadRequest(new { success = false, error_code = "SIGNATURE_ERROR", message = result.Error });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                assembly_name = result.AssemblyName,
                public_key_token = result.PublicKeyToken,
                message = result.Message
            }
        });
    }

    /// <summary>
    /// 使用 SNK 文件签名
    /// </summary>
    [HttpPost("sign")]
    public async Task<IActionResult> SignWithSnk([FromForm] SignRequest request)
    {
        if (request.SnkFile == null || request.SnkFile.Length == 0)
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "SNK file required" });
        }

        using var stream = new MemoryStream();
        await request.SnkFile.CopyToAsync(stream);
        var snkContent = stream.ToArray();

        var result = _signatureService.SignWithSnk(request.Mvid, snkContent);
        
        if (!result.Success)
        {
            return BadRequest(new { success = false, error_code = "SIGNATURE_ERROR", message = result.Error });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                assembly_name = result.AssemblyName,
                message = result.Message
            }
        });
    }
}

public class SignatureRequest
{
    public string? Mvid { get; set; }
}

public class DelaySignRequest
{
    public string? Mvid { get; set; }
    public IFormFile? PublicKeyFile { get; set; }
}

public class SignRequest
{
    public string? Mvid { get; set; }
    public IFormFile? SnkFile { get; set; }
}
