# 默认用户创建 - 快速开始

## ✅ 已完成

默认用户（`userId="default-user-001"`）的创建功能已完全实现！

---

## 🚀 立即开始

### 1. 启动后端

```bash
cd examples/Kode.Agent.WebApiAssistant
dotnet run
```

等待看到：
```
[INFO] Database initialized successfully
[INFO] Kode.Agent WebApi Assistant started successfully
```

### 2. 启动前端

```bash
cd examples/Kode.Agent.VueWeb
npm install  # 首次需要
npm run dev
```

### 3. 打开浏览器

访问：`http://localhost:3000`

**自动完成：**
- ✅ 自动创建默认用户（`default-user-001`）
- ✅ 显示成功提示："默认用户创建成功"
- ✅ 应用正常加载
- ✅ 所有功能可用

---

## 🎯 新功能

### 新 API 端点

```
POST /api/users/create?userId={userId}
```

**示例：**
```bash
curl -X POST "http://localhost:5123/api/users/create?userId=default-user-001" \
  -H "Content-Type: application/json" \
  -d '{"username":"Default User","email":"default@example.com"}'
```

**特点：**
- 指定任何 `userId`（包括 `default-user-001`）
- 自动检查用户是否已存在
- 自动生成 Agent ID
- 返回完整的用户信息

---

## 📝 API 使用对比

### 旧方法（已弃用）

```typescript
// ❌ userId 会是 "Default User"
await userApi.register({
  username: 'Default User',
  email: 'default@example.com'
})
```

### 新方法（推荐）

```typescript
// ✅ userId 是 "default-user-001"
await userApi.createUser('default-user-001', {
  username: 'Default User',
  email: 'default@example.com'
})
```

---

## 🔄 自动化流程

### 首次启动

```
前端启动
  ↓
尝试获取用户：GET /api/users/profile?userId=default-user-001
  ↓
返回 404（用户不存在）
  ↓
自动创建用户：POST /api/users/create?userId=default-user-001
  ↓
创建成功，返回用户信息
  ↓
保存到前端状态
  ↓
应用就绪
```

### 后续启动

```
前端启动
  ↓
尝试获取用户：GET /api/users/profile?userId=default-user-001
  ↓
返回 200（用户已存在）
  ↓
直接使用用户
  ↓
应用就绪
```

---

## ✨ 验证步骤

### 1. 检查用户创建

**查看浏览器控制台：**
```
GET /api/users/profile?userId=default-user-001 404 (Not Found)
正在创建默认用户...
POST /api/users/create?userId=default-user-001 201 (Created)
默认用户创建成功
```

### 2. 查看后端日志

**后端应该显示：**
```
[INFO] Create user request. UserId: default-user-001, Username: Default User
[INFO] Created user: default-user-001, Username: Default User
```

### 3. 测试创建会话

**在聊天界面发送第一条消息：**
```
POST /api/sessions?userId=default-user-001
```

**应该成功创建会话：**
```json
{
  "sessionId": "...",
  "userId": "default-user-001",
  ...
}
```

---

## 📚 完整文档

- **详细指南**：`examples/Default_User_Creation_Guide.md`
- **API 文档**：http://localhost:5123（Swagger UI）
- **故障排除**：`examples/TROUBLESHOOTING.md`

---

## 🎊 改进总结

| 改进项 | 说明 | 状态 |
|--------|------|------|
| 新 API 端点 | 支持指定 userId 创建用户 | ✅ |
| 后端服务 | 实现创建用户逻辑 | ✅ |
| 前端 API | 封装新端点调用 | ✅ |
| 前端状态管理 | 使用新 API 创建默认用户 | ✅ |
| 错误处理 | 友好的错误提示 | ✅ |
| 文档 | 完整的使用指南 | ✅ |

---

**所有功能已就绪！直接启动应用即可使用。** 🚀

---

## 💡 提示

- 默认用户自动创建，无需手动操作
- 用户信息会持久化保存到数据库
- 重启应用后会自动使用已存在的用户
- 如果需要重置用户，删除数据库文件：`del app.db app.db-shm app.db-wal`

---

**有问题？** 查看详细文档或检查日志。
