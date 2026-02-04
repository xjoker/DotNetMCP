using DotNetMcp.Backend.Core.Context;
using Mono.Cecil;
using System.Collections.Concurrent;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 会话事务管理器 - 支持 begin/commit/rollback 操作
/// </summary>
public interface ISessionTransactionManager
{
    /// <summary>开始新事务</summary>
    string BeginTransaction(string mvid);
    
    /// <summary>提交事务</summary>
    bool CommitTransaction(string transactionId);
    
    /// <summary>回滚事务</summary>
    bool RollbackTransaction(string transactionId);
    
    /// <summary>获取事务状态</summary>
    TransactionInfo? GetTransaction(string transactionId);
    
    /// <summary>列出所有活动事务</summary>
    IEnumerable<TransactionInfo> ListTransactions();
}

public class SessionTransactionManager : ISessionTransactionManager
{
    private readonly ConcurrentDictionary<string, TransactionInfo> _transactions = new();
    private readonly ConcurrentDictionary<string, byte[]> _snapshots = new();
    private readonly IInstanceRegistry _registry;

    public SessionTransactionManager(IInstanceRegistry registry)
    {
        _registry = registry;
    }

    public string BeginTransaction(string mvid)
    {
        var context = _registry.Get(mvid);
        if (context?.Assembly == null)
        {
            throw new InvalidOperationException($"Assembly with MVID {mvid} not found");
        }

        var transactionId = GenerateTransactionId();
        
        // 创建快照
        var snapshot = CreateSnapshot(context.Assembly);
        _snapshots[transactionId] = snapshot;

        var transaction = new TransactionInfo
        {
            TransactionId = transactionId,
            Mvid = mvid,
            StartedAt = DateTime.UtcNow,
            Status = TransactionStatus.Active,
            ModificationCount = 0
        };

        _transactions[transactionId] = transaction;
        
        return transactionId;
    }

    public bool CommitTransaction(string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction))
        {
            return false;
        }

        if (transaction.Status != TransactionStatus.Active)
        {
            return false;
        }

        // 提交事务
        transaction.Status = TransactionStatus.Committed;
        transaction.CompletedAt = DateTime.UtcNow;

        // 清理快照
        _snapshots.TryRemove(transactionId, out _);

        return true;
    }

    public bool RollbackTransaction(string transactionId)
    {
        if (!_transactions.TryGetValue(transactionId, out var transaction))
        {
            return false;
        }

        if (transaction.Status != TransactionStatus.Active)
        {
            return false;
        }

        // 恢复快照
        if (_snapshots.TryGetValue(transactionId, out var snapshot))
        {
            RestoreSnapshot(transaction.Mvid, snapshot);
            _snapshots.TryRemove(transactionId, out _);
        }

        transaction.Status = TransactionStatus.RolledBack;
        transaction.CompletedAt = DateTime.UtcNow;

        return true;
    }

    public TransactionInfo? GetTransaction(string transactionId)
    {
        return _transactions.TryGetValue(transactionId, out var transaction) ? transaction : null;
    }

    public IEnumerable<TransactionInfo> ListTransactions()
    {
        return _transactions.Values.OrderByDescending(t => t.StartedAt);
    }

    private string GenerateTransactionId()
    {
        return $"txn_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    }

    private byte[] CreateSnapshot(AssemblyDefinition assembly)
    {
        using var stream = new MemoryStream();
        assembly.Write(stream);
        return stream.ToArray();
    }

    private void RestoreSnapshot(string mvid, byte[] snapshot)
    {
        var context = _registry.Get(mvid);
        if (context == null) return;

        // 从快照恢复程序集
        using var stream = new MemoryStream(snapshot);
        var assembly = AssemblyDefinition.ReadAssembly(stream);
        
        // 更新上下文
        context.Reload(assembly);
    }

    /// <summary>
    /// 清理过期事务 (超过 24 小时)
    /// </summary>
    public void CleanupExpiredTransactions()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var expiredKeys = _transactions
            .Where(kvp => kvp.Value.StartedAt < cutoff && 
                          kvp.Value.Status != TransactionStatus.Active)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _transactions.TryRemove(key, out _);
            _snapshots.TryRemove(key, out _);
        }
    }
}

public class TransactionInfo
{
    public string TransactionId { get; set; } = "";
    public string Mvid { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TransactionStatus Status { get; set; }
    public int ModificationCount { get; set; }
}

public enum TransactionStatus
{
    Active,
    Committed,
    RolledBack
}
