using DotNetMcp.Backend.Core.Utils;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Utils;

public class UnityPathDetectorTests : IDisposable
{
    private readonly string _tempRoot;

    public UnityPathDetectorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"UnityDetectorTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── 辅助方法 ──────────────────────────────────────────────────────────

    private static void CreateDll(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
    }

    // ── Windows / Linux 布局测试 ───────────────────────────────────────────

    [Fact]
    public void Detect_WindowsLayout_GameDataManaged_ReturnsSuccess()
    {
        // Arrange: {root}/MyGame_Data/Managed/Assembly-CSharp.dll
        var gameRoot = Path.Combine(_tempRoot, "WindowsGame");
        var dllPath = Path.Combine(gameRoot, "MyGame_Data", "Managed", "Assembly-CSharp.dll");
        CreateDll(dllPath);

        // 额外 dll 以验证 ManagedAssemblies
        var extraDll = Path.Combine(gameRoot, "MyGame_Data", "Managed", "UnityEngine.dll");
        CreateDll(extraDll);

        // Act
        var result = UnityPathDetector.Detect(gameRoot);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(dllPath, result.AssemblyCSharpPath);
        Assert.Equal(Path.Combine(gameRoot, "MyGame_Data", "Managed"), result.ManagedDirectory);
        Assert.Equal("MyGame", result.GameName);
        Assert.Contains(dllPath, result.ManagedAssemblies);
        Assert.Contains(extraDll, result.ManagedAssemblies);
        Assert.Equal(2, result.ManagedAssemblies.Count);
    }

    [Fact]
    public void Detect_WindowsLayout_DataManaged_ReturnsSuccess()
    {
        // Arrange: {root}/Data/Managed/Assembly-CSharp.dll（少数游戏）
        var gameRoot = Path.Combine(_tempRoot, "WindowsGameData");
        var dllPath = Path.Combine(gameRoot, "Data", "Managed", "Assembly-CSharp.dll");
        CreateDll(dllPath);

        // Act
        var result = UnityPathDetector.Detect(gameRoot);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(dllPath, result.AssemblyCSharpPath);
    }

    // ── macOS .app 包探测测试 ──────────────────────────────────────────────

    [Fact]
    public void Detect_MacOsAppBundle_DirectPath_ReturnsSuccess()
    {
        // Arrange: {root}/MyGame.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll
        var appBundle = Path.Combine(_tempRoot, "MyGame.app");
        var dllPath = Path.Combine(appBundle, "Contents", "Resources", "Data", "Managed", "Assembly-CSharp.dll");
        CreateDll(dllPath);

        // Act — 直接传入 .app 路径
        var result = UnityPathDetector.Detect(appBundle);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(dllPath, result.AssemblyCSharpPath);
        Assert.Equal("MyGame", result.GameName);
        Assert.Equal("macos", result.Platform);
    }

    [Fact]
    public void Detect_MacOsAppBundle_ParentDirectory_ReturnsSuccess()
    {
        // Arrange: root 是 .app 的父目录
        var gameRoot = Path.Combine(_tempRoot, "MacParentDir");
        Directory.CreateDirectory(gameRoot);
        var appBundle = Path.Combine(gameRoot, "AnotherGame.app");
        var dllPath = Path.Combine(appBundle, "Contents", "Resources", "Data", "Managed", "Assembly-CSharp.dll");
        CreateDll(dllPath);

        // Act — 传入父目录
        var result = UnityPathDetector.Detect(gameRoot);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(dllPath, result.AssemblyCSharpPath);
    }

    // ── 错误路径测试 ───────────────────────────────────────────────────────

    [Fact]
    public void Detect_NonExistentPath_ReturnsFailure()
    {
        var result = UnityPathDetector.Detect("/tmp/this_path_does_not_exist_12345xyz");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Detect_EmptyPath_ReturnsFailure()
    {
        var result = UnityPathDetector.Detect("");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Detect_DirectoryWithoutUnityStructure_ReturnsFailure()
    {
        // Arrange: 空目录，没有任何 Unity 文件
        var emptyDir = Path.Combine(_tempRoot, "EmptyGame");
        Directory.CreateDirectory(emptyDir);

        var result = UnityPathDetector.Detect(emptyDir);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    // ── DetectAssemblyCSharp 便捷方法测试 ─────────────────────────────────

    [Fact]
    public void DetectAssemblyCSharp_Found_ReturnsPath()
    {
        var gameRoot = Path.Combine(_tempRoot, "SimpleGame");
        var dllPath = Path.Combine(gameRoot, "SimpleGame_Data", "Managed", "Assembly-CSharp.dll");
        CreateDll(dllPath);

        var path = UnityPathDetector.DetectAssemblyCSharp(gameRoot);

        Assert.Equal(dllPath, path);
    }

    [Fact]
    public void DetectAssemblyCSharp_NotFound_ReturnsNull()
    {
        var emptyDir = Path.Combine(_tempRoot, "NotAGame");
        Directory.CreateDirectory(emptyDir);

        var path = UnityPathDetector.DetectAssemblyCSharp(emptyDir);

        Assert.Null(path);
    }

    // ── ManagedAssemblies 列表测试 ────────────────────────────────────────

    [Fact]
    public void Detect_ManagedAssemblies_ContainsAllDlls()
    {
        var gameRoot = Path.Combine(_tempRoot, "MultiDllGame");
        var managedDir = Path.Combine(gameRoot, "MultiDllGame_Data", "Managed");
        var dlls = new[] { "Assembly-CSharp.dll", "UnityEngine.dll", "Newtonsoft.Json.dll" };
        foreach (var dll in dlls)
            CreateDll(Path.Combine(managedDir, dll));

        var result = UnityPathDetector.Detect(gameRoot);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ManagedAssemblies.Count);
        foreach (var dll in dlls)
            Assert.Contains(result.ManagedAssemblies, p => p.EndsWith(dll));
    }

    // ── BFS 回退搜索测试 ──────────────────────────────────────────────────

    [Fact]
    public void Detect_BfsFallback_FindsNestedAssembly()
    {
        // Arrange: 没有标准 _Data 结构，但在深度 3 处存在 dll
        var gameRoot = Path.Combine(_tempRoot, "WeirdLayout");
        var dllPath = Path.Combine(gameRoot, "level1", "level2", "Assembly-CSharp.dll");
        CreateDll(dllPath);

        var result = UnityPathDetector.Detect(gameRoot);

        Assert.True(result.IsSuccess);
        Assert.Equal(dllPath, result.AssemblyCSharpPath);
    }
}
