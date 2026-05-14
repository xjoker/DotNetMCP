using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

/// <summary>
/// EnhancedSearchService 直接搜索能力测试
/// 覆盖：正则模式、高级语法（+/-）、Token 模式
/// </summary>
public class EnhancedSearchServiceTests
{
    private readonly string _testAssemblyPath;

    public EnhancedSearchServiceTests()
    {
        _testAssemblyPath = typeof(EnhancedSearchServiceTests).Assembly.Location;
    }

    private static async Task<(AssemblyContext ctx, EnhancedSearchService svc)> CreateAsync(string path)
    {
        var ctx = new AssemblyContext(path);
        await ctx.LoadAsync();
        var svc = new EnhancedSearchService(ctx.Mvid);
        return (ctx, svc);
    }

    [Fact]
    public async Task Search_WithRegexPattern_ReturnsMatchingTypes()
    {
        // Arrange: 使用正则 /^.*Tests$/ 匹配以 Tests 结尾的类型
        var (ctx, svc) = await CreateAsync(_testAssemblyPath);
        var module = ctx.Assembly!.MainModule;

        try
        {
            // Act
            var result = svc.Search(module, @"/^.*Tests$/", SearchMode.Type, limit: 200);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Items.Count > 0, "Expected at least one type matching /^.*Tests$/");
            Assert.All(result.Items, item =>
                Assert.True(item.Name.EndsWith("Tests", StringComparison.Ordinal),
                    $"Item '{item.Name}' does not end with 'Tests'"));
        }
        finally
        {
            ctx.Dispose();
        }
    }

    [Fact]
    public async Task Search_WithAdvancedSyntaxExclude_FiltersCorrectly()
    {
        // Arrange: +Search -Service 应包含 Search 但排除 Service
        var (ctx, svc) = await CreateAsync(_testAssemblyPath);
        var module = ctx.Assembly!.MainModule;

        try
        {
            // 先拿全部含 "Search" 的结果
            var allSearch = svc.Search(module, "Search", SearchMode.TypeAndMember, limit: 500);

            // Act: 加上排除 "Service"
            var filtered = svc.Search(module, "+Search -Service", SearchMode.TypeAndMember, limit: 500);

            // Assert: filtered 结果中不应包含名称含 "Service" 的条目
            Assert.All(filtered.Items, item =>
                Assert.DoesNotContain("Service", item.Name, StringComparison.OrdinalIgnoreCase));

            // 过滤后数量 <= 原始数量
            Assert.True(filtered.Items.Count <= allSearch.Items.Count);
        }
        finally
        {
            ctx.Dispose();
        }
    }

    [Fact]
    public async Task Search_WithTokenMode_ReturnsCorrectType()
    {
        // Arrange: 获取测试程序集第一个有效 type 的 metadata token
        var (ctx, svc) = await CreateAsync(_testAssemblyPath);
        var module = ctx.Assembly!.MainModule;

        try
        {
            // 找到第一个非 <Module> 的 type
            var firstType = module.Types.FirstOrDefault(t => t.Name != "<Module>");
            Assert.NotNull(firstType);

            var tokenHex = $"0x{firstType!.MetadataToken.ToUInt32():X8}";

            // Act
            var result = svc.Search(module, tokenHex, SearchMode.Token, limit: 10);

            // Assert
            Assert.True(result.Items.Count > 0, $"Expected a result for token {tokenHex}");
            var found = result.Items.First();
            Assert.Equal(firstType.FullName, found.FullName);
        }
        finally
        {
            ctx.Dispose();
        }
    }
}
