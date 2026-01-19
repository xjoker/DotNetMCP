using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Identity;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class IndexBuilderTests
{
    private readonly string _testAssemblyPath;

    public IndexBuilderTests()
    {
        _testAssemblyPath = typeof(IndexBuilderTests).Assembly.Location;
    }

    [Fact]
    public async Task BuildTypeIndex_ShouldIndexAllPublicTypes()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);

        // Act
        var index = builder.BuildTypeIndex();

        // Assert
        Assert.True(index.Count > 0);
        Assert.NotEmpty(index.Version);

        // Cleanup
        context.Dispose();
    }

    [Fact]
    public async Task BuildTypeIndex_ShouldFindTypeByName()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var index = builder.BuildTypeIndex();

        // Act
        var results = index.FindByName("IndexBuilderTests");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, t => t.Name == "IndexBuilderTests");

        context.Dispose();
    }

    [Fact]
    public async Task BuildTypeIndex_ShouldSearchByKeyword()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var index = builder.BuildTypeIndex();

        // Act
        var results = index.Search("Index");

        // Assert
        Assert.NotEmpty(results);

        context.Dispose();
    }

    [Fact]
    public async Task BuildMemberIndex_ShouldIndexAllMembers()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);

        // Act
        var index = builder.BuildMemberIndex();

        // Assert
        Assert.True(index.Count > 0);

        var stats = index.GetStatsByKind();
        Assert.True(stats.ContainsKey(MemberKind.Method));

        context.Dispose();
    }

    [Fact]
    public async Task BuildMemberIndex_ShouldFindMethodByName()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var index = builder.BuildMemberIndex();

        // Act
        var results = index.FindByName("BuildTypeIndex_ShouldIndexAllPublicTypes");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, m => m.Kind == MemberKind.Method);

        context.Dispose();
    }

    [Fact]
    public async Task BuildFullIndex_ShouldReturnBothIndexes()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);

        // Act
        var (typeIndex, memberIndex) = builder.BuildFullIndex();

        // Assert
        Assert.True(typeIndex.Count > 0);
        Assert.True(memberIndex.Count > 0);
        Assert.Equal(typeIndex.Version, memberIndex.Version);

        context.Dispose();
    }

    [Fact]
    public async Task TypeIndex_GetNamespaces_ShouldReturnAllNamespaces()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var index = builder.BuildTypeIndex();

        // Act
        var namespaces = index.GetNamespaces();

        // Assert
        Assert.NotEmpty(namespaces);
        Assert.Contains(namespaces, ns => ns.Contains("DotNetMcp"));

        context.Dispose();
    }

    [Fact]
    public async Task MemberIndex_GetMembersOfType_ShouldReturnTypeMembers()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var (typeIndex, memberIndex) = builder.BuildFullIndex();

        // 找到一个类型
        var testType = typeIndex.Search("IndexBuilderTests").FirstOrDefault();
        Assert.NotNull(testType);

        // Act
        var members = memberIndex.GetMembersOfType(testType.Id);

        // Assert
        Assert.NotEmpty(members);
        Assert.Contains(members, m => m.Kind == MemberKind.Method);

        context.Dispose();
    }
}
