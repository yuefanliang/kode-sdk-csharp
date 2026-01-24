# 问题解决指南

## 已解决的问题

### 问题1：Entity Framework Core 错误
**错误信息：**
```
Microsoft.EntityFrameworkCore.DbUpdateException:
"An error occurred while saving the entity changes. See the inner exception for details."
```

**根本原因：**
数据库实体关系配置错误。`WorkspaceEntity` 包含了指向 `SessionEntity` 的导航属性，但 `SessionEntity` 的 `UserId` 字段实际关联到 `UserEntity`，而不是 `WorkspaceEntity`。这导致 Entity Framework Core 在尝试保存实体时产生关系冲突。

**修复内容：**

1. **修复 `AppDbContext.cs`**
   - 移除了错误的 Workspace 与 Session 关系配置

2. **修复 `WorkspaceEntity.cs`**
   - 移除了导航属性 `List<SessionEntity> Sessions`

3. **修复 `SqlitePersistenceService.cs`**
   - 更新 `GetWorkspaceAsync` 方法，移除了 `Include(w => w.Sessions)`

### 问题2：404 Not Found 错误
**错误信息：**
```
http://localhost:3000/api/users/profile?userId=default-user-001 404 (Not Found)
```

**根本原因：**
由于问题1中的数据库保存失败，导致用户无法创建。前端尝试获取不存在的用户信息时返回404。

**修复内容：**

1. **删除旧数据库文件**
   - 删除了 `app.db`、`app.db-shm`、`app.db-wal` 文件
   - 应用程序会在下次启动时自动重新创建正确的数据库结构

2. **改进前端错误处理**
   - 添加了 `error-handler.ts` 工具类
   - 更新了 `user.ts` store，更好地处理404错误
   - 更新了 `request.ts`，改进错误消息显示

## 验证修复

### 1. 重启后端服务
```bash
cd examples/Kode.Agent.WebApiAssistant
dotnet run
```

应该看到以下日志：
```
[INFO] Database initialized successfully
```

### 2. 重启前端服务
```bash
cd examples/Kode.Agent.VueWeb
npm run dev
```

### 3. 验证功能
- 前端应该能正常加载
- 自动创建默认用户
- 不再显示404错误
- 所有功能正常工作

## 其他改进

### 新增错误处理工具
创建了 `src/utils/error-handler.ts`，提供以下功能：
- 提取错误消息
- 判断错误类型（404、网络错误、服务器错误等）
- 格式化错误消息用于显示

### 改进的用户体验
- 更友好的错误提示
- 明确的状态反馈
- 详细的控制台日志

## 未来预防措施

### 开发建议
1. **使用 EF Core 迁移**
   - 不要直接删除生产数据库
   - 使用 `Add-Migration` 和 `Update-Database` 命令

2. **启用详细日志**
   - 在开发环境中启用 EF Core 详细日志
   - 便于调试数据库问题

3. **代码审查**
   - 修改数据库实体时仔细检查关系配置
   - 确保外键和导航属性一致

### 生产环境
1. **数据库备份**
   - 定期备份数据库
   - 在重大修改前创建备份

2. **监控和告警**
   - 监控数据库错误
   - 设置自动告警

3. **回滚机制**
   - 准备数据库迁移回滚脚本
   - 确保可以快速恢复

## 相关文件

### 后端文件
- `examples/Kode.Agent.WebApiAssistant/Services/Persistence/AppDbContext.cs`
- `examples/Kode.Agent.WebApiAssistant/Services/Persistence/Entities/WorkspaceEntity.cs`
- `examples/Kode.Agent.WebApiAssistant/Services/Persistence/SqlitePersistenceService.cs`

### 前端文件
- `examples/Kode.Agent.VueWeb/src/utils/error-handler.ts`
- `examples/Kode.Agent.VueWeb/src/api/request.ts`
- `examples/Kode.Agent.VueWeb/src/stores/user.ts`

### 工具脚本
- `examples/Kode.Agent.WebApiAssistant/reset-database.bat`
- `examples/Kode.Agent.WebApiAssistant/reset-database.sh`

## 常见问题

### Q: 如何重置数据库？
**A:** 运行以下命令：
```bash
# Windows
cd examples\Kode.Agent.WebApiAssistant
del app.db app.db-shm app.db-wal

# Linux/Mac
cd examples/Kode.Agent.WebApiAssistant
rm -f app.db app.db-shm app.db-wal
```

### Q: 如何查看数据库内容？
**A:** 使用 SQLite 客户端工具，例如：
- DB Browser for SQLite (https://sqlitebrowser.org/)
- VS Code SQLite 扩展
- 命令行工具 `sqlite3`

### Q: 如何启用 EF Core 详细日志？
**A:** 在 `appsettings.json` 中添加：
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

## 支持

如果问题仍然存在，请：
1. 检查后端日志文件：`examples/Kode.Agent.WebApiAssistant/logs/`
2. 检查浏览器控制台错误
3. 确保后端和前端都在运行
4. 验证数据库连接字符串配置

## 更新日志

- 2025-01-25: 修复了数据库关系配置错误，解决了EF Core异常和404问题
- 2025-01-25: 添加了完善的错误处理机制
- 2025-01-25: 创建了数据库重置脚本
