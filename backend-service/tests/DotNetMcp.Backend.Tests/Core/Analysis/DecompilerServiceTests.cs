using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class DecompilerServiceTests
{
    private readonly string _testAssemblyPath;

    public DecompilerServiceTests()
    {
        _testAssemblyPath = typeof(DecompilerServiceTests).Assembly.Location;
    }

    [Fact]
    public async Task DecompileType_ShouldReturnCSharpCode()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var service = new DecompilerService(context);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "DecompilerServiceTests");
        Assert.NotNull(testType);

        // Act
        var result = service.DecompileType(testType.FullName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Code);
        Assert.Contains("class", result.Code);

        context.Dispose();
    }

    [Fact]
    public async Task GetIL_ShouldReturnILCode()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var service = new DecompilerService(context);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "DecompilerServiceTests");
        Assert.NotNull(testType);

        // Act
        var il = service.GetIL(testType);

        // Assert
        Assert.NotNull(il);
        Assert.Contains(".class", il);
        Assert.Contains(".method", il);

        context.Dispose();
    }
}
