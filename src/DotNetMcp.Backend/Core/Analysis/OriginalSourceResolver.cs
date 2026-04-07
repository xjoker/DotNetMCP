using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Analysis;

/// <summary>
/// 从 Portable PDB 解析原始源码
/// 三级策略：嵌入源码 → 本地文件（哈希校验）→ SourceLink 远程下载
/// </summary>
public sealed class OriginalSourceResolver : IDisposable
{
    private static readonly Guid EmbeddedSourceGuid = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
    private static readonly Guid SourceLinkGuid = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
    private static readonly Guid Md5Guid = new("406EA660-64CF-4C82-B6F0-42D48172A799");
    private static readonly Guid Sha1Guid = new("FF1816EC-AA5E-4D10-87F7-6F4963833460");
    private static readonly Guid Sha256Guid = new("8829D00F-11B8-4213-878B-770E8597AC16");

    private readonly ConcurrentDictionary<string, ResolvedSource?> _cache = new();
    private MetadataReaderProvider? _pdbProvider;
    private MetadataReader? _pdbReader;
    private SourceLinkMap? _sourceLinkMap;
    private bool _initialized;
    private bool _disposed;
    private readonly object _initLock = new();

    /// <summary>
    /// 尝试解析类型的原始源码
    /// </summary>
    public ResolvedSource? TryResolveType(string assemblyPath, string typeName)
    {
        if (string.IsNullOrEmpty(assemblyPath))
            return null;

        EnsureInitialized(assemblyPath);
        if (_pdbReader == null)
            return null;

        return _cache.GetOrAdd(typeName, _ => ResolveTypeSource(typeName));
    }

    private void EnsureInitialized(string assemblyPath)
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                if (!TryOpenPortablePdb(assemblyPath, out var provider, out var reader))
                    return;

                _pdbProvider = provider;
                _pdbReader = reader;
                _sourceLinkMap = ParseSourceLinkMap(reader);
            }
            catch
            {
                // PDB 不可用，静默降级
            }
        }
    }

    private ResolvedSource? ResolveTypeSource(string typeName)
    {
        if (_pdbReader == null) return null;

        // 查找关联的文档
        foreach (var methodDebugHandle in _pdbReader.MethodDebugInformation)
        {
            var debugInfo = _pdbReader.GetMethodDebugInformation(methodDebugHandle);
            if (debugInfo.Document.IsNil) continue;

            var document = _pdbReader.GetDocument(debugInfo.Document);
            var docName = _pdbReader.GetString(document.Name);

            // 尝试匹配类型名到源文件
            var fileName = Path.GetFileNameWithoutExtension(docName);
            var simpleTypeName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;

            if (!string.Equals(fileName, simpleTypeName, StringComparison.OrdinalIgnoreCase))
                continue;

            // 尝试三级策略获取源码
            var source = TryGetEmbeddedSource(debugInfo.Document)
                      ?? TryGetLocalSource(docName, document)
                      ?? TryGetSourceLinkSource(docName, document);

            if (source != null)
                return source;
        }

        return null;
    }

    private ResolvedSource? TryGetEmbeddedSource(DocumentHandle documentHandle)
    {
        if (_pdbReader == null) return null;

        foreach (var debugHandle in _pdbReader.GetCustomDebugInformation(documentHandle))
        {
            var debugInfo = _pdbReader.GetCustomDebugInformation(debugHandle);
            if (debugInfo.Kind.IsNil || debugInfo.Value.IsNil) continue;
            if (_pdbReader.GetGuid(debugInfo.Kind) != EmbeddedSourceGuid) continue;

            var blobReader = _pdbReader.GetBlobReader(debugInfo.Value);
            var format = blobReader.ReadInt32();

            byte[] sourceBytes;
            if (format == 0)
            {
                sourceBytes = blobReader.ReadBytes(blobReader.RemainingBytes);
            }
            else if (format > 0)
            {
                using var compressed = new MemoryStream(blobReader.ReadBytes(blobReader.RemainingBytes));
                using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                using var output = new MemoryStream(format);
                deflate.CopyTo(output);
                sourceBytes = output.ToArray();
            }
            else
            {
                continue;
            }

            return new ResolvedSource
            {
                Code = Encoding.UTF8.GetString(sourceBytes),
                SourceKind = "embedded",
                Language = "C#"
            };
        }

        return null;
    }

    private ResolvedSource? TryGetLocalSource(string documentName, Document document)
    {
        if (_pdbReader == null || !File.Exists(documentName))
            return null;

        var bytes = File.ReadAllBytes(documentName);
        if (!VerifyHash(document, bytes))
            return null;

        return new ResolvedSource
        {
            Code = Encoding.UTF8.GetString(bytes),
            SourceKind = "local",
            SourcePath = documentName,
            Language = "C#"
        };
    }

    private ResolvedSource? TryGetSourceLinkSource(string documentName, Document document)
    {
        if (_sourceLinkMap == null || !_sourceLinkMap.TryResolve(documentName, out var url))
            return null;

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return null;

            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if (!VerifyHash(document, bytes))
                return null;

            return new ResolvedSource
            {
                Code = Encoding.UTF8.GetString(bytes),
                SourceKind = "sourcelink",
                SourcePath = url,
                Language = "C#"
            };
        }
        catch
        {
            return null;
        }
    }

    private bool VerifyHash(Document document, byte[] sourceBytes)
    {
        if (_pdbReader == null || document.Hash.IsNil || document.HashAlgorithm.IsNil)
            return true;

        var expectedHash = _pdbReader.GetBlobBytes(document.Hash);
        if (expectedHash.Length == 0) return true;

        var algorithm = _pdbReader.GetGuid(document.HashAlgorithm);
        byte[]? actualHash = algorithm switch
        {
            var v when v == Md5Guid => MD5.HashData(sourceBytes),
            var v when v == Sha1Guid => SHA1.HashData(sourceBytes),
            var v when v == Sha256Guid => SHA256.HashData(sourceBytes),
            _ => null
        };

        return actualHash == null || actualHash.AsSpan().SequenceEqual(expectedHash);
    }

    private static bool TryOpenPortablePdb(string assemblyPath, out MetadataReaderProvider provider, out MetadataReader reader)
    {
        provider = null!;
        reader = null!;

        // 先尝试嵌入的 PDB
        try
        {
            using var peStream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(peStream);

            var embeddedEntries = peReader.ReadDebugDirectory()
                .Where(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb)
                .ToList();

            if (embeddedEntries.Count > 0)
            {
                provider = peReader.ReadEmbeddedPortablePdbDebugDirectoryData(embeddedEntries[0]);
                reader = provider.GetMetadataReader();
                return true;
            }
        }
        catch
        {
            // 嵌入 PDB 不可用
        }

        // 尝试外部 PDB 文件
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (!File.Exists(pdbPath)) return false;

        try
        {
            var pdbStream = new MemoryStream(File.ReadAllBytes(pdbPath));
            provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            reader = provider.GetMetadataReader();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SourceLinkMap? ParseSourceLinkMap(MetadataReader reader)
    {
        foreach (var handle in reader.CustomDebugInformation)
        {
            var debugInfo = reader.GetCustomDebugInformation(handle);
            if (debugInfo.Kind.IsNil || debugInfo.Value.IsNil) continue;

            var parentKind = debugInfo.Parent.Kind;
            if (parentKind != HandleKind.ModuleDefinition && parentKind != HandleKind.AssemblyDefinition)
                continue;

            if (reader.GetGuid(debugInfo.Kind) != SourceLinkGuid)
                continue;

            var blobReader = reader.GetBlobReader(debugInfo.Value);
            var json = blobReader.ReadUTF8(blobReader.RemainingBytes);
            return SourceLinkMap.Parse(json);
        }

        return null;
    }

    public void ClearCache() => _cache.Clear();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pdbProvider?.Dispose();
        _cache.Clear();
    }
}

