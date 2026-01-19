namespace SampleLib.BasicTypes;

/// <summary>
/// 接口定义
/// </summary>
public interface IProcessor
{
    void Process();
    string GetResult();
}

public interface IAsyncProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken = default);
    Task<string> GetResultAsync();
}

public interface IGenericProcessor<T>
{
    T Process(T input);
    IEnumerable<T> ProcessMany(IEnumerable<T> inputs);
}

/// <summary>
/// 接口实现
/// </summary>
public class StringProcessor : IProcessor, IGenericProcessor<string>
{
    private string _result = string.Empty;

    public void Process()
    {
        _result = "Processed";
    }

    public string GetResult() => _result;

    public string Process(string input) => input.ToUpperInvariant();

    public IEnumerable<string> ProcessMany(IEnumerable<string> inputs)
    {
        return inputs.Select(Process);
    }
}

/// <summary>
/// 异步处理器实现
/// </summary>
public class AsyncProcessor : IAsyncProcessor
{
    private string _result = string.Empty;

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        _result = "Async Processed";
    }

    public async Task<string> GetResultAsync()
    {
        await Task.Yield();
        return _result;
    }
}
