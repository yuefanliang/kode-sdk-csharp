using System.Collections.Concurrent;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Types;
using Microsoft.Extensions.Logging;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 流处理服务 - 处理对话流中的实时交互
/// </summary>
public class StreamProcessorService
{
    private readonly ConcurrentDictionary<string, SessionContext> _sessions = new();
    private readonly ILogger<StreamProcessorService> _logger;
    private readonly IApprovalService _approvalService;

    public StreamProcessorService(
        ILogger<StreamProcessorService> logger,
        IApprovalService approvalService)
    {
        _logger = logger;
        _approvalService = approvalService;
    }

    /// <summary>
    /// 会话上下文
    /// </summary>
    public class SessionContext
    {
        public string SessionId { get; set; } = string.Empty;
        public string? CurrentFileId { get; set; }
        public List<string> FileErrors { get; set; } = new();
        public List<string> PendingApprovals { get; set; } = new();
        public string? UserId { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 获取或创建会话上下文
    /// </summary>
    public SessionContext GetOrCreateSession(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, id => new SessionContext
        {
            SessionId = id,
            LastActivity = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 关联文件到会话
    /// </summary>
    public void AssociateFileWithSession(string sessionId, string fileId)
    {
        var context = GetOrCreateSession(sessionId);
        context.CurrentFileId = fileId;
        context.LastActivity = DateTime.UtcNow;

        _logger.LogInformation("文件 {FileId} 关联到会话 {SessionId}", fileId, sessionId);
    }

    /// <summary>
    /// 记录文件错误
    /// </summary>
    public void RecordFileError(string sessionId, string fileId, string error)
    {
        var context = GetOrCreateSession(sessionId);
        context.FileErrors.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] 文件 {fileId}: {error}");
        context.LastActivity = DateTime.UtcNow;

        _logger.LogWarning("记录文件错误 - 会话: {SessionId}, 文件: {FileId}, 错误: {Error}",
            sessionId, fileId, error);
    }

    /// <summary>
    /// 创建审批请求
    /// </summary>
    public async Task<string?> CreateApprovalAsync(
        string sessionId,
        string toolName,
        object? arguments,
        CancellationToken cancellationToken = default)
    {
        var context = GetOrCreateSession(sessionId);

        var approvalId = await _approvalService.CreateApprovalAsync(
            agentId: sessionId,
            userId: context.UserId ?? "system",
            toolName: toolName,
            arguments: arguments);

        context.PendingApprovals.Add(approvalId);
        context.LastActivity = DateTime.UtcNow;

        _logger.LogInformation("创建审批请求 - 会话: {SessionId}, 审批ID: {ApprovalId}, 工具: {ToolName}",
            sessionId, approvalId, toolName);

        return approvalId;
    }

    /// <summary>
    /// 检查审批状态
    /// </summary>
    public async Task<bool> CheckApprovalStatusAsync(
        string sessionId,
        string approvalId,
        CancellationToken cancellationToken = default)
    {
        var approval = await _approvalService.GetApprovalAsync(approvalId);

        if (approval == null)
            return false;

        var context = GetOrCreateSession(sessionId);

        // 如果审批完成，从待审批列表中移除
        if (approval.Decision != "pending")
        {
            context.PendingApprovals.Remove(approvalId);
            context.LastActivity = DateTime.UtcNow;

            _logger.LogInformation("审批完成 - 审批ID: {ApprovalId}, 状态: {Status}",
                approvalId, approval.Decision);

            return approval.Decision == "approved";
        }

        return false;
    }

    /// <summary>
    /// 获取审批服务（内部使用）
    /// </summary>
    public IApprovalService GetApprovalService() => _approvalService;

    /// <summary>
    /// 清理过期会话
    /// </summary>
    public void CleanupExpiredSessions(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        var expiredSessions = _sessions
            .Where(kvp => now - kvp.Value.LastActivity > timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            if (_sessions.TryRemove(sessionId, out _))
            {
                _logger.LogInformation("清理过期会话: {SessionId}", sessionId);
            }
        }
    }

    /// <summary>
    /// 获取会话统计信息
    /// </summary>
    public SessionStats GetSessionStats(string sessionId)
    {
        var context = GetOrCreateSession(sessionId);
        return new SessionStats
        {
            SessionId = sessionId,
            CurrentFileId = context.CurrentFileId,
            ErrorCount = context.FileErrors.Count,
            PendingApprovalCount = context.PendingApprovals.Count,
            LastActivity = context.LastActivity
        };
    }

    public record SessionStats
    {
        public string SessionId { get; init; } = string.Empty;
        public string? CurrentFileId { get; init; }
        public int ErrorCount { get; init; }
        public int PendingApprovalCount { get; init; }
        public DateTime LastActivity { get; init; }
    }
}
