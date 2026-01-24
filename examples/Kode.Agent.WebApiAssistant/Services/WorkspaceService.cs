using Kode.Agent.WebApiAssistant.Models.Entities;
using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 工作区管理服务实现
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly IPersistenceService _persistenceService;
    private readonly ILogger<WorkspaceService> _logger;
    private readonly IUserService _userService;
    private readonly AssistantOptions _assistantOptions;
    private readonly string _defaultWorkDir;

    public WorkspaceService(
        IPersistenceService persistenceService,
        ILogger<WorkspaceService> logger,
        IUserService userService,
        AssistantOptions assistantOptions)
    {
        _persistenceService = persistenceService;
        _logger = logger;
        _userService = userService;
        _assistantOptions = assistantOptions;
        _defaultWorkDir = Path.Combine(_assistantOptions.WorkDir, "data");
        Directory.CreateDirectory(_defaultWorkDir);
    }

    public async Task<Workspace> CreateWorkspaceAsync(
        string userId,
        string name,
        string? description = null,
        string? workDir = null)
    {
        // 验证用户是否存在
        var user = await _userService.GetUserAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found when creating workspace: {UserId}", userId);
            throw new ArgumentException($"User not found: {userId}", nameof(userId));
        }

        var workspaceId = Guid.NewGuid().ToString("N");

        var workspaceEntity = new WorkspaceEntity
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Name = name,
            Description = description,
            WorkDir = NormalizeWorkDir(workDir, workspaceId),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = false
        };

        var createdWorkspace = await _persistenceService.CreateWorkspaceAsync(workspaceEntity);

        _logger.LogInformation("Created workspace: {WorkspaceId} for user: {UserId}", workspaceId, userId);

        return MapToWorkspace(createdWorkspace);
    }

    public async Task<Workspace?> GetWorkspaceAsync(string workspaceId)
    {
        var workspaceEntity = await _persistenceService.GetWorkspaceAsync(workspaceId);
        if (workspaceEntity == null)
        {
            return null;
        }

        var normalized = NormalizeWorkDir(workspaceEntity.WorkDir, workspaceId);
        if (!string.Equals(workspaceEntity.WorkDir, normalized, StringComparison.OrdinalIgnoreCase))
        {
            workspaceEntity.WorkDir = normalized;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            await _persistenceService.UpdateWorkspaceAsync(workspaceEntity);
        }

        return MapToWorkspace(workspaceEntity);
    }

    public async Task<IReadOnlyList<Workspace>> ListWorkspacesAsync(string userId)
    {
        var workspaceEntities = await _persistenceService.ListWorkspacesAsync(userId);
        foreach (var entity in workspaceEntities)
        {
            var normalized = NormalizeWorkDir(entity.WorkDir, entity.WorkspaceId);
            if (!string.Equals(entity.WorkDir, normalized, StringComparison.OrdinalIgnoreCase))
            {
                entity.WorkDir = normalized;
                entity.UpdatedAt = DateTime.UtcNow;
                await _persistenceService.UpdateWorkspaceAsync(entity);
            }
        }

        return workspaceEntities
            .Select(MapToWorkspace)
            .OrderByDescending(w => w.UpdatedAt)
            .ToList();
    }

    public async Task<Workspace?> UpdateWorkspaceAsync(
        string workspaceId,
        string? name = null,
        string? description = null,
        string? workDir = null)
    {
        var workspaceEntity = await _persistenceService.GetWorkspaceAsync(workspaceId);
        if (workspaceEntity == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            workspaceEntity.Name = name;
        }

        if (description != null)
        {
            workspaceEntity.Description = description;
        }

        if (workDir != null)
        {
            workspaceEntity.WorkDir = NormalizeWorkDir(workDir, workspaceId);
        }

        workspaceEntity.UpdatedAt = DateTime.UtcNow;

        await _persistenceService.UpdateWorkspaceAsync(workspaceEntity);
        _logger.LogInformation("Updated workspace: {WorkspaceId}", workspaceId);

        return MapToWorkspace(workspaceEntity);
    }

    public async Task DeleteWorkspaceAsync(string workspaceId)
    {
        await _persistenceService.DeleteWorkspaceAsync(workspaceId);
        _logger.LogInformation("Deleted workspace: {WorkspaceId}", workspaceId);
    }

    public async Task SetActiveWorkspaceAsync(string userId, string workspaceId)
    {
        await _persistenceService.SetActiveWorkspaceAsync(userId, workspaceId);
        _logger.LogInformation("Set active workspace: {WorkspaceId} for user: {UserId}", workspaceId, userId);
    }

    public async Task<Workspace?> GetActiveWorkspaceAsync(string userId)
    {
        var workspaceEntity = await _persistenceService.GetActiveWorkspaceAsync(userId);
        if (workspaceEntity == null)
        {
            return null;
        }

        var normalized = NormalizeWorkDir(workspaceEntity.WorkDir, workspaceEntity.WorkspaceId);
        if (!string.Equals(workspaceEntity.WorkDir, normalized, StringComparison.OrdinalIgnoreCase))
        {
            workspaceEntity.WorkDir = normalized;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            await _persistenceService.UpdateWorkspaceAsync(workspaceEntity);
        }

        return MapToWorkspace(workspaceEntity);
    }

    public async Task AssignSessionToWorkspaceAsync(string sessionId, string workspaceId)
    {
        // 当前实现中,Session 已经通过 UserId 隐式关联到工作区
        // 未来可以考虑扩展 SessionEntity 添加 WorkspaceId 字段实现显式关联
        _logger.LogInformation("Assigned session {SessionId} to workspace {WorkspaceId}", sessionId, workspaceId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 映射 WorkspaceEntity 到 Workspace
    /// </summary>
    private static Workspace MapToWorkspace(WorkspaceEntity entity)
    {
        return new Workspace
        {
            WorkspaceId = entity.WorkspaceId,
            UserId = entity.UserId,
            Name = entity.Name,
            Description = entity.Description,
            WorkDir = entity.WorkDir,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsActive = entity.IsActive
        };
    }

    private string NormalizeWorkDir(string? workDir, string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workDir))
        {
            return _defaultWorkDir;
        }

        var normalized = Path.IsPathRooted(workDir)
            ? Path.GetFullPath(workDir)
            : Path.GetFullPath(Path.Combine(_assistantOptions.WorkDir, workDir));

        var legacyDefault = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "workspaces", workspaceId));

        if (string.Equals(normalized, legacyDefault, StringComparison.OrdinalIgnoreCase))
        {
            return _defaultWorkDir;
        }

        return normalized;
    }
}
