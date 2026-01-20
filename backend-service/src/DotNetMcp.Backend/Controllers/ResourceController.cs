using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotNetMcp.Backend.Controllers
{
    [ApiController]
    [Route("resources")]
    public class ResourceController : ControllerBase
    {
        private readonly IInstanceRegistry _registry;

        public ResourceController(IInstanceRegistry registry)
        {
            _registry = registry;
        }

        private AssemblyContext? GetContext(string? mvid) => _registry.Get(mvid);

        /// <summary>
        /// List all embedded resources in the assembly
        /// </summary>
        [HttpGet("list")]
        public IActionResult ListResources([FromQuery] string? mvid = null)
        {
            try
            {
                var context = GetContext(mvid);
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
                var resources = service.ListResources();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        resources,
                        total_count = resources.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "OPERATION_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Get resource content by name
        /// </summary>
        [HttpGet("{resourceName}")]
        public IActionResult GetResource(string resourceName, [FromQuery] string? mvid = null, [FromQuery] bool returnBase64 = true)
        {
            try
            {
                var context = GetContext(mvid);
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
                var resource = service.GetResource(resourceName);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        name = resource.Name,
                        size = resource.Size,
                        is_public = resource.IsPublic,
                        content_preview = resource.ContentPreview,
                        content = returnBase64 ? Convert.ToBase64String(resource.Content) : null,
                        content_hex = !returnBase64 ? BitConverter.ToString(resource.Content).Replace("-", "") : null
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "RESOURCE_NOT_FOUND",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "OPERATION_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Add a new embedded resource
        /// </summary>
        [HttpPost("add")]
        public IActionResult AddResource([FromBody] AddResourceRequest request)
        {
            try
            {
                // 验证资源名
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error_code = "INVALID_RESOURCE_NAME",
                        message = "Resource name cannot be empty"
                    });
                }

                var context = GetContext(request.Mvid);
                if (context == null)
                {
                    return Ok(new
                    {
                        success = false,
                        error_code = "NO_CONTEXT",
                        message = "No assembly loaded"
                    });
                }

                var content = request.ContentBase64 != null
                    ? Convert.FromBase64String(request.ContentBase64)
                    : request.ContentText != null
                        ? Encoding.UTF8.GetBytes(request.ContentText)
                        : Array.Empty<byte>();

                var service = new ResourceService(context);
                service.AddResource(request.Name, content, request.IsPublic);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        name = request.Name,
                        size = content.Length,
                        is_public = request.IsPublic
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "RESOURCE_EXISTS",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "OPERATION_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Replace an existing embedded resource
        /// </summary>
        [HttpPut("replace")]
        public IActionResult ReplaceResource([FromBody] ReplaceResourceRequest request)
        {
            try
            {
                var context = GetContext(request.Mvid);
                if (context == null)
                {
                    return Ok(new
                    {
                        success = false,
                        error_code = "NO_CONTEXT",
                        message = "No assembly loaded"
                    });
                }

                var content = request.ContentBase64 != null
                    ? Convert.FromBase64String(request.ContentBase64)
                    : request.ContentText != null
                        ? Encoding.UTF8.GetBytes(request.ContentText)
                        : Array.Empty<byte>();

                var service = new ResourceService(context);
                service.ReplaceResource(request.Name, content, request.IsPublic);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        name = request.Name,
                        size = content.Length,
                        replaced = true
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "RESOURCE_NOT_FOUND",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "OPERATION_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Remove a resource by name
        /// </summary>
        [HttpDelete("{resourceName}")]
        public IActionResult RemoveResource(string resourceName, [FromQuery] string? mvid = null)
        {
            try
            {
                var context = GetContext(mvid);
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
                service.RemoveResource(resourceName);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        name = resourceName,
                        removed = true
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "RESOURCE_NOT_FOUND",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "OPERATION_FAILED",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Export all resources
        /// </summary>
        [HttpPost("export")]
        public IActionResult ExportAllResources([FromQuery] string? mvid = null)
        {
            try
            {
                var context = GetContext(mvid);
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
                var resources = service.ExportAllResources();

                var result = resources.ToDictionary(
                    kvp => kvp.Key,
                    kvp => Convert.ToBase64String(kvp.Value)
                );

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        resources = result,
                        count = result.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error_code = "OPERATION_FAILED",
                    message = ex.Message
                });
            }
        }
    }

    public class AddResourceRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ContentBase64 { get; set; }
        public string? ContentText { get; set; }
        public bool IsPublic { get; set; } = true;
        public string? Mvid { get; set; }
    }

    public class ReplaceResourceRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ContentBase64 { get; set; }
        public string? ContentText { get; set; }
        public bool? IsPublic { get; set; }
        public string? Mvid { get; set; }
    }
}
