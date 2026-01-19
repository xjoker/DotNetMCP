namespace DotNetMcp.Backend.Core.Paging;

/// <summary>
/// 分页服务 - 提供分页遍历功能
/// </summary>
public class PagingService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;
    private readonly string _indexVersion;

    public PagingService(string indexVersion)
    {
        _indexVersion = indexVersion ?? throw new ArgumentNullException(nameof(indexVersion));
    }

    /// <summary>
    /// 创建分页结果
    /// </summary>
    /// <typeparam name="T">数据项类型</typeparam>
    /// <param name="allItems">全部数据</param>
    /// <param name="cursor">游标（可选）</param>
    /// <param name="limit">每页数量</param>
    /// <returns>分页结果</returns>
    public PagedResult<T> CreatePagedResult<T>(IReadOnlyList<T> allItems, string? cursor, int? limit)
    {
        var effectiveLimit = NormalizeLimit(limit);
        var offset = 0;

        // 处理游标
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var validation = CursorCodec.Validate(cursor, _indexVersion);
            if (!validation.IsValid)
            {
                if (validation.IsExpired)
                {
                    return PagedResult<T>.Error(
                        PagingErrorCode.CursorExpired,
                        validation.ErrorMessage ?? "Cursor expired"
                    );
                }
                return PagedResult<T>.Error(
                    PagingErrorCode.InvalidCursor,
                    validation.ErrorMessage ?? "Invalid cursor"
                );
            }

            offset = validation.Data!.Offset;
        }

        // 提取当前页数据
        var items = allItems.Skip(offset).Take(effectiveLimit).ToList();
        var hasMore = offset + effectiveLimit < allItems.Count;

        // 生成下一页游标
        string? nextCursor = null;
        if (hasMore)
        {
            nextCursor = CursorCodec.Encode(offset + effectiveLimit, _indexVersion);
        }

        return PagedResult<T>.Success(items, nextCursor, hasMore, allItems.Count);
    }

    /// <summary>
    /// 创建分页结果（异步版本）
    /// </summary>
    public async Task<PagedResult<T>> CreatePagedResultAsync<T>(
        IAsyncEnumerable<T> items,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var allItems = new List<T>();
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            allItems.Add(item);
        }

        return CreatePagedResult(allItems, cursor, limit);
    }

    /// <summary>
    /// 标准化 limit 参数
    /// </summary>
    private static int NormalizeLimit(int? limit)
    {
        if (limit == null || limit <= 0)
            return DefaultLimit;

        if (limit > MaxLimit)
            return MaxLimit;

        return limit.Value;
    }

    /// <summary>
    /// 获取默认 limit
    /// </summary>
    public static int GetDefaultLimit() => DefaultLimit;

    /// <summary>
    /// 获取最大 limit
    /// </summary>
    public static int GetMaxLimit() => MaxLimit;
}

/// <summary>
/// 分页结果
/// </summary>
public record PagedResult<T>
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<T>? Items { get; init; }
    public string? Cursor { get; init; }
    public bool HasMore { get; init; }
    public int? TotalCount { get; init; }
    public PagingErrorCode? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static PagedResult<T> Success(
        IReadOnlyList<T> items,
        string? cursor,
        bool hasMore,
        int? totalCount = null)
        => new()
        {
            IsSuccess = true,
            Items = items,
            Cursor = cursor,
            HasMore = hasMore,
            TotalCount = totalCount
        };

    public static PagedResult<T> Error(PagingErrorCode errorCode, string message)
        => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = message
        };
}

/// <summary>
/// 分页错误码
/// </summary>
public enum PagingErrorCode
{
    InvalidCursor = 1003,
    CursorExpired = 1004,
    InvalidLimit = 1005
}
