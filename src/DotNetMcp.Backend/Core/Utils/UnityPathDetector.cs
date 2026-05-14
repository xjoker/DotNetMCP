namespace DotNetMcp.Backend.Core.Utils;

/// <summary>
/// Unity 游戏目录路径探测工具
/// 自动定位 Assembly-CSharp.dll，支持 Windows / macOS / Linux 布局
/// </summary>
public static class UnityPathDetector
{
    private const string AssemblyCSharpDll = "Assembly-CSharp.dll";
    private const int MaxSearchDepth = 4;

    /// <summary>
    /// 从 Unity 游戏目录探测 Assembly-CSharp.dll 路径。
    /// </summary>
    /// <param name="gameRootPath">游戏目录（包含 .exe 或 .app）</param>
    /// <returns>找到的 dll 完整路径，未找到返回 null</returns>
    public static string? DetectAssemblyCSharp(string gameRootPath)
    {
        var result = Detect(gameRootPath);
        return result.IsSuccess ? result.AssemblyCSharpPath : null;
    }

    /// <summary>
    /// 探测 Unity 游戏目录，返回完整探测结果
    /// </summary>
    /// <param name="gameRootPath">游戏目录或 .app 包路径</param>
    public static UnityDetectionResult Detect(string gameRootPath)
    {
        if (string.IsNullOrWhiteSpace(gameRootPath))
            return UnityDetectionResult.Failure("Game root path is null or empty.");

        if (!Directory.Exists(gameRootPath))
            return UnityDetectionResult.Failure($"Directory not found: {gameRootPath}");

        // 步骤1：macOS .app 包直接处理
        if (gameRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return DetectFromAppBundle(gameRootPath);
        }

        // 步骤2：检查目录内是否包含 .app 子目录（root 是 .app 的父目录）
        var appBundles = Directory.GetDirectories(gameRootPath, "*.app", SearchOption.TopDirectoryOnly);
        if (appBundles.Length > 0)
        {
            // 取第一个 .app，通常就是游戏本体
            var bundleResult = DetectFromAppBundle(appBundles[0]);
            if (bundleResult.IsSuccess)
                return bundleResult;
        }

        // 步骤3：Windows / Linux 布局：{root}\{GameName}_Data\Managed\
        var windowsLinuxResult = DetectWindowsLinuxLayout(gameRootPath);
        if (windowsLinuxResult.IsSuccess)
            return windowsLinuxResult;

        // 步骤4：回退——深度受限 BFS 搜索
        return DetectByBfsSearch(gameRootPath);
    }

    // ── macOS .app 包探测 ──────────────────────────────────────────────────

    private static UnityDetectionResult DetectFromAppBundle(string appPath)
    {
        var managedPath = Path.Combine(appPath, "Contents", "Resources", "Data", "Managed");
        var dllPath = Path.Combine(managedPath, AssemblyCSharpDll);

        if (File.Exists(dllPath))
        {
            var gameName = Path.GetFileNameWithoutExtension(appPath);
            return BuildSuccess(dllPath, managedPath, gameName, "macos");
        }

        return UnityDetectionResult.Failure($"Assembly-CSharp.dll not found in macOS .app bundle: {appPath}");
    }

    // ── Windows / Linux 布局探测 ───────────────────────────────────────────

    private static UnityDetectionResult DetectWindowsLinuxLayout(string gameRootPath)
    {
        // 模式1：{root}\{GameName}_Data\Managed\Assembly-CSharp.dll
        var dataDirectories = Directory.GetDirectories(gameRootPath, "*_Data", SearchOption.TopDirectoryOnly);
        foreach (var dataDir in dataDirectories)
        {
            var managedPath = Path.Combine(dataDir, "Managed");
            var dllPath = Path.Combine(managedPath, AssemblyCSharpDll);
            if (File.Exists(dllPath))
            {
                var gameName = Path.GetFileName(dataDir);
                if (gameName.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                    gameName = gameName[..^5]; // 去掉 _Data 后缀
                var platform = DetectOsPlatform();
                return BuildSuccess(dllPath, managedPath, gameName, platform);
            }
        }

        // 模式2：{root}\Data\Managed\Assembly-CSharp.dll（少数游戏）
        {
            var managedPath = Path.Combine(gameRootPath, "Data", "Managed");
            var dllPath = Path.Combine(managedPath, AssemblyCSharpDll);
            if (File.Exists(dllPath))
            {
                var gameName = Path.GetFileName(gameRootPath.TrimEnd(Path.DirectorySeparatorChar,
                                                                       Path.AltDirectorySeparatorChar));
                return BuildSuccess(dllPath, managedPath, gameName, DetectOsPlatform());
            }
        }

        return UnityDetectionResult.Failure("Windows/Linux layout not matched.");
    }

    // ── 深度受限 BFS 回退搜索 ──────────────────────────────────────────────

    private static UnityDetectionResult DetectByBfsSearch(string rootPath)
    {
        var queue = new Queue<(string dir, int depth)>();
        queue.Enqueue((rootPath, 0));

        while (queue.Count > 0)
        {
            var (currentDir, depth) = queue.Dequeue();
            if (depth > MaxSearchDepth)
                continue;

            string targetPath = Path.Combine(currentDir, AssemblyCSharpDll);
            if (File.Exists(targetPath))
            {
                var platform = currentDir.Contains(".app" + Path.DirectorySeparatorChar) ? "macos" : DetectOsPlatform();
                return BuildSuccess(targetPath, currentDir, null, platform);
            }

            try
            {
                foreach (var subDir in Directory.GetDirectories(currentDir))
                    queue.Enqueue((subDir, depth + 1));
            }
            catch (UnauthorizedAccessException) { /* 跳过无权限目录 */ }
            catch (IOException) { /* 跳过无法访问的目录 */ }
        }

        return UnityDetectionResult.Failure($"Assembly-CSharp.dll not found within depth {MaxSearchDepth} of: {rootPath}");
    }

    // ── 辅助方法 ──────────────────────────────────────────────────────────

    private static UnityDetectionResult BuildSuccess(
        string dllPath,
        string managedDir,
        string? gameName,
        string platform)
    {
        var assemblies = new List<string>();
        try
        {
            assemblies.AddRange(Directory.GetFiles(managedDir, "*.dll", SearchOption.TopDirectoryOnly));
        }
        catch (IOException) { /* 无法枚举时保持空列表 */ }

        return new UnityDetectionResult
        {
            IsSuccess = true,
            AssemblyCSharpPath = dllPath,
            ManagedDirectory = managedDir,
            GameName = gameName,
            Platform = platform,
            ManagedAssemblies = assemblies
        };
    }

    private static string DetectOsPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        return "unknown";
    }
}

/// <summary>
/// Unity 路径探测结果
/// </summary>
public record UnityDetectionResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? GameName { get; init; }
    public string? AssemblyCSharpPath { get; init; }
    public string? ManagedDirectory { get; init; }

    /// <summary>
    /// Unity 版本（暂不解析，保留扩展点）
    /// </summary>
    public string? UnityVersion { get; init; }

    /// <summary>
    /// Managed 目录下所有 .dll 路径
    /// </summary>
    public List<string> ManagedAssemblies { get; init; } = new();

    /// <summary>
    /// 平台：windows / macos / linux / unknown
    /// </summary>
    public string Platform { get; init; } = "unknown";

    public static UnityDetectionResult Failure(string msg) =>
        new() { IsSuccess = false, ErrorMessage = msg };
}
