using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Context;

public class AssemblyContextTests
{
    [Fact]
    public async Task LoadAsync_WithValidAssembly_ShouldSucceed()
    {
        // Arrange: 使用当前测试程序集作为有效程序集
        var assemblyPath = typeof(AssemblyContextTests).Assembly.Location;
        var context = new AssemblyContext(assemblyPath);

        // Act
        var result = await context.LoadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.True(context.IsLoaded);
        Assert.NotEqual(Guid.Empty, context.Mvid);
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = "/tmp/non_existent_assembly.dll";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => new AssemblyContext(nonExistentPath));
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AssemblyContext(null!));
    }

    [Fact]
    public async Task GetInfo_AfterLoad_ShouldReturnCorrectInfo()
    {
        // Arrange
        var assemblyPath = typeof(AssemblyContextTests).Assembly.Location;
        var context = new AssemblyContext(assemblyPath);
        await context.LoadAsync();

        // Act
        var info = context.GetInfo();

        // Assert
        Assert.NotNull(info.Name);
        Assert.NotNull(info.FullName);
        Assert.NotNull(info.Version);
        Assert.NotEqual(Guid.Empty.ToString(), info.Mvid);
        Assert.True(info.TypesCount > 0);
        Assert.NotNull(info.Dependencies);
    }

    [Fact]
    public void GetInfo_WithoutLoad_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var assemblyPath = typeof(AssemblyContextTests).Assembly.Location;
        var context = new AssemblyContext(assemblyPath);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => context.GetInfo());
    }

    [Fact]
    public async Task Dispose_ShouldNotThrow()
    {
        // Arrange
        var assemblyPath = typeof(AssemblyContextTests).Assembly.Location;
        var context = new AssemblyContext(assemblyPath);
        await context.LoadAsync();

        // Act & Assert
        context.Dispose();
        context.Dispose(); // 第二次调用不应抛出异常
    }

    [Fact]
    public async Task LoadAsync_MultipleTimes_ShouldReturnSameContext()
    {
        // Arrange
        var assemblyPath = typeof(AssemblyContextTests).Assembly.Location;
        var context = new AssemblyContext(assemblyPath);

        // Act
        var result1 = await context.LoadAsync();
        var result2 = await context.LoadAsync();

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Same(result1.Context, result2.Context);
    }
}
