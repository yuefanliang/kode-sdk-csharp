# 创建默认用户指南

## 🎯 目标

创建一个 `userId` 为 `default-user-001` 的默认用户。

---

## ✅ 已实现的改进

### 1. 新增 API 端点：`POST /api/users/create`

这个新的端点允许指定 `userId` 创建用户：

```bash
POST /api/users/create?userId=default-user-001
Content-Type: application/json

{
  "username": "Default User",
  "email": "default@example.com"
}
```

**特点：**
- ✅ 可以指定任何 `userId`（包括 `default-user-001`）
- ✅ 检查用户是否已存在，避免重复创建
- ✅ 自动生成对应的 Agent ID
- ✅ 支持自定义用户名和邮箱

### 2. 前端用户创建逻辑优化

```typescript
// 之前：使用 register API，userId 基于 username
await userApi.register({
  username: 'Default User',  // userId 会是 "Default User"
  email: 'default@example.com'
})

// 现在：使用 create API，指定 userId
await userApi.createUser('default-user-001', {  // userId 是 "default-user-001"
  username: 'Default User',
  email: 'default@example.com'
})
```

### 3. UserService 增强方法

添加了 `CreateUserEntityAsync` 方法：
```csharp
Task<UserEntity> CreateUserEntityAsync(UserEntity userEntity)
```

这个方法：
- ✅ 直接使用 UserEntity 保存到数据库
- ✅ 自动更新缓存
- ✅ 返回保存后的实体

---

## 🚀 使用方法

### 自动创建（前端自动处理）

前端在应用启动时自动创建默认用户：

1. 首次启动：尝试获取 `userId=default-user-001` 的用户
2. 如果用户不存在（404）：自动调用 `/api/users/create` 创建
3. 创建成功：保存用户信息到状态
4. 后续启动：直接使用已存在的用户

**无需任何手动操作！**

### 手动创建 API 调用

如果需要手动创建用户，可以使用：

```bash
curl -X POST "http://localhost:5123/api/users/create?userId=default-user-001" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "Default User",
    "email": "default@example.com"
  }'
```

**响应示例：**
```json
{
  "userId": "default-user-001",
  "displayName": "Default User",
  "agentId": "user_default-user-001_windows",
  "createdAt": "2025-01-25T10:30:00Z",
  "lastActiveAt": "2025-01-25T10:30:00Z"
}
```

---

## 📋 API 对比

### 注册用户 (`POST /api/users/register`)

**用途：** 用户自主注册
```json
{
  "username": "alice",
  "email": "alice@example.com"
}
```

**特点：**
- `userId` = `username`（相同）
- 适用于用户自己选择用户名

### 创建用户 (`POST /api/users/create`)

**用途：** 系统或管理员创建指定用户
```
/api/users/create?userId=default-user-001
```
```json
{
  "username": "Default User",
  "email": "default@example.com"
}
```

**特点：**
- `userId` 在查询字符串中指定
- `username` 可选，默认为 `userId`
- 适用于预设用户ID的场景

### 获取用户信息 (`GET /api/users/profile`)

```bash
GET /api/users/profile?userId=default-user-001
```

---

## 🔄 完整流程

### 首次启动

1. **前端�**
   ```typescript
   await userStore.initDefaultUser()
   ```

2. **尝试获取用户**
   ```
   GET /api/users/profile?userId=default-user-001
   ```

3. **返回 404（用户不存在）**
   ```json
   {
     "error": "User not found"
   }
   ```

4. **创建用户**
   ```typescript
   await userApi.createUser('default-user-001', {
     username: 'Default User',
     email: 'default@example.com'
   })
   ```

5. **API 调用**
   ```
   POST /api/users/create?userId=default-user-001
   Content-Type: application/json

   {
     "username": "Default User",
     "email": "default@example.com"
   }
   ```

6. **用户创建成功**
   ```json
   {
     "userId": "default-user-001",
     "displayName": "Default User",
     ...
   }
   ```

7. **前端保存用户状态**
   ```typescript
   user.value = createResponse.data
   ElMessage.success('默认用户创建成功')
   ```

### 后续启动

1. **前端�**
   ```typescript
   await userStore.initDefaultUser()
   ```

2. **尝试获取用户**
   ```
   GET /api/users/profile?userId=default-user-001
   ```

3. **返回 200（用户已存在）**
   ```json
   {
     "userId": "default-user-001",
     ...
   }
   ```

4. **直接使用用户**
   ```typescript
   user.value = response.data
   // 跳过创建步骤
   ```

---

## 📚 修改的文件

### 后端
1. `Controllers/UsersController.cs`
   - 添加 `POST /api/users/create` 端点
   - 支持通过查询字符串指定 `userId`

