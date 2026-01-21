using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetMcp.Backend.Controllers
{
    [ApiController]
    [Route("transfer")]
    public class TransferController : ControllerBase
    {
        private readonly TransferTokenStore _tokenStore;
        private readonly IInstanceRegistry _registry;

        public TransferController(TransferTokenStore tokenStore, IInstanceRegistry registry)
        {
            _tokenStore = tokenStore;
            _registry = registry;
        }

        private AssemblyContext? GetContext(string? mvid) => _registry.Get(mvid);

        /// <summary>
        /// Create a transfer token (called via MCP tool)
        /// </summary>
        [HttpPost("token/create")]
        public IActionResult CreateToken([FromBody] CreateTokenRequest request)
        {
            try
            {
                var token = _tokenStore.Create(
                    request.Operation,
                    request.ResourceType,
                    request.TimeoutSeconds,
                    request.Mvid,
                    request.ResourceId
                );

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        token = token.TokenId,
                        expires_at = token.ExpiresAt.ToString("O"),
                        expires_in = request.TimeoutSeconds,
                        transfer_url = $"{Request.Scheme}://{Request.Host}/transfer"
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "TOKEN_CREATE_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Revoke a transfer token (called via MCP tool)
        /// </summary>
        [HttpPost("token/revoke")]
        public IActionResult RevokeToken([FromBody] RevokeTokenRequest request)
        {
            var revoked = _tokenStore.Revoke(request.Token);
            return Ok(new
            {
                success = revoked,
                message = revoked ? "Token revoked" : "Token not found"
            });
        }

        /// <summary>
        /// Get token status (called via MCP tool)
        /// </summary>
        [HttpGet("token/status")]
        public IActionResult GetTokenStatus([FromQuery] string token)
        {
            var (exists, used, expiresIn) = _tokenStore.GetStatus(token);
            return Ok(new
            {
                success = true,
                data = new
                {
                    exists,
                    used,
                    expires_in = expiresIn
                }
            });
        }

        /// <summary>
        /// Upload file endpoint
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
        public async Task<IActionResult> Upload(
            [FromHeader(Name = "X-Transfer-Token")] string token,
            IFormFile file,
            [FromForm] string name)
        {
            // Validate token
            var tokenData = _tokenStore.Validate(token);
            if (tokenData == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    error_code = "INVALID_TOKEN",
                    message = "Invalid or expired token"
                });
            }

            if (tokenData.Operation != "upload")
            {
                return StatusCode(403, new
                {
                    success = false,
                    error_code = "FORBIDDEN",
                    message = "Token not authorized for upload"
                });
            }

            try
            {
                // Read file content
                using var stream = file.OpenReadStream();
                var content = new byte[file.Length];
                await stream.ReadExactlyAsync(content);

                // Add resource
                var context = GetContext(tokenData.Mvid);
                if (context == null)
                {
                    return Ok(new
                    {
                        success = false,
                        error_code = "NO_CONTEXT",
                        message = "No assembly loaded"
                    });
                }

                var service = new ResourceService(context);
                service.AddResource(name, content, true);

                // Mark token as used
                _tokenStore.MarkUsed(token);

                // Compress response
                var response = new
                {
                    success = true,
                    data = new
                    {
                        name,
                        size = content.Length,
                        upload_time = DateTime.UtcNow.ToString("O")
                    }
                };

                return CompressedJson(response);
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "UPLOAD_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Download resource endpoint
        /// </summary>
        [HttpGet("download/{resourceName}")]
        public IActionResult Download(
            [FromHeader(Name = "X-Transfer-Token")] string token,
            string resourceName)
        {
            var tokenData = _tokenStore.Validate(token);
            if (tokenData == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    error_code = "INVALID_TOKEN",
                    message = "Invalid or expired token"
                });
            }

            if (tokenData.Operation != "download")
            {
                return StatusCode(403, new
                {
                    success = false,
                    error_code = "FORBIDDEN",
                    message = "Token not authorized for download"
                });
            }

            try
            {
                var context = GetContext(tokenData.Mvid);
                if (context == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error_code = "NO_CONTEXT",
                        message = "No assembly loaded"
                    });
                }

                var service = new ResourceService(context);
                var resource = service.GetResource(resourceName);

                // Compress with zlib
                using var output = new MemoryStream();
                using (var zlib = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    zlib.Write(resource.Content);
                }

                Response.Headers["Content-Encoding"] = "deflate";
                Response.Headers["X-Original-Size"] = resource.Content.Length.ToString();

                _tokenStore.MarkUsed(token);

                return File(output.ToArray(), "application/octet-stream", resourceName);
            }
            catch (Exception ex)
            {
                return NotFound(new
                {
                    success = false,
                    error_code = "DOWNLOAD_FAILED",
                    message = ex.Message
                });
            }
        }

        private IActionResult CompressedJson(object data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            using var output = new MemoryStream();
            using (var zlib = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(bytes);
            }

            Response.Headers["Content-Encoding"] = "deflate";
            Response.Headers["Content-Type"] = "application/json";

            return File(output.ToArray(), "application/json");
        }
    }

    public class CreateTokenRequest
    {
        public string Operation { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 120;
        public string? Mvid { get; set; }
        public string? ResourceId { get; set; }
    }

    public class RevokeTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}
