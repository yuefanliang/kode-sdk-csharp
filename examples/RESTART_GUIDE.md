# 快速重启指南

## 问题已修复 ✅

我们刚刚修复了以下问题：
1. ✅ Entity Framework Core 数据库错误
2. ✅ 404 Not Found 错误

## 重启步骤

### 步骤 1: 停止当前运行的服务
- 停止后端服务（如果在运行）
- 停止前端服务（如果在运行）

### 步骤 2: 启动后端服务

**方式 A：使用 Visual Studio**
1. 打开 `Kode.Agent.WebApiAssistant` 项目
2. 按 `F5` 或点击"开始"按钮

**方式 B：使用命令行**
```bash
cd examples/Kode.Agent.WebApiAssistant
dotnet run
```

等待看到以下日志：
```
[INFO] Database initialized successfully
[INFO] Kode.Agent WebApi Assistant started successfully
```

后端服务将在 `http://localhost:5123` 运行

### 步骤 3: 启动前端服务

**在新的终端窗口中运行：**

```bash
cd examples/Kode.Agent.VueWeb
npm install  # 仅首次运行需要
npm run dev
```

前端服务将在 `http://localhost:3000` 运行

### 步骤 4: 访问应用

打开浏览器访问：`http://localhost:3000`

应该能看到：
- ✅ 界面正常加载
- ✅ 自动创建默认用户
- ✅ 可以开始对话

## 预期日志输出

### 后端日志
```
[10:00:00] Starting Kode.Agent WebApi Assistant
[10:00:01] Database initialized successfully
[10:00:01] Available endpoints:
[10:00:01]   POST http://localhost:5123/v1/chat/completions
[10:00:01]   POST http://localhost:5123/{sessionId}/v1/chat/completions
[10:00:01] Kode.Agent WebApi Assistant started successfully
```

### 前端日志（浏览器控制台）
```
GET /api/users/profile?userId=default-user-001 404 (Not Found)
GET /api/users/register 201 (Created)
✅ 默认用户创建成功
```

注意：第一次404是正常的，因为用户不存在，系统会自动创建。

## 如果仍然遇到问题

### 1. 检查端口占用
```bash
# Windows
netstat -ano | findstr :5123
netstat -ano | findstr :3000

# Linux/Mac
lsof -i :5123
lsof -i :3000
```

如果端口被占用，可以：
- 关闭占用端口的程序
- 或修改配置文件中的端口号

### 2. 清理浏览器缓存
- 按 `Ctrl+Shift+R` (Windows/Linux) 或 `Cmd+Shift+R` (Mac) 强制刷新
- 或清除浏览器缓存和Cookie

### 3. 检查防火墙设置
确保防火墙允许访问 localhost 端口 5123 和 3000

### 4. 查看详细日志
**后端日志位置：**
```
examples/Kode.Agent.WebApiAssistant/logs/
```

**浏览器控制台：**
- 按 `F12` 打开开发者工具
- 查看 Console 和 Network 标签页

## 快速测试

### 测试 1: 健康检查
访问：`http://localhost:5123/healthz`

应该返回：
```json
{
  "ok": true
}
```

### 测试 2: 查看API文档
访问：`http://localhost:5123`

应该看到 Swagger UI 界面

### 测试 3: 测试对话
1. 打开前端应用
2. 在输入框中输入："你好"
3. 按Enter发送

应该能看到AI的回复。

## 常用命令

### 后端
```bash
# 编译
dotnet build

# 运行
dotnet run

# 发布
dotnet publish -c Release
```

### 前端
```bash
# 安装依赖
npm install

# 开发模式
npm run dev

# 生产构建
npm run build

# 预览生产构建
npm run preview
```

## 下一步

应用现在应该可以正常工作了！你可以：

1. 📝 开始与AI对话
2. 🗂️ 创建和管理工作区
3. 💬 管理会话
4. ✅ 处理审批事项

需要帮助？查看：
- [故障排除指南](./TROUBLESHOOTING.md)
- [后端README](./Kode.Agent.WebApiAssistant/README.md)
- [前端README](./Kode.Agent.VueWeb/README.md)

---

**提示：** 将此页面添加到浏览器书签，方便下次快速重启服务！
