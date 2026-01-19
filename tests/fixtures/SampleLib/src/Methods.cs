namespace SampleLib.Methods;

/// <summary>
/// 方法重载 - 用于测试方法签名区分
/// </summary>
public class MethodOverloads
{
    public int Calculate(int a)
    {
        return a * 2;
    }

    public int Calculate(int a, int b)
    {
        return a + b;
    }

    public double Calculate(double a, double b)
    {
        return a + b;
    }

    public string Calculate(string a, string b)
    {
        return a + b;
    }

    public T Calculate<T>(T a, T b) where T : struct
    {
        return a;
    }

    public void Process() { }
    public void Process(string input) { }
    public void Process(string input, int count) { }
    public void Process(int count, string input) { }
    public void Process(params string[] inputs) { }
}

/// <summary>
/// 异步方法
/// </summary>
public class AsyncMethods
{
    public async Task SimpleAsync()
    {
        await Task.Delay(100);
    }

    public async Task<int> ReturnValueAsync()
    {
        await Task.Delay(50);
        return 42;
    }

    public async Task<string> WithCancellationAsync(CancellationToken token)
    {
        await Task.Delay(100, token);
        return "Completed";
    }

    public async IAsyncEnumerable<int> StreamAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(10);
            yield return i;
        }
    }

    public ValueTask<int> ValueTaskMethod()
    {
        return new ValueTask<int>(42);
    }
}

/// <summary>
/// Lambda 和委托
/// </summary>
public class LambdaExamples
{
    public int ProcessWithLambda(int[] numbers)
    {
        return numbers.Where(n => n > 0)
                      .Select(n => n * 2)
                      .Sum();
    }

    public Func<int, int> GetDoubler()
    {
        return x => x * 2;
    }

    public Action<string> GetPrinter()
    {
        return s => Console.WriteLine(s);
    }

    public void UseLocalFunction(int value)
    {
        int LocalDouble(int x) => x * 2;
        int LocalTriple(int x) => x * 3;

        Console.WriteLine(LocalDouble(value));
        Console.WriteLine(LocalTriple(value));
    }
}
