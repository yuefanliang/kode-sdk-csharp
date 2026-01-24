# Kode Agent Vue Web

使用 Vue 3 + TypeScript 开发的 Kode Agent 前端界面。

## 功能特性

- ✅ 工作区管理 - 创建、切换、删除工作区
- ✅ 智能对话系统 - 支持多轮持续交互
- ✅ 审批流程 - 集成审批功能，保持对话上下文
- ✅ 会话管理 - 创建、切换、删除会话
- ✅ Markdown 渲染 - 美化消息显示
- ✅ 代码高亮 - 支持语法高亮
- ✅ 响应式设计 - 界面简洁直观，交互流畅
- ✅ 错误处理 - 完善的错误提示和状态管理

## 技术栈

- Vue 3.4+
- TypeScript 5.4+
- Pinia 2.1+ (状态管理)
- Element Plus 2.6+ (UI 组件库)
- Axios 1.6+ (HTTP 客户端)
- Markdown-it 14.0+ (Markdown 渲染)
- Highlight.js 11.9+ (代码高亮)
- Vite 5.1+ (构建工具)
- Vue Router 4.3+ (路由)

## 快速开始

### 安装依赖

```bash
npm install
```

### 启动开发服务器

```bash
npm run dev
```

前端服务将在 `http://localhost:3000` 启动，并自动代理 API 请求到后端服务器 `http://localhost:5123`。

### 构建生产版本

```bash
npm run build
```

### 预览生产构建

```bash
npm run preview
```

## 项目结构

```
src/
├── api/              # API 服务层
│   ├── request.ts    # Axios 请求封装
│   ├── user.ts       # 用户 API
│   ├── workspace.ts  # 工作区 API
│   ├── session.ts    # 会话 API
│   ├── approval.ts   # 审批 API
│   └── chat.ts       # 对话 API
├── assets/           # 静态资源
├── components/       # Vue 组件
│   ├── WorkspaceSelector.vue   # 工作区选择器
│   ├── SessionList.vue          # 会话列表
│   ├── ApprovalPanel.vue        # 审批面板
│   └── MarkdownRenderer.vue     # Markdown 渲染器
├── router/           # 路由配置
│   └── index.ts
├── stores/           # Pinia 状态管理
│   ├── user.ts       # 用户状态
│   ├── workspace.ts  # 工作区状态
│   ├── session.ts    # 会话状态
│   ├── approval.ts   # 审批状态
│   └── chat.ts       # 对话状态
├── types/            # TypeScript 类型定义
│   └── index.ts
├── views/            # 页面视图
│   └── ChatView.vue  # 对话页面
├── App.vue           # 根组件
└── main.ts           # 应用入口
```

## 使用说明

### 默认用户

应用使用默认用户（ID: `default-user-001`），无需登录即可使用所有功能。

### 工作区管理

- 点击顶部的工作区名称可切换工作区
- 选择"创建工作区"可新建工作区
- 每个工作区有独立的配置和环境

### 对话功能

- 支持多轮持续对话
- 自动保持对话上下文
- 输入消息后按 Enter 发送，Shift+Enter 换行
- 支持 Markdown 格式和代码高亮

### 审批流程

- 待审批事项显示在右侧面板
- 可以查看审批详情和参数
- 支持确认或取消审批
- 保持审批事项的对话上下文

### 会话管理

- 左侧显示所有会话列表
- 点击"新建"创建新会话
- 支持切换和删除会话
- 自动更新会话时间

## API 接口

应用通过以下 API 接口与后端交互：

- `POST /v1/chat/completions` - 发送对话消息
- `POST /{sessionId}/v1/chat/completions` - 会话级别的对话消息
- `GET /api/workspaces` - 获取工作区列表
- `POST /api/workspaces` - 创建工作区
- `GET /api/sessions` - 获取会话列表
- `POST /api/sessions` - 创建会话
- `GET /api/approvals/pending` - 获取待审批列表
- `POST /api/approvals/{approvalId}/confirm` - 确认审批
- `POST /api/approvals/{approvalId}/cancel` - 取消审批

## 配置说明

### API 代理

在 `vite.config.ts` 中配置了 API 代理：

```typescript
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5123',
      changeOrigin: true
    },
    '/v1': {
      target: 'http://localhost:5123',
      changeOrigin: true
    }
  }
}
```

如需修改后端地址，请修改 `target` 字段。

## 注意事项

1. 确保后端服务已在 `http://localhost:5123` 启动
2. 前端开发服务器运行在 `http://localhost:3000`
3. 所有 API 请求都会自动代理到后端
4. 审批面板会每30秒自动刷新待审批列表
5. 对话消息会自动滚动到底部

## 开发建议

- 使用 TypeScript 提供的类型检查
- 遵循 Vue 3 Composition API 最佳实践
- 使用 Pinia 进行状态管理
- 保持组件单一职责原则
- 做好错误处理和用户提示

## 许可证

MIT
