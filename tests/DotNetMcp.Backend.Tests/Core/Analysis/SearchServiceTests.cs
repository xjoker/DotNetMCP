using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Identity;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class SearchServiceTests
{
    private readonly string _testAssemblyPath;

    public SearchServiceTests()
    {
        _testAssemblyPath = typeof(SearchServiceTests).Assembly.Location;
    }

    [Fact]
    public async Task Search_ShouldFindTypesAndMembers()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var (typeIndex, memberIndex) = builder.BuildFullIndex();
        var service = new SearchService(typeIndex, memberIndex);

        // Act
        var result = service.Search(new SearchQuery { Keyword = "Search" });

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Types);
        Assert.NotEmpty(result.Members);

        context.Dispose();
    }

    [Fact]
    public async Task SearchTypes_ShouldReturnMatchingTypes()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var (typeIndex, memberIndex) = builder.BuildFullIndex();
        var service = new SearchService(typeIndex, memberIndex);

        // Act
        var results = service.SearchTypes("SearchService");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, t => t.Name.Contains("SearchService"));

        context.Dispose();
    }

    [Fact]
    public async Task GetTypeDetail_ShouldReturnMembersOfType()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new IndexBuilder(context);
        var (typeIndex, memberIndex) = builder.BuildFullIndex();
        var service = new SearchService(typeIndex, memberIndex);

        var testType = typeIndex.Search("SearchServiceTests").FirstOrDefault();
        Assert.NotNull(testType);

        // Act
        var detail = service.GetTypeDetail(testType.Id);

        // Assert
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Methods);

        context.Dispose();
    }
}
