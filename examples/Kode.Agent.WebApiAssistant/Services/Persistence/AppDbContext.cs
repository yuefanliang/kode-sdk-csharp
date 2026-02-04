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
    public DbSet<SessionWorkspaceEntity> SessionWorkspaces => Set<SessionWorkspaceEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<SystemConfigEntity> SystemConfigs => Set<SystemConfigEntity>();
    public DbSet<SessionSkillEntity> SessionSkills => Set<SessionSkillEntity>();

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

        // SessionWorkspace 配置
        modelBuilder.Entity<SessionWorkspaceEntity>(entity =>
        {
            entity.HasIndex(e => e.WorkspaceId).IsUnique();
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.WorkspaceId).IsRequired();
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.WorkDirectory).IsRequired();

            // 关系配置 - Session 到 SessionWorkspace 的关系
            entity.HasOne<SessionEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            // 关系配置 - User 到 SessionWorkspace 的关系
            entity.HasOne<UserEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Message 配置
        modelBuilder.Entity<MessageEntity>(entity =>
        {
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.MessageId).IsRequired();
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.Content).IsRequired();

            // 关系配置 - Session 到 Message 的关系
            entity.HasOne<SessionEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            // 关系配置 - User 到 Message 的关系
            entity.HasOne<UserEntity>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SystemConfig 配置
        modelBuilder.Entity<SystemConfigEntity>(entity =>
        {
            entity.HasIndex(e => e.ConfigKey).IsUnique();
            entity.HasIndex(e => e.Group);
            entity.Property(e => e.ConfigKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Group).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ValueType).IsRequired().HasMaxLength(20);
        });

        // SessionSkill 配置
        modelBuilder.Entity<SessionSkillEntity>(entity =>
        {
            entity.HasIndex(e => new { e.SessionId, e.SkillId }).IsUnique();
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.SkillId);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SkillId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(20);

            // 关系配置
            entity.HasOne(e => e.Session)
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
