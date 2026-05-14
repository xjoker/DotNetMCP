using System.Text.Json;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// alias 持久化状态
/// </summary>
public class AliasState
{
    public Dictionary<string, AliasEntry> Aliases { get; set; } = new();
}

/// <summary>
/// 单条 alias 记录
/// </summary>
public class AliasEntry
{
    public required string Mvid { get; set; }
    public required string AssemblyPath { get; set; }
    public DateTime RegisteredAt { get; set; }
}

/// <summary>
/// alias 持久化服务 - 将 alias → path 映射原子写入磁盘
/// </summary>
public class AliasPersistence
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 默认路径：{LocalAppData}/dotnet-mcp/aliases.json
    /// </summary>
    public AliasPersistence(string? customPath = null)
    {
        if (customPath != null)
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _filePath = Path.Combine(appData, "dotnet-mcp", "aliases.json");
        }
    }

    /// <summary>
    /// 加载已持久化的 alias 状态；文件不存在时返回空状态
    /// </summary>
    public AliasState Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AliasState();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AliasState>(json) ?? new AliasState();
        }
        catch
        {
            // 文件损坏或格式错误，返回空状态
            return new AliasState();
        }
    }

    /// <summary>
    /// 原子写入 alias 状态（先写 .tmp 再 rename）
    /// </summary>
    public void Save(AliasState state)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var tmpPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(state, _jsonOptions);

        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _filePath, overwrite: true);
    }
}
