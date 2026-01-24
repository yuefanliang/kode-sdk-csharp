using Microsoft.EntityFrameworkCore;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services.Persistence;

/// <summary>
/// 应用程序数据库上下文
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User 配置
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.AgentId);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.AgentId).IsRequired();
        });

        // Session 配置
        modelBuilder.Entity<SessionEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AgentId).IsUnique();
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.AgentId).IsRequired();

            // 关系配置 - User 到 Session 的关系
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sessions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired(false); // 导航属性可以为空
        });

        // Workspace 配置
        modelBuilder.Entity<WorkspaceEntity>(entity =>
        {
            entity.HasIndex(e => e.WorkspaceId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.WorkspaceId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
        });
    }
}
