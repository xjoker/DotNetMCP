namespace DotNetMcp.Backend.Core.Paging;

/// <summary>
/// 切片服务 - 提供数据切片功能
/// 
/// 用于从大型列表中高效提取指定范围的数据，减少内存占用。
/// </summary>
public static class SlicingService
{
    /// <summary>
    /// 切片数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="source">源数据</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="count">数量</param>
    /// <returns>切片结果</returns>
    public static SliceResult<T> Slice<T>(IReadOnlyList<T> source, int offset, int count)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (offset < 0)
        {
            return SliceResult<T>.Error(SliceErrorCode.InvalidOffset, $"Offset cannot be negative: {offset}");
        }

        if (count < 0)
        {
            return SliceResult<T>.Error(SliceErrorCode.InvalidCount, $"Count cannot be negative: {count}");
        }

        if (offset >= source.Count)
        {
            return SliceResult<T>.Success(Array.Empty<T>(), offset, source.Count);
        }

        var actualCount = Math.Min(count, source.Count - offset);
        var items = new List<T>(actualCount);

        for (int i = 0; i < actualCount; i++)
        {
            items.Add(source[offset + i]);
        }

        return SliceResult<T>.Success(items, offset, source.Count);
    }

    /// <summary>
    /// 切片数据（使用 LINQ）
    /// </summary>
    public static SliceResult<T> SliceLinq<T>(IEnumerable<T> source, int offset, int count)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (offset < 0)
        {
            return SliceResult<T>.Error(SliceErrorCode.InvalidOffset, $"Offset cannot be negative: {offset}");
        }

        if (count < 0)
        {
            return SliceResult<T>.Error(SliceErrorCode.InvalidCount, $"Count cannot be negative: {count}");
        }

        var items = source.Skip(offset).Take(count).ToList();
        var totalCount = source is ICollection<T> collection ? collection.Count : (int?)null;

        return SliceResult<T>.Success(items, offset, totalCount);
    }

    /// <summary>
    /// 按范围切片
    /// </summary>
    /// <param name="source">源数据</param>
    /// <param name="start">起始索引（包含）</param>
    /// <param name="end">结束索引（不包含）</param>
    public static SliceResult<T> SliceRange<T>(IReadOnlyList<T> source, int start, int end)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (start < 0 || start > source.Count)
        {
            return SliceResult<T>.Error(SliceErrorCode.InvalidOffset, $"Start index out of range: {start}");
        }

        if (end < start || end > source.Count)
        {
            return SliceResult<T>.Error(SliceErrorCode.InvalidCount, $"End index out of range: {end}");
        }

        var count = end - start;
        return Slice(source, start, count);
    }

    /// <summary>
    /// 批量切片 - 将数据分成多个批次
    /// </summary>
    /// <param name="source">源数据</param>
    /// <param name="batchSize">每批大小</param>
    /// <returns>批次迭代器</returns>
    public static IEnumerable<IReadOnlyList<T>> Batch<T>(IReadOnlyList<T> source, int batchSize)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));

        for (int i = 0; i < source.Count; i += batchSize)
        {
            var count = Math.Min(batchSize, source.Count - i);
            var batch = new List<T>(count);
            
            for (int j = 0; j < count; j++)
            {
                batch.Add(source[i + j]);
            }

            yield return batch;
        }
    }
}

/// <summary>
/// 切片结果
/// </summary>
public record SliceResult<T>
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<T>? Items { get; init; }
    public int Offset { get; init; }
    public int? TotalCount { get; init; }
    public SliceErrorCode? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static SliceResult<T> Success(IReadOnlyList<T> items, int offset, int? totalCount)
        => new()
        {
            IsSuccess = true,
            Items = items,
            Offset = offset,
            TotalCount = totalCount
        };

    public static SliceResult<T> Error(SliceErrorCode errorCode, string message)
        => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message
        };
}

/// <summary>
/// 切片错误码
/// </summary>
public enum SliceErrorCode
{
    InvalidOffset = 1006,
    InvalidCount = 1007
}
