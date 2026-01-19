namespace SampleLib.CallGraph;

/// <summary>
/// 调用图测试 - 入口点
/// </summary>
public class EntryPoint
{
    private readonly ServiceA _serviceA;
    private readonly ServiceB _serviceB;

    public EntryPoint()
    {
        _serviceA = new ServiceA();
        _serviceB = new ServiceB();
    }

    public void Main()
    {
        var resultA = _serviceA.Process("input");
        var resultB = _serviceB.Transform(42);
        Console.WriteLine($"Results: {resultA}, {resultB}");
    }

    public async Task MainAsync()
    {
        await _serviceA.ProcessAsync("async input");
        await _serviceB.TransformAsync(100);
    }

    public void CallChainA()
    {
        _serviceA.Level1();
    }

    public void CallChainB()
    {
        _serviceB.Level1();
    }
}

/// <summary>
/// 服务 A - 调用链
/// </summary>
public class ServiceA
{
    private readonly Utility _utility = new();

    public string Process(string input)
    {
        var validated = Validate(input);
        return _utility.Format(validated);
    }

    public async Task<string> ProcessAsync(string input)
    {
        await Task.Delay(10);
        return Process(input);
    }

    private string Validate(string input)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Input cannot be empty");
        return input.Trim();
    }

    public void Level1()
    {
        Level2();
    }

    private void Level2()
    {
        Level3();
    }

    private void Level3()
    {
        _utility.DeepMethod();
    }
}

/// <summary>
/// 服务 B - 调用链
/// </summary>
public class ServiceB
{
    private readonly Utility _utility = new();

    public int Transform(int value)
    {
        var doubled = Double(value);
        return _utility.Compute(doubled);
    }

    public async Task<int> TransformAsync(int value)
    {
        await Task.Delay(10);
        return Transform(value);
    }

    private int Double(int value) => value * 2;

    public void Level1()
    {
        Level2A();
        Level2B();
    }

    private void Level2A()
    {
        _utility.HelperA();
    }

    private void Level2B()
    {
        _utility.HelperB();
    }
}

/// <summary>
/// 工具类 - 被多个服务调用
/// </summary>
public class Utility
{
    public string Format(string input) => input.ToUpperInvariant();

    public int Compute(int value) => value + 10;

    public void DeepMethod()
    {
        Console.WriteLine("Deep method called");
    }

    public void HelperA()
    {
        Console.WriteLine("Helper A");
    }

    public void HelperB()
    {
        Console.WriteLine("Helper B");
    }
}
