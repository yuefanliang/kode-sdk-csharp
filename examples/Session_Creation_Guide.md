# 会话创建功能测试指南

## 功能概述

会话系统允许用户创建多个独立的对话会话，每个会话可以包含多个消息，并支持多轮对话。

## API 端点

### 创建新会话
```
POST /api/sessions?userId={userId}
Content-Type: application/json

{
  "title": "会话标题（可选）"
}
```

**响应：**
```json
{
  "sessionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "userId": "default-user-001",
  "title": "会话标题",
  "agentId": "session_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "createdAt": "2025-01-25T10:00:00Z",
  "updatedAt": "2025-01-25T10:00:00Z",
  "messageCount": 0
}
```

### 获取会话列表
```
GET /api/sessions?userId={userId}
```

### 获取单个会话
```
GET /api/sessions/{sessionId}
```

### 更新会话标题
```
PATCH /api/sessions/{sessionId}
Content-Type: application/json

{
  "title": "新标题"
}
```

### 删除会话
```
DELETE /api/sessions/{sessionId}
```

## 前端使用

### 1. 创建新会话
```typescript
import { useSessionStore } from '@/stores/session'

const sessionStore = useSessionStore()

// 创建新会话
const newSession = await sessionStore.createSession('default-user-001', {
  title: '新对话'
})

console.log('会话ID:', newSession.sessionId)
```

### 2. 在对话中使用
```typescript
import { useChatStore } from '@/stores/chat'

const chatStore = useChatStore()

// 切换到新会话
sessionStore.switchSession(newSession.sessionId)

// 发送消息（会自动与指定会话关联）
await chatStore.sendMessage(newSession.sessionId, '你好')
```

### 3. 获取会话列表
```typescript
// 加载用户的所有会话
await sessionStore.loadSessions('default-user-001')

// 遍历会话
sessionStore.sessions.forEach(session => {
  console.log(`${session.title} - ${session.messageCount} 条消息`)
})
```

## 测试步骤

### 使用 curl 测试

#### 1. 创建会话（带标题）
```bash
curl -X POST "http://localhost:5123/api/sessions?userId=default-user-001" \
  -H "Content-Type: application/json" \
  -d '{"title":"测试会话"}'
```

#### 2. 创建会话（使用默认标题）
```bash
curl -X POST "http://localhost:5123/api/sessions?userId=default-user-001" \
  -H "Content-Type: application/json" \
  -d '{}'
```

#### 3. 获取会话列表
```bash
curl "http://localhost:5123/api/sessions?userId=default-user-001"
```

#### 4. 获取单个会话
```bash
curl "http://localhost:5123/api/sessions/{sessionId}"
```

#### 5. 更新会话标题
```bash
curl -X PATCH "http://localhost:5123/api/sessions/{sessionId}" \
  -H "Content-Type: application/json" \
  -d '{"title":"更新后的标题"}'
```

#### 6. 删除会话
```bash
curl -X DELETE "http://localhost:5123/api/sessions/{sessionId}"
```

### 使用前端测试

1. **启动应用**
   - 后端：`cd examples/Kode.Agent.WebApiAssistant && dotnet run`
   - 前端：`cd examples/Kode.Agent.VueWeb && npm run dev`

2. **打开浏览器**
   访问 `http://localhost:3000`

3. **创建会话**
   - 点击左侧面板的"新建"按钮
   - 应该在列表中看到新会话

4. **发送消息**
   - 在聊天框输入消息
   - 按 Enter 发送
   - 消息应该成功发送并显示回复

5. **切换会话**
   - 点击不同的会话
   - 每个会话应该有独立的消息历史

6. **删除会话**
   - 点击会话右侧的菜单
   - 选择"删除"
   - 会话应该从列表中移除

## 常见问题

### Q: 创建会话时返回 404
**A:** 用户不存在。确保：
1. 用户已正确创建
2. userId 参数正确

检查：
```bash
curl "http://localhost:5123/api/users/profile?userId=default-user-001"
```

### Q: 创建会话时返回 400 Bad Request
**A:** 参数缺失或无效。确保：
1. userId 参数在查询字符串中
2. 请求体是有效的 JSON

### Q: 创建会话时返回 500 错误
**A:** 数据库错误。检查：
1. 数据库是否正确初始化
2. 查看后端日志获取详细错误

### Q: 会话列表为空
**A:** 可能的原因：
1. 没有创建会话
2. userId 参数错误
3. 数据库权限问题

## 性能优化

### 1. 会话缓存
系统使用内存缓存提高性能：
```csharp
private readonly ConcurrentDictionary<string, Session> _cache
```

### 2. 批量加载
获取会话列表时一次性加载所有会话：
```typescript
await sessionStore.loadSessions(userId)
```

### 3. 延迟加载
使用 Include 延迟加载关联数据：
```csharp
.Include(s => s.User)
```

## 数据库结构

### Sessions 表

| 字段 | 类型 | 说明 |
|------|------|------|
| SessionId | VARCHAR(256) PK | 会话唯一ID |
| UserId | VARCHAR(256) FK | 关联的用户ID |
| Title | VARCHAR(512) | 会话标题 |
| AgentId | VARCHAR(256) UNIQUE | 关联的Agent ID |
| CreatedAt | DATETIME | 创建时间 |
| UpdatedAt | DATETIME | 更新时间 |
| MessageCount | INT | 消息数量 |

### 关系

- **Session → User**: 多对一
  - 一个用户可以有多个会话
  - 删除用户时级联删除所有会话

## 安全考虑

### 1. 用户隔离
每个会话通过 `UserId` 隔离，用户只能访问自己的会话。

### 2. 验证
创建会话前验证用户存在性：
```csharp
var user = await _userService.GetUserAsync(userId);
if (user == null)
{
    throw new ArgumentException($"User not found: {userId}");
}
```

### 3. 输入验证
- 会话标题最大长度：512字符
- 会话ID必须是有效的GUID

## 扩展功能

未来可以添加：

1. **会话分组**
   - 按项目、主题等分组
   - 添加文件夹功能

2. **会话标签**
   - 为会话添加标签
   - 按标签筛选

3. **会话搜索**
   - 搜索会话内容
   - 搜索消息历史

4. **会话导出**
   - 导出为 Markdown
   - 导出为 PDF

5. **会话分享**
   - 生成分享链接
   - 只读访问权限

## 相关文档

- [API 文档](http://localhost:5123) - Swagger UI
- [后端 README](./Kode.Agent.WebApiAssistant/README.md)
- [前端 README](./Kode.Agent.VueWeb/README.md)
- [故障排除指南](./TROUBLESHOOTING.md)