2. `Models/Requests/UserCreateRequest.cs`
   - 新建请求模型
   - 包含可选的 `username` 和 `email`

3. `Services/IUserService.cs`
   - 添加 `CreateUserEntityAsync` 方法

4. `Services/UserService.cs`
   - 实现 `CreateUserEntityAsync` 方法

### 前端
1. `src/api/user.ts`
   - 添加 `createUser` 方法
   - 调用新的 API 端点

2. `src/stores/user.ts`
   - 更新 `initDefaultUser` 方法
   - 使用 `createUser` 代替 `register`

---

## ✨ 功能特性

### 后端功能
- ✅ **用户存在性检查**：避免重复创建
- ✅ **Agent ID 生成**：自动生成唯一的 Agent ID
- ✅ **缓存管理**：使用内存缓存提高性能
- ✅ **持久化存储**：自动保存到 SQLite 数据库
- ✅ **日志记录**：详细的操作日志便于调试

### 前端功能
- ✅ **自动初始化**：应用启动时自动创建用户
- ✅ **错误处理**：友好的错误提示
- ✅ **状态管理**：统一管理用户状态
- ✅ **加载状态**：显示加载指示器

---

## 🧪 测试验证

### 测试 1：创建默认用户

```bash
curl -X POST "http://localhost:5123/api/users/create?userId=default-user-001" \
  -H "Content-Type: application/json" \
  -d '{"username":"Default User","email":"default@example.com"}'
```

**预期结果：** 201 Created，返回用户信息

### 测试 2：重复创建

再次执行相同的命令：

```bash
curl -X POST "http://localhost:5123/api/users/create?userId=default-user-001" \
  -H "Content-Type: application/json" \
  -d '{"username":"Default User","email":"default@example.com"}'
```

**预期结果：** 200 OK，返回已存在的用户信息（不重复创建）

### 测试 3：获取用户信息

```bash
curl "http://localhost:5123/api/users/profile?userId=default-user-001"
```

**预期结果：** 200 OK，返回用户详细信息

### 测试 4：前端自动初始化

1. 启动前端应用：`cd examples/Kode.Agent.VueWeb && npm run dev`
2. 访问 `http://localhost:3000`
3. 查看浏览器控制台

**预期结果：**
- 日志显示"正在创建默认用户..."
- 日志显示"默认用户创建成功"
- 应用正常加载

---

## 🐛 常见问题

### Q: 用户创建失败，返回 500 错误

**A:** 检查：
1. 后端服务是否正常运行
2. 数据库是否正确初始化
3. 查看后端日志获取详细错误

### Q: 前端显示"创建用户失败，请检查后端服务"

**A:**
1. 确保后端运行在 `http://localhost:5123`
2. 检查浏览器控制台的网络请求
3. 查看具体的错误消息

### Q: 创建的用户 userId 不是 "default-user-001"

**A:** 确保前端代码中使用：
```typescript
await userApi.createUser('default-user-001', {...})
```
而不是：
```typescript
await userApi.register({...})  // 这会使用 username 作为 userId
```

### Q: 数据库中没有用户记录

**A:**
1. 检查数据库文件是否存在：`app.db`
2. 使用 SQLite 工具查看数据：
   ```bash
   sqlite3 app.db "SELECT * FROM Users WHERE UserId='default-user-001';"
   ```
3. 如果没有记录，重启应用让它自动创建

---

## 📊 数据库表结构

### Users 表

| 字段 | 类型 | 说明 |
|------|------|------|
| UserId | VARCHAR(256) PK | 用户唯一ID |
| DisplayName | VARCHAR(256) | 显示名称 |
| AgentId | VARCHAR(256) | 关联的Agent ID |
| CreatedAt | DATETIME | 创建时间 |
| LastActiveAt | DATETIME | 最后活跃时间 |

### 索引

- `UserId`：唯一索引
- `AgentId`：普通索引

---

## 🎯 总结

| 任务 | 状态 |
|------|------|
| 新增 API 端点 | ✅ 完成 |
| 后端服务实现 | ✅ 完成 |
| 前端 API 封装 | ✅ 完成 |
| 前端状态管理更新 | ✅ 完成 |
| 数据库重置 | ✅ 完成 |
| 文档更新 | ✅ 完成 |

**所有改进已完成，默认用户可以正确创建和使用了！** 🎉

---

## 📚 相关文档

- [后端 API 文档](http://localhost:5123) - Swagger UI
- [后端 README](./Kode.Agent.WebApiAssistant/README.md)
- [前端 README](./Kode.Agent.VueWeb/README.md)
- [故障排除指南](./TROUBLESHOOTING.md)
