using Microsoft.AspNetCore.Mvc;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Backend.Controllers;

/// <summary>
/// 事务 API 控制器 - 管理修改事务
/// </summary>
[ApiController]
[Route("transaction")]
public class TransactionController : ControllerBase
{
    private readonly ISessionTransactionManager _transactionManager;

    public TransactionController(ISessionTransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    /// <summary>
    /// 开始新事务
    /// </summary>
    [HttpPost("begin")]
    public IActionResult BeginTransaction([FromBody] BeginTransactionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mvid))
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "MVID required" });
        }

        try
        {
            var transactionId = _transactionManager.BeginTransaction(request.Mvid);
            return Ok(new
            {
                success = true,
                data = new
                {
                    transaction_id = transactionId,
                    mvid = request.Mvid,
                    message = "Transaction started"
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error_code = "ASSEMBLY_NOT_FOUND", message = ex.Message });
        }
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    [HttpPost("commit")]
    public IActionResult CommitTransaction([FromBody] TransactionIdRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Transaction ID required" });
        }

        var success = _transactionManager.CommitTransaction(request.TransactionId);
        if (!success)
        {
            return BadRequest(new { success = false, error_code = "TRANSACTION_NOT_FOUND", message = "Transaction not found or already completed" });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                transaction_id = request.TransactionId,
                status = "committed",
                message = "Transaction committed successfully"
            }
        });
    }

    /// <summary>
    /// 回滚事务
    /// </summary>
    [HttpPost("rollback")]
    public IActionResult RollbackTransaction([FromBody] TransactionIdRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return BadRequest(new { success = false, error_code = "INVALID_REQUEST", message = "Transaction ID required" });
        }

        var success = _transactionManager.RollbackTransaction(request.TransactionId);
        if (!success)
        {
            return BadRequest(new { success = false, error_code = "TRANSACTION_NOT_FOUND", message = "Transaction not found or already completed" });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                transaction_id = request.TransactionId,
                status = "rolled_back",
                message = "Transaction rolled back successfully"
            }
        });
    }

    /// <summary>
    /// 获取事务状态
    /// </summary>
    [HttpGet("{transactionId}")]
    public IActionResult GetTransaction(string transactionId)
    {
        var transaction = _transactionManager.GetTransaction(transactionId);
        if (transaction == null)
        {
            return NotFound(new { success = false, error_code = "TRANSACTION_NOT_FOUND", message = "Transaction not found" });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                transaction_id = transaction.TransactionId,
                mvid = transaction.Mvid,
                started_at = transaction.StartedAt,
                completed_at = transaction.CompletedAt,
                status = transaction.Status.ToString(),
                modification_count = transaction.ModificationCount
            }
        });
    }

    /// <summary>
    /// 列出所有事务
    /// </summary>
    [HttpGet]
    public IActionResult ListTransactions()
    {
        var transactions = _transactionManager.ListTransactions();
        return Ok(new
        {
            success = true,
            data = new
            {
                transactions = transactions.Select(t => new
                {
                    transaction_id = t.TransactionId,
                    mvid = t.Mvid,
                    started_at = t.StartedAt,
                    completed_at = t.CompletedAt,
                    status = t.Status.ToString(),
                    modification_count = t.ModificationCount
                }),
                total = transactions.Count()
            }
        });
    }
}

public class BeginTransactionRequest
{
    public string Mvid { get; set; } = "";
}

public class TransactionIdRequest
{
    public string TransactionId { get; set; } = "";
}
