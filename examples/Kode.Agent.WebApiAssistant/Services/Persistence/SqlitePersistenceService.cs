using Microsoft.EntityFrameworkCore;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services.Persistence;

/// <summary>
/// SQLite 持久化服务实现
/// </summary>
public class SqlitePersistenceService : IPersistenceService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqlitePersistenceService> _logger;

    public SqlitePersistenceService(
        AppDbContext dbContext,
        ILogger<SqlitePersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        _logger.LogInformation("Database initialized successfully");
    }

    #region User 操作

    public async Task<UserEntity?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public async Task<UserEntity> UpsertUserAsync(UserEntity user, CancellationToken cancellationToken = default)
    {
        var existingUser = await _dbContext.Users.FindAsync(new object[] { user.UserId }, cancellationToken);
        if (existingUser != null)
        {
            // 更新现有用户
            existingUser.DisplayName = user.DisplayName;
            existingUser.AgentId = user.AgentId;
            existingUser.LastActiveAt = user.LastActiveAt;
            _dbContext.Users.Update(existingUser);
        }
        else
        {
            // 创建新用户
            _dbContext.Users.Add(user);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Upserted user: {UserId}", user.UserId);
        return user;
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null)
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted user: {UserId}", userId);
        }
    }

    public async Task<IReadOnlyList<UserEntity>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .OrderByDescending(u => u.LastActiveAt)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Session 操作

    public async Task<SessionEntity?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
    }

    public async Task<SessionEntity> CreateSessionAsync(SessionEntity session, CancellationToken cancellationToken = default)
    {
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created session: {SessionId}", session.SessionId);
        return session;
    }

    public async Task UpdateSessionAsync(SessionEntity session, CancellationToken cancellationToken = default)
    {
        var existingSession = await _dbContext.Sessions.FindAsync(new object[] { session.SessionId }, cancellationToken);
        if (existingSession != null)
        {
            _dbContext.Entry(existingSession).CurrentValues.SetValues(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated session: {SessionId}", session.SessionId);
        }
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions.FindAsync(new object[] { sessionId }, cancellationToken);
        if (session != null)
        {
            _dbContext.Sessions.Remove(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
        }
    }

    public async Task<IReadOnlyList<SessionEntity>> ListSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task IncrementSessionMessageCountAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions.FindAsync(new object[] { sessionId }, cancellationToken);
        if (session != null)
        {
            session.MessageCount++;
            session.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion

    #region Workspace 操作

    public async Task<WorkspaceEntity?> GetWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkspaceId == workspaceId, cancellationToken);
    }

    public async Task<WorkspaceEntity> CreateWorkspaceAsync(WorkspaceEntity workspace, CancellationToken cancellationToken = default)
    {
        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created workspace: {WorkspaceId}", workspace.WorkspaceId);
        return workspace;
    }

    public async Task UpdateWorkspaceAsync(WorkspaceEntity workspace, CancellationToken cancellationToken = default)
    {
        var existingWorkspace = await _dbContext.Workspaces.FindAsync(new object[] { workspace.WorkspaceId }, cancellationToken);
        if (existingWorkspace != null)
        {
            _dbContext.Entry(existingWorkspace).CurrentValues.SetValues(workspace);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated workspace: {WorkspaceId}", workspace.WorkspaceId);
        }
    }

    public async Task DeleteWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces.FindAsync(new object[] { workspaceId }, cancellationToken);
        if (workspace != null)
        {
            _dbContext.Workspaces.Remove(workspace);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted workspace: {WorkspaceId}", workspaceId);
        }
    }

    public async Task<IReadOnlyList<WorkspaceEntity>> ListWorkspacesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SetActiveWorkspaceAsync(string userId, string workspaceId, CancellationToken cancellationToken = default)
    {
        // 将用户所有工作区设置为非活动
        var userWorkspaces = await _dbContext.Workspaces
            .Where(w => w.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var ws in userWorkspaces)
        {
            ws.IsActive = false;
        }

        // 设置指定工作区为活动
        var activeWorkspace = await _dbContext.Workspaces.FindAsync(new object[] { workspaceId }, cancellationToken);
        if (activeWorkspace != null && activeWorkspace.UserId == userId)
        {
            activeWorkspace.IsActive = true;
            activeWorkspace.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Set active workspace: {WorkspaceId} for user: {UserId}", workspaceId, userId);
    }

    public async Task<WorkspaceEntity?> GetActiveWorkspaceAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.IsActive, cancellationToken);
    }

    #endregion
}
