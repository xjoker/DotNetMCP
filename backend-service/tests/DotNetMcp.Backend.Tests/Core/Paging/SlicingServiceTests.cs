using DotNetMcp.Backend.Core.Paging;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Paging;

public class SlicingServiceTests
{
    [Fact]
    public void Slice_ValidRange_ShouldReturnCorrectData()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var result = SlicingService.Slice(source, offset: 10, count: 20);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Items);
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(11, result.Items[0]);
        Assert.Equal(30, result.Items[^1]);
        Assert.Equal(10, result.Offset);
        Assert.Equal(100, result.TotalCount);
    }

    [Fact]
    public void Slice_OffsetAtEnd_ShouldReturnEmpty()
    {
        // Arrange
        var source = Enumerable.Range(1, 10).ToList();

        // Act
        var result = SlicingService.Slice(source, offset: 10, count: 5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Slice_CountExceedsRemaining_ShouldReturnRemaining()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var result = SlicingService.Slice(source, offset: 90, count: 50);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Items);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(91, result.Items[0]);
        Assert.Equal(100, result.Items[^1]);
    }

    [Fact]
    public void Slice_NegativeOffset_ShouldReturnError()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var result = SlicingService.Slice(source, offset: -1, count: 10);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(SliceErrorCode.InvalidOffset, result.ErrorCode);
    }

    [Fact]
    public void Slice_NegativeCount_ShouldReturnError()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var result = SlicingService.Slice(source, offset: 0, count: -1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(SliceErrorCode.InvalidCount, result.ErrorCode);
    }

    [Fact]
    public void SliceRange_ValidRange_ShouldReturnCorrectData()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var result = SlicingService.SliceRange(source, start: 10, end: 30);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Items);
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(11, result.Items[0]);
        Assert.Equal(30, result.Items[^1]);
    }

    [Fact]
    public void Batch_ShouldSplitIntoCorrectBatches()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var batches = SlicingService.Batch(source, batchSize: 25).ToList();

        // Assert
        Assert.Equal(4, batches.Count);
        Assert.Equal(25, batches[0].Count);
        Assert.Equal(25, batches[1].Count);
        Assert.Equal(25, batches[2].Count);
        Assert.Equal(25, batches[3].Count);
        Assert.Equal(1, batches[0][0]);
        Assert.Equal(76, batches[3][0]);
    }

    [Fact]
    public void Batch_UnevenSplit_ShouldHandleLastBatch()
    {
        // Arrange
        var source = Enumerable.Range(1, 100).ToList();

        // Act
        var batches = SlicingService.Batch(source, batchSize: 30).ToList();

        // Assert
        Assert.Equal(4, batches.Count);
        Assert.Equal(30, batches[0].Count);
        Assert.Equal(30, batches[1].Count);
        Assert.Equal(30, batches[2].Count);
        Assert.Equal(10, batches[3].Count); // 最后一批只有 10 个
    }
}
