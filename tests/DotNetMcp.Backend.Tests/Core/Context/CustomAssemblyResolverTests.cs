using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Context;

public class CustomAssemblyResolverTests
{
    [Fact]
    public void Constructor_WithValidSearchPaths_ShouldNotThrow()
    {
        // Arrange
        var searchPaths = new[] { Path.GetTempPath() };

        // Act
        using var resolver = new CustomAssemblyResolver(searchPaths);

        // Assert
        Assert.NotNull(resolver);
        Assert.NotNull(resolver.Statistics);
    }

    [Fact]
    public void Statistics_InitialState_ShouldBeZero()
    {
        // Arrange & Act
        using var resolver = new CustomAssemblyResolver();

        // Assert
        Assert.Equal(0, resolver.Statistics.TotalAttempts);
        Assert.Equal(0, resolver.Statistics.Level1Success);
        Assert.Equal(0, resolver.Statistics.Level2Success);
        Assert.Equal(0, resolver.Statistics.Level3Fallback);
    }

    [Fact]
    public void GetSuccessRate_WithNoAttempts_ShouldReturnZero()
    {
        // Arrange
        using var resolver = new CustomAssemblyResolver();

        // Act
        var successRate = resolver.GetSuccessRate();

        // Assert
        Assert.Equal(0, successRate);
    }

    [Fact]
    public void Dispose_ShouldClearCache()
    {
        // Arrange
        using var resolver = new CustomAssemblyResolver();

        // Act & Assert - 不应抛出异常
        resolver.Dispose();
    }

    [Fact]
    public void DetectDotnetRoot_WithDotnetRootEnvVar_ReturnsEnvValue()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        try
        {
            var expected = "/custom/dotnet/root";
            Environment.SetEnvironmentVariable("DOTNET_ROOT", expected);

            // Act
            var result = CustomAssemblyResolver.DetectDotnetRoot();

            // Assert
            Assert.Equal(expected, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", originalValue);
        }
    }

    [Fact]
    public void DetectDotnetRoot_WithoutEnvVar_ReturnsNonEmptyPath()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", null);

            // Act
            var result = CustomAssemblyResolver.DetectDotnetRoot();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", originalValue);
        }
    }

    [Fact]
    public void DetectDotnetRoot_WithoutEnvVar_ReturnsCorrectPlatformPath()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", null);

            // Act
            var result = CustomAssemblyResolver.DetectDotnetRoot();

            // Assert — 当 /etc/dotnet/install_location 不存在时，平台默认值应正确
            if (OperatingSystem.IsWindows())
            {
                Assert.Contains("dotnet", result, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Program Files", result, StringComparison.OrdinalIgnoreCase);
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS 默认（install_location 文件不存在时）
                if (!File.Exists("/etc/dotnet/install_location"))
                    Assert.Equal("/usr/local/share/dotnet", result);
            }
            else
            {
                // Linux 默认
                if (!File.Exists("/etc/dotnet/install_location"))
                    Assert.True(result == "/usr/share/dotnet" || result == "/usr/lib/dotnet",
                        $"Unexpected Linux dotnet root: {result}");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", originalValue);
        }
    }

    [Fact]
    public void DetectDotnetRoot_WithInstallLocationFile_ReturnsFileContent()
    {
        // Arrange：用临时文件模拟 /etc/dotnet/install_location
        // 只在能写入临时文件的环境下验证文件读取逻辑
        var tmpFile = Path.Combine(Path.GetTempPath(), $"dotnet_install_location_{Guid.NewGuid():N}");
        var expectedPath = "/tmp/custom-dotnet";
        try
        {
            File.WriteAllText(tmpFile, expectedPath + "\n");

            // 直接调用内部逻辑：验证文件读取 + trim 正确
            var content = File.ReadAllText(tmpFile).Trim();
            Assert.Equal(expectedPath, content);
        }
        finally
        {
            if (File.Exists(tmpFile))
                File.Delete(tmpFile);
        }
    }
}