/// <summary>
/// 解析后的源码
/// </summary>
public class ResolvedSource
{
    public required string Code { get; set; }
    public required string SourceKind { get; set; }
    public string? SourcePath { get; set; }
    public string Language { get; set; } = "C#";
}

/// <summary>
/// SourceLink URL 映射
/// </summary>
internal sealed class SourceLinkMap
{
    private readonly List<(string prefix, string suffix, string urlPrefix, string urlSuffix, bool hasWildcard)> _entries = new();

    public static SourceLinkMap? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Object)
                return null;

            var map = new SourceLinkMap();
            foreach (var prop in documents.EnumerateObject())
            {
                var pattern = prop.Name.Replace('\\', '/');
                var url = prop.Value.GetString()?.Replace('\\', '/') ?? "";

                var docWild = pattern.IndexOf('*');
                var urlWild = url.IndexOf('*');

                if (docWild >= 0 && urlWild >= 0)
                {
                    map._entries.Add((
                        pattern[..docWild],
                        pattern[(docWild + 1)..],
                        url[..urlWild],
                        url[(urlWild + 1)..],
                        true));
                }
                else
                {
                    map._entries.Add((pattern, "", url, "", false));
                }
            }

            return map._entries.Count > 0 ? map : null;
        }
        catch
        {
            return null;
        }
    }

    public bool TryResolve(string documentName, out string? resolvedUri)
    {
        var normalized = documentName.Replace('\\', '/');

        foreach (var (prefix, suffix, urlPrefix, urlSuffix, hasWildcard) in _entries)
        {
            if (!hasWildcard)
            {
                if (string.Equals(normalized, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedUri = urlPrefix;
                    return true;
                }
                continue;
            }

            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var wildcardLength = normalized.Length - prefix.Length - suffix.Length;
                if (wildcardLength < 0) continue;

                var wildcardValue = normalized.Substring(prefix.Length, wildcardLength);
                resolvedUri = $"{urlPrefix}{wildcardValue}{urlSuffix}";
                return true;
            }
        }

        resolvedUri = null;
        return false;
    }
}
