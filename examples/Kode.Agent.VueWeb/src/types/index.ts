// 用户相关类型
export interface User {
  userId: string
  username: string
  email?: string
  createdAt: string
}

export interface UserCreateRequest {
  username: string
  email?: string
}

// 工作区相关类型
export interface Workspace {
  workspaceId: string
  name: string
  description?: string
  workDir?: string
  userId: string
  createdAt: string
  updatedAt: string
}

export interface WorkspaceCreateRequest {
  name: string
  description?: string
  workDir?: string
}

// 会话相关类型
export interface Session {
  sessionId: string
  userId: string
  title?: string
  agentId?: string
  createdAt: string
  updatedAt: string
  messageCount?: number
}

export interface SessionCreateRequest {
  title?: string
}

export interface SessionUpdateRequest {
  title?: string
}

export interface SessionStatus {
  sessionId: string
  runtimeState: string
  breakpointState: string
  lastAccessUtc?: string | null
  activeLeases: number
  inPool: boolean
}

// 审批相关类型
export interface Approval {
  approvalId: string
  sessionId?: string
  agentId?: string
  userId: string
  toolName: string
  arguments: unknown
  description: string
  status: 'pending' | 'approved' | 'cancelled'
  createdAt: string
  decidedAt?: string
  note?: string
}

export interface ApprovalDecisionRequest {
  note?: string
}

// 对话相关类型
export interface MessagePart {
  type: 'text' | 'tool'
  content?: string // for text
  toolName?: string // for tool
  toolId?: string // for tool
  status?: 'running' | 'completed' | 'failed' // for tool
  output?: string // for tool result
  errorMessage?: string // for tool error
  isExpanded?: boolean // for tool UI state
}

export interface Message {
  role: 'system' | 'user' | 'assistant' | 'tool'
  content: string
  timestamp?: string
  parts?: MessagePart[]
}

export interface ChatCompletionRequest {
  messages: Message[]
  model?: string
  temperature?: number
  max_tokens?: number
  stream?: boolean
}

export interface ChatCompletionResponse {
  id: string
  object: string
  created: number
  model: string
  choices: Array<{
    index: number
    message: {
      role: string
      content: string
    }
    finish_reason: string
  }>
  usage: {
    prompt_tokens: number
    completion_tokens: number
    total_tokens: number
  }
}

// API响应通用类型
export interface ApiResponse<T> {
  data?: T
  error?: string
  message?: string
}
