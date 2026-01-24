# 数据库问题修复说明

## 问题描述

1. **Entity Framework Core错误**: `Microsoft.EntityFrameworkCore.DbUpdateException`
2. **404错误**: `/api/users/profile?userId=default-user-001` 返回404

## 根本原因

数据库实体关系配置错误：
- `WorkspaceEntity` 有一个导航属性 `Sessions`，指向 `SessionEntity`
- 但 `SessionEntity` 的 `UserId` 字段实际是关联到 `UserEntity` 的，不是 `WorkspaceEntity`
- 这导致Entity Framework Core在尝试保存实体时产生关系冲突

## 修复内容

### 1. 修复 AppDbContext.cs
移除了错误的Workspace与Session关系配置：
```csharp
// 删除了这段错误的配置：
entity.HasMany(e => e.Sessions)
      .WithOne()
      .HasForeignKey(e => e.UserId)  // 错误：UserId指向User，不是Workspace
      .OnDelete(DeleteBehavior.Restrict);
```

### 2. 修复 WorkspaceEntity.cs
移除了导航属性：
```csharp
// 删除了：
public List<SessionEntity> Sessions { get; set; } = new();
```

### 3. 修复 SqlitePersistenceService.cs
更新了GetWorkspaceAsync方法，移除了Include操作：
```csharp
// 之前：
.Include(w => w.Sessions)

// 现在：
// 直接查询，不再Include Sessions
```

### 4. 创建数据库重置脚本
- `reset-database.bat` (Windows)
- `reset-database.sh` (Linux/Mac)

## 修复步骤

### 方法1：自动删除数据库（已执行）
数据库文件已被删除，应用程序会在下次启动时自动重新创建。

### 方法2：手动重置数据库
如果方法1不生效，请手动执行：
```bash
# Windows
cd examples\Kode.Agent.WebApiAssistant
del app.db app.db-shm app.db-wal

# Linux/Mac
cd examples/Kode.Agent.WebApiAssistant
rm -f app.db app.db-shm app.db-wal
```

## 验证修复

1. 启动后端应用
2. 启动前端应用
3. 检查日志，应该看到：
   ```
   [INFO] Database initialized successfully
   [INFO] Created new user: default-user-001
   ```
4. 前端应该能正常加载，不再显示404错误

## 预防措施

在将来修改数据库实体时：
1. 确保关系配置正确
2. 使用EF Core的迁移功能而不是直接删除数据库
3. 在开发环境中启用详细的EF Core日志以便调试
4. 实体关系遵循单一职责原则，一个外键只关联一个实体

## 注意事项

- 删除数据库会清除所有数据（用户、会话、工作区等）
- 如果是生产环境，请使用数据库迁移而不是直接删除
- 建议添加数据库备份机制
