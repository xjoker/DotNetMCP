using DotNetMcp.Backend.Core.Paging;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Paging;

public class PagingServiceTests
{
    private const string TestVersion = "v1";

    [Fact]
    public void CreatePagedResult_FirstPage_ShouldReturnCorrectData()
    {
        // Arrange
        var service = new PagingService(TestVersion);
        var items = Enumerable.Range(1, 100).ToList();

        // Act
        var result = service.CreatePagedResult(items, cursor: null, limit: 20);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Items);
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(1, result.Items[0]);
        Assert.Equal(20, result.Items[^1]);
        Assert.True(result.HasMore);
        Assert.NotNull(result.Cursor);
        Assert.Equal(100, result.TotalCount);
    }

    [Fact]
    public void CreatePagedResult_LastPage_ShouldNotHaveMore()
    {
        // Arrange
        var service = new PagingService(TestVersion);
        var items = Enumerable.Range(1, 50).ToList();

        // Act
        var result = service.CreatePagedResult(items, cursor: null, limit: 100);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Items);
        Assert.Equal(50, result.Items.Count);
        Assert.False(result.HasMore);
        Assert.Null(result.Cursor);
    }

    [Fact]
    public void CreatePagedResult_WithCursor_ShouldReturnNextPage()
    {
        // Arrange
        var service = new PagingService(TestVersion);
        var items = Enumerable.Range(1, 100).ToList();
        
        // 获取第一页
        var firstPage = service.CreatePagedResult(items, cursor: null, limit: 20);
        Assert.True(firstPage.IsSuccess);

        // Act - 获取第二页
        var secondPage = service.CreatePagedResult(items, firstPage.Cursor, limit: 20);

        // Assert
        Assert.True(secondPage.IsSuccess);
        Assert.NotNull(secondPage.Items);
        Assert.Equal(20, secondPage.Items.Count);
        Assert.Equal(21, secondPage.Items[0]);
        Assert.Equal(40, secondPage.Items[^1]);
    }

    [Fact]
    public void CreatePagedResult_InvalidCursor_ShouldReturnError()
    {
        // Arrange
        var service = new PagingService(TestVersion);
        var items = Enumerable.Range(1, 100).ToList();
        var invalidCursor = "invalid-cursor";

        // Act
        var result = service.CreatePagedResult(items, invalidCursor, limit: 20);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PagingErrorCode.InvalidCursor, result.ErrorCode);
    }

    [Fact]
    public void CreatePagedResult_ExpiredCursor_ShouldReturnError()
    {
        // Arrange
        var service = new PagingService(TestVersion);
        var items = Enumerable.Range(1, 100).ToList();
        
        // 创建一个旧版本的游标
        var oldCursor = CursorCodec.Encode(20, "v0");

        // Act
        var result = service.CreatePagedResult(items, oldCursor, limit: 20);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PagingErrorCode.CursorExpired, result.ErrorCode);
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(10, 10)]
    [InlineData(100, 100)]
    [InlineData(600, 500)] // 超过最大值，应限制为 500
    public void CreatePagedResult_LimitNormalization_ShouldWork(int? inputLimit, int expectedCount)
    {
        // Arrange
        var service = new PagingService(TestVersion);
        var items = Enumerable.Range(1, 1000).ToList();

        // Act
        var result = service.CreatePagedResult(items, cursor: null, limit: inputLimit);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Items!.Count);
    }

    [Fact]
    public void GetDefaultLimit_ShouldReturn50()
    {
        Assert.Equal(50, PagingService.GetDefaultLimit());
    }

    [Fact]
    public void GetMaxLimit_ShouldReturn500()
    {
        Assert.Equal(500, PagingService.GetMaxLimit());
    }
}
