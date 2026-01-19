namespace SampleLib.BasicTypes;

/// <summary>
/// 泛型类 - 用于测试泛型分析
/// </summary>
public class GenericClass<T>
{
    private readonly List<T> _items = new();

    public void Add(T item) => _items.Add(item);

    public T Get(int index) => _items[index];

    public int Count => _items.Count;

    public IEnumerable<T> GetAll() => _items;

    public TResult Transform<TResult>(Func<T, TResult> transformer, T item)
    {
        return transformer(item);
    }
}

/// <summary>
/// 多类型参数泛型
/// </summary>
public class GenericPair<TKey, TValue>
{
    public TKey Key { get; set; }
    public TValue Value { get; set; }

    public GenericPair(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }

    public KeyValuePair<TKey, TValue> ToKeyValuePair()
    {
        return new KeyValuePair<TKey, TValue>(Key, Value);
    }
}

/// <summary>
/// 泛型约束示例
/// </summary>
public class GenericWithConstraints<T> where T : class, IDisposable, new()
{
    private T? _instance;

    public T GetOrCreate()
    {
        return _instance ??= new T();
    }

    public void Dispose()
    {
        _instance?.Dispose();
        _instance = null;
    }
}
