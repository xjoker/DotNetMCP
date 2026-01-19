using Mono.Cecil;
using DotNetMcp.Backend.Core.Context;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNetMcp.Backend.Services
{
    /// <summary>
    /// Resource management service for reading and modifying embedded resources
    /// </summary>
    public class ResourceService
    {
        private readonly AssemblyContext _context;

        public ResourceService(AssemblyContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// List all embedded resources in the assembly
        /// </summary>
        public List<ResourceInfo> ListResources()
        {
            var result = new List<ResourceInfo>();

            foreach (var resource in _context.Assembly.MainModule.Resources)
            {
                var info = new ResourceInfo
                {
                    Name = resource.Name,
                    ResourceType = resource.ResourceType.ToString(),
                    IsPublic = resource.IsPublic,
                    IsPrivate = resource.IsPrivate
                };

                if (resource is EmbeddedResource embeddedResource)
                {
                    info.Size = embeddedResource.GetResourceData().Length;
                }

                result.Add(info);
            }

            return result;
        }

        /// <summary>
        /// Get resource content by name
        /// </summary>
        public ResourceData GetResource(string resourceName)
        {
            var resource = _context.Assembly.MainModule.Resources
                .OfType<EmbeddedResource>()
                .FirstOrDefault(r => r.Name == resourceName);

            if (resource == null)
            {
                throw new InvalidOperationException($"Resource '{resourceName}' not found");
            }

            var data = resource.GetResourceData();
            
            return new ResourceData
            {
                Name = resource.Name,
                Content = data,
                Size = data.Length,
                IsPublic = resource.IsPublic,
                ContentPreview = GetContentPreview(data)
            };
        }

        /// <summary>
        /// Add a new embedded resource
        /// </summary>
        public void AddResource(string name, byte[] content, bool isPublic = true)
        {
            // Check if resource already exists
            var existing = _context.Assembly.MainModule.Resources.FirstOrDefault(r => r.Name == name);
            if (existing != null)
            {
                throw new InvalidOperationException($"Resource '{name}' already exists. Use replace instead.");
            }

            var attributes = isPublic 
                ? ManifestResourceAttributes.Public 
                : ManifestResourceAttributes.Private;

            var newResource = new EmbeddedResource(name, attributes, content);
            _context.Assembly.MainModule.Resources.Add(newResource);
        }

        /// <summary>
        /// Replace an existing embedded resource
        /// </summary>
        public void ReplaceResource(string name, byte[] content, bool? isPublic = null)
        {
            var existing = _context.Assembly.MainModule.Resources
                .OfType<EmbeddedResource>()
                .FirstOrDefault(r => r.Name == name);

            if (existing == null)
            {
                throw new InvalidOperationException($"Resource '{name}' not found");
            }

            // Remove old resource
            _context.Assembly.MainModule.Resources.Remove(existing);

            // Add new resource with same or updated attributes
            var attributes = isPublic.HasValue
                ? (isPublic.Value ? ManifestResourceAttributes.Public : ManifestResourceAttributes.Private)
                : existing.Attributes;

            var newResource = new EmbeddedResource(name, attributes, content);
            _context.Assembly.MainModule.Resources.Add(newResource);
        }

        /// <summary>
        /// Remove a resource by name
        /// </summary>
        public void RemoveResource(string name)
        {
            var resource = _context.Assembly.MainModule.Resources.FirstOrDefault(r => r.Name == name);
            
            if (resource == null)
            {
                throw new InvalidOperationException($"Resource '{name}' not found");
            }

            _context.Assembly.MainModule.Resources.Remove(resource);
        }

        /// <summary>
        /// Export all resources to a dictionary
        /// </summary>
        public Dictionary<string, byte[]> ExportAllResources()
        {
            var result = new Dictionary<string, byte[]>();

            foreach (var resource in _context.Assembly.MainModule.Resources.OfType<EmbeddedResource>())
            {
                result[resource.Name] = resource.GetResourceData();
            }

            return result;
        }

        private string? GetContentPreview(byte[] data, int maxLength = 100)
        {
            if (data.Length == 0) return string.Empty;

            try
            {
                // Try to decode as UTF-8 text
                var text = Encoding.UTF8.GetString(data);
                if (text.All(c => !char.IsControl(c) || char.IsWhiteSpace(c)))
                {
                    return text.Length > maxLength 
                        ? text.Substring(0, maxLength) + "..." 
                        : text;
                }
            }
            catch
            {
                // Not text, show hex preview
            }

            // Binary data - show hex preview
            var hexLength = Math.Min(maxLength / 2, data.Length);
            return BitConverter.ToString(data, 0, hexLength).Replace("-", " ");
        }
    }

    public class ResourceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool IsPrivate { get; set; }
        public long Size { get; set; }
    }

    public class ResourceData
    {
        public string Name { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public long Size { get; set; }
        public bool IsPublic { get; set; }
        public string? ContentPreview { get; set; }
    }
}
