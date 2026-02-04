<template>
  <div class="chat-view">
    <div class="chat-container">
      <div class="chat-messages" ref="messagesContainer" @scroll="handleScroll">
        <div v-if="showRunningStatus" class="session-status">
          <el-tag type="warning" size="small">运行中</el-tag>
          <span>会话仍在后台执行</span>
        </div>

        <!-- 加载更多消息按钮 -->
        <div v-if="chatStore.hasMoreMessages()" class="load-more-container">
          <el-button
            text
            @click="handleLoadMore"
            :loading="loadingMore"
            class="load-more-button"
          >
            加载更多消息
          </el-button>
        </div>

        <div v-if="messages.length === 0" class="empty-state">
          <el-icon :size="64" color="#c0c4cc"><ChatDotRound /></el-icon>
          <p>开始与AI助手对话吧</p>
        </div>

        <div
          v-for="(message, index) in messages"
          :key="index"
          class="message-item"
          :class="message.role"
        >
          <div class="message-avatar">
            <el-icon v-if="message.role === 'user'"><User /></el-icon>
            <el-icon v-else-if="message.role === 'tool'"><Tools /></el-icon>
            <el-icon v-else><Promotion /></el-icon>
          </div>
          <div class="message-content">
            <div class="message-role">
              {{ message.role === 'user' ? '你' : message.role === 'tool' ? '工具' : 'AI助手' }}
            </div>
            
            <div v-if="message.role === 'assistant'" class="assistant-content-wrapper">
              <!-- Render parts if available -->
              <template v-if="message.parts && message.parts.length > 0">
                <div v-for="(item, pIndex) in groupParts(message.parts)" :key="pIndex" class="message-part-wrapper">
                  <!-- Text Part -->
                  <div v-if="item.type === 'text' && item.content" class="message-bubble">
                    <MarkdownRenderer :content="item.content" />
                  </div>
                  
                  <!-- Tool Group (非审批工具) -->
                  <div v-else-if="item.type === 'tool-group'" class="tool-group-container">
                    <div class="tool-group-header" @click="toggleToolGroup(item)">
                      <div class="tool-group-title">
                        <el-icon :class="{ 'is-expanded': getToolGroupExpanded(item) }"><ArrowRight /></el-icon>
                        <el-icon><Tools /></el-icon>
                        <span>工具调用 ({{ item.parts.length }})</span>
                      </div>
                      <div class="tool-group-status">
                        <div v-if="getToolGroupStatus(item.parts) === 'running'" class="status-loading">
                          <span class="loading-ring"></span>
                        </div>
                        <el-icon v-else-if="getToolGroupStatus(item.parts) === 'completed'" class="status-success"><Check /></el-icon>
                        <el-icon v-else-if="getToolGroupStatus(item.parts) === 'failed'" class="status-error"><Close /></el-icon>
                      </div>
                    </div>
                    <div v-show="getToolGroupExpanded(item)" class="tool-group-content" :class="{ 'is-expanded': getToolGroupExpanded(item) }">
                      <div v-for="(part, tIndex) in item.parts" :key="tIndex" class="tool-part-wrapper">
                        <div
                          class="tool-item"
                          :class="{
                            'tool-completed': part.status === 'completed',
                            'tool-failed': part.status === 'failed'
                          }"
                        >
                          <div class="tool-header" @click.stop="toggleTool(part)">
                            <div class="tool-title">
                              <el-icon :class="{ 'is-expanded': getToolExpanded(part) }"><ArrowRight /></el-icon>
                              <el-icon><Tools /></el-icon>
                              <span>调用工具: {{ part.toolName }}</span>
                              <el-tag v-if="isSensitiveTool(part.toolName)" type="warning" size="small" class="sensitive-tag">
                                <el-icon><Warning /></el-icon>
                                敏感操作
                              </el-tag>
                            </div>
                            <div class="tool-status">
                              <div v-if="part.status === 'running' || !part.status" class="status-loading">
                                <span class="loading-ring"></span>
                              </div>
                              <el-icon v-else-if="part.status === 'completed'" class="status-success"><Check /></el-icon>
                              <el-icon v-else-if="part.status === 'failed'" class="status-error"><Close /></el-icon>
                            </div>
                          </div>

                          <div v-show="getToolExpanded(part)" class="tool-content" :class="{ 'is-expanded': getToolExpanded(part) }">
                            <div class="tool-detail-grid">
                              <div class="tool-detail-item">
                                <span class="label">工具名称:</span>
                                <span class="value code">{{ part.toolName }}</span>
                              </div>
                              <div class="tool-detail-item">
                                <span class="label">调用ID:</span>
                                <span class="value code">{{ part.toolId }}</span>
                              </div>
                              <div v-if="part.toolInput" class="tool-detail-item full-width">
                                <span class="label">输入参数:</span>
                                <div class="code-block">
                                  <pre>{{ formatToolInput(part.toolInput) }}</pre>
                                </div>
                              </div>
                            </div>
                            <div v-if="isSensitiveTool(part.toolName)" class="tool-approval-note">
                              <el-icon color="#E6A23C"><Warning /></el-icon>
                              <span>此操作为敏感操作，已触发审批流程</span>
                            </div>
                            <div v-if="part.status === 'completed' && part.output" class="tool-result-output">
                              <div class="result-label">执行结果:</div>
                              <div class="code-block">
                                <pre>{{ part.output }}</pre>
                              </div>
                            </div>
                            <div v-if="part.status === 'failed' && part.errorMessage" class="tool-result-error">
                              <div class="result-label">异常信息:</div>
                              <div class="code-block">
                                <pre>{{ part.errorMessage }}</pre>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                  
                  <!-- 审批工具独立显示 -->
                  <div v-else-if="item.type === 'approval-tool'" class="tool-group-container approval-tool-container">
                    <div class="tool-approval-banner">
                      <el-icon color="#E6A23C"><Warning /></el-icon>
                      <span>等待审批</span>
                    </div>
                    <div v-for="(part, tIndex) in item.parts" :key="tIndex" class="tool-part-wrapper">
                      <div
                        class="tool-item"
                        :class="{
                          'tool-needs-approval': part.needsApproval && part.status === 'running'
                        }"
                      >
                        <div class="tool-header" @click.stop="toggleTool(part)">
                          <div class="tool-title">
                            <el-icon :class="{ 'is-expanded': getToolExpanded(part) }"><ArrowRight /></el-icon>
                            <el-icon><Tools /></el-icon>
                            <span>调用工具: {{ part.toolName }}</span>
                            <el-tag type="warning" size="small" class="sensitive-tag">
                              <el-icon><Warning /></el-icon>
                              敏感操作
                            </el-tag>
                          </div>
                          <div class="tool-status">
                            <div v-if="part.status === 'running' || !part.status" class="status-loading">
                              <span class="loading-ring"></span>
                            </div>
                            <el-icon v-else-if="part.status === 'completed'" class="status-success"><Check /></el-icon>
                            <el-icon v-else-if="part.status === 'failed'" class="status-error"><Close /></el-icon>
                          </div>
                        </div>

                        <div v-show="getToolExpanded(part)" class="tool-content" :class="{ 'is-expanded': getToolExpanded(part) }">
                          <div class="tool-detail-grid">
                            <div class="tool-detail-item">
                              <span class="label">工具名称:</span>
                              <span class="value code">{{ part.toolName }}</span>
                            </div>
                            <div class="tool-detail-item">
                              <span class="label">调用ID:</span>
                              <span class="value code">{{ part.toolId }}</span>
                            </div>
                            <div v-if="part.toolInput" class="tool-detail-item full-width">
                              <span class="label">输入参数:</span>
                              <div class="code-block">
                                <pre>{{ formatToolInput(part.toolInput) }}</pre>
                              </div>
                            </div>
                          </div>
                          <div class="tool-approval-note">
                            <el-icon color="#E6A23C"><Warning /></el-icon>
                            <span>此操作为敏感操作，需要您的审批</span>
                          </div>
                        </div>
                      </div>

                      <div v-if="part.needsApproval && part.status === 'running'" class="tool-approval-actions">
                        <el-button
                          type="success"
                          size="small"
                          @click.stop="handleToolApprove(part)"
                          :loading="approvingTool === part.toolId"
                          :disabled="approvingTool !== null"
                        >
                          <el-icon><Check /></el-icon>
                          通过
                        </el-button>
                        <el-button
                          type="danger"
                          size="small"
                          @click.stop="handleToolReject(part)"
                          :loading="rejectingTool === part.toolId"
                          :disabled="rejectingTool !== null"
                        >
                          <el-icon><Close /></el-icon>
                          拒绝
                        </el-button>
                      </div>
                    </div>
                  </div>
                </div>
                <div v-if="!hasRenderableText(message) && message.content" class="message-bubble">
                  <MarkdownRenderer :content="message.content" />
                </div>
              </template>
              
              <!-- Fallback for legacy messages without parts -->
              <div v-else-if="message.content" class="message-bubble">
                <MarkdownRenderer :content="message.content" />
              </div>
              
              <!-- Session End Indicator -->
              <div v-if="index === messages.length - 1 && !loading && !showRunningStatus && message.role === 'assistant'" class="session-end-indicator">
                <el-icon><Check /></el-icon>
                <span>本次会话已结束</span>
              </div>
            </div>
            
            <div v-else class="plain-text">{{ message.content }}</div>
            
            <div v-if="message.timestamp" class="message-time">
              {{ formatTime(message.timestamp) }}
            </div>
          </div>
        </div>

        <div v-if="loading || showRunningStatus" class="message-item assistant">
          <div class="message-avatar">
            <el-icon><Promotion /></el-icon>
          </div>
          <div class="message-content">
            <div class="message-role">AI助手</div>
            <div class="typing-indicator">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
        </div>
      </div>

      <div class="chat-input-container">
        <div class="input-wrapper">
          <div v-if="pendingFile" class="file-chip">
            <el-icon><Paperclip /></el-icon>
            <span class="file-name">{{ pendingFile.name }}</span>
            <el-icon class="close-icon" @click="removePendingFile"><Close /></el-icon>
          </div>
          <div class="input-actions">
            <div class="workspace-config">
              <el-button
                :icon="FolderOpened"
                circle
                text
                @click="showWorkspaceDialog = true"
                title="配置工作区"
                class="action-btn"
              />
            </div>
            <input
              type="file"
              ref="fileInput"
              style="display: none"
              @change="handleFileSelect"
            />
            <el-button
              :icon="Paperclip"
              circle
              text
              @click="triggerFileUpload"
              title="上传文件"
              class="action-btn"
            />
            <el-input
              v-model="inputMessage"
              type="textarea"
              :rows="1"
              :autosize="{ minRows: 1, maxRows: 6 }"
              placeholder="输入消息..."
              resize="none"
              :disabled="loading || showRunningStatus || approvalStore.hasPendingApprovals"
              @keydown.enter.prevent="handleSend"
            />
            <el-button
              type="primary"
              :icon="Promotion"
              circle
              @click="handleSend"
              :disabled="(!inputMessage.trim() && !pendingFile) || loading || showRunningStatus || approvalStore.hasPendingApprovals"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- 会话工作区配置对话框 -->
    <SessionWorkspaceDialog
      v-model="showWorkspaceDialog"
      :session-id="activeSessionId"
      :current-work-directory="currentWorkDirectory"
      @save="handleWorkspaceSaved"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ChatDotRound, User, Promotion, Tools, Paperclip, Close, ArrowRight, Check, FolderOpened, Warning } from '@element-plus/icons-vue'
import { useChatStore } from '@/stores/chat'
import { useSessionStore } from '@/stores/session'
import { useApprovalStore } from '@/stores/approval'
import { useUserStore } from '@/stores/user'
import MarkdownRenderer from '@/components/MarkdownRenderer.vue'
import SessionWorkspaceDialog from '@/components/SessionWorkspaceDialog.vue'
import { ElMessage, ElMessageBox } from 'element-plus'

const route = useRoute()
const router = useRouter()
const chatStore = useChatStore()
const sessionStore = useSessionStore()
const approvalStore = useApprovalStore()
const userStore = useUserStore()

const inputMessage = ref('')
const messagesContainer = ref<HTMLElement>()
const fileInput = ref<HTMLInputElement>()
const pendingFile = ref<File | null>(null)
const loadingMore = ref(false)
const isScrolling = ref(false)
const showWorkspaceDialog = ref(false)
const currentWorkDirectory = ref('')
const approvingTool = ref<string | null>(null)
const rejectingTool = ref<string | null>(null)

// 工具展开状态管理 - 使用响应式 Map
const toolGroupExpandState = ref<Map<string, boolean>>(new Map())
const toolExpandState = ref<Map<string, boolean>>(new Map())

const messages = computed(() => chatStore.messages)
const loading = computed(() => chatStore.loading)
const currentSession = computed(() => sessionStore.currentSession)
const currentStatus = computed(() => {
  if (!sessionId.value) return null
  return sessionStore.sessionStatus[sessionId.value]
})
const showRunningStatus = computed(() => {
  if (!currentStatus.value) return false
  // 会话正在运行或暂停（等待审批）时都显示运行状态
  return currentStatus.value.runtimeState === 'Working' ||
         currentStatus.value.runtimeState === 'Paused' ||
         currentStatus.value.breakpointState === 'AwaitingApproval'
})

const sessionId = computed(() => {
  return route.params.sessionId as string
})

const activeSessionId = computed(() => {
  return currentSession.value?.sessionId || sessionId.value || ''
})

// 监听会话变化，加载工作区配置
watch(activeSessionId, async (newSessionId) => {
  if (newSessionId) {
    await loadSessionWorkspace(newSessionId)
  }
})

async function loadSessionWorkspace(sessionId: string) {
  try {
    const response = await fetch(
      `/api/sessionworkspaces/sessions/${sessionId}?userId=default-user-001`
    )

    if (response.ok) {
      const workspace = await response.json()
      currentWorkDirectory.value = workspace.workDirectory || ''
    } else {
      currentWorkDirectory.value = ''
    }
  } catch (error) {
    console.error('Failed to load session workspace:', error)
    currentWorkDirectory.value = ''
  }
}

async function handleWorkspaceSaved(sessionId: string, workDirectory: string) {
  currentWorkDirectory.value = workDirectory
  if (sessionId) {
    await loadSessionWorkspace(sessionId)
  }
  ElMessage.success('会话工作区已更新')
}

// 监听会话变化
watch(sessionId, async (newSessionId) => {
  if (newSessionId) {
    // 查找或设置当前会话
    const session = sessionStore.sessions.find(s => s.sessionId === newSessionId)
    if (session) {
      sessionStore.setCurrentSession(session)
    }

    // 加载会话消息
    chatStore.loadMessages(newSessionId)

    // 刷新待审批列表
    await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId.value)

    startStatusPolling(newSessionId)
  }
})

// 监听消息变化，自动滚动到底部
watch(messages, () => {
  nextTick(() => {
    // 只有不是用户手动滚动时才自动滚动
    if (!isScrolling.value) {
      scrollToBottom({ smooth: true })
    }
  })
}, { deep: true })

// 监听消息变化，平滑滚动到底部
watch(messages, () => {
  nextTick(() => {
    // 只在新消息添加时滚动
    if (!isScrolling.value) {
      scrollToBottom({ smooth: true })
    }
  })
}, { deep: true })

// 滚动处理
let scrollTimeout: ReturnType<typeof setTimeout> | null = null
function handleScroll(event: Event) {
  isScrolling.value = true

  // 清除之前的定时器
  if (scrollTimeout) {
    clearTimeout(scrollTimeout)
  }

  // 停止滚动1秒后重置标志
  scrollTimeout = setTimeout(() => {
    isScrolling.value = false
  }, 1000)

  // 检测滚动到顶部，显示加载更多提示
  const container = event.target as HTMLElement
  if (container.scrollTop < 50 && chatStore.hasMoreMessages() && !loadingMore.value) {
    // 可以在这里自动加载，或者让用户手动点击
  }
}

function scrollToBottom(options: { smooth?: boolean } = {}) {
  if (messagesContainer.value) {
    const { smooth = true } = options
    messagesContainer.value.scrollTo({
      top: messagesContainer.value.scrollHeight,
      behavior: smooth ? 'smooth' : 'auto'
    })
  }
}

// 加载更多消息
async function handleLoadMore() {
  if (loadingMore.value || !chatStore.hasMoreMessages()) return

  const oldScrollTop = messagesContainer.value?.scrollTop || 0
  const oldScrollHeight = messagesContainer.value?.scrollHeight || 0

  loadingMore.value = true
  try {
    const hasMore = chatStore.loadMoreMessages()

    if (hasMore) {
      // 等待DOM更新后恢复滚动位置
      await nextTick()
      if (messagesContainer.value) {
        const newScrollHeight = messagesContainer.value.scrollHeight
        const heightDiff = newScrollHeight - oldScrollHeight
        messagesContainer.value.scrollTop = oldScrollTop + heightDiff
      }
    }
  } catch (error) {
    console.error('Failed to load more messages:', error)
  } finally {
    loadingMore.value = false
  }
}

function triggerFileUpload() {
  fileInput.value?.click()
}

async function handleFileSelect(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  // Check file size (e.g. 10MB)
  if (file.size > 10 * 1024 * 1024) {
    ElMessage.warning('文件大小不能超过 10MB')
    target.value = ''
    return
  }

  pendingFile.value = file
  target.value = ''
}

function removePendingFile() {
  pendingFile.value = null
}

async function handleSend() {
  if (loading.value || showRunningStatus.value) return
  const content = inputMessage.value.trim()
  if (!content && !pendingFile.value) return

  // 检查是否有待审批事项或会话处于等待审批状态
  if (approvalStore.hasPendingApprovals ||
      (currentStatus.value && currentStatus.value.breakpointState === 'AwaitingApproval')) {
    try {
      await ElMessageBox.confirm(
        '当前有待处理的审批事项，请先处理审批后再发送新消息。是否继续发送？',
        '提示',
        {
          confirmButtonText: '继续发送',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )
    } catch {
      // 用户点击了取消，阻止发送消息
      return
    }
  }

  try {
    let finalContent = content

    // Upload file if exists
    if (pendingFile.value) {
      try {
        const size = formatSize(pendingFile.value.size)
        // 获取当前会话ID，传递给上传接口
        const activeSessionId = currentSession.value?.sessionId || sessionId.value || undefined
        const res = await chatStore.uploadFile(pendingFile.value, activeSessionId)
        // Format: 📄 文件: [renamed_name](url) (size)
        const fileLink = `📄 文件: [${res.fileName}](${res.url}) (${size})`
        finalContent = content ? `${content}\n\n${fileLink}` : fileLink
        pendingFile.value = null
      } catch (error) {
        ElMessage.error('文件上传失败')
        console.error(error)
        return
      }
    }

    await handleSendMessage(finalContent)
    inputMessage.value = ''
  } catch (error) {
    console.error('发送失败:', error)
  }
}

async function handleSendMessage(content: string) {
  try {
    // 如果没有会话，创建新会话
    if (!currentSession.value) {
      await sessionStore.createSession(userStore.DEFAULT_USER_ID, {
        title: content.slice(0, 20)
      })
    }

    const activeSessionId = currentSession.value?.sessionId || sessionId.value || null

    try {
      // 发送消息
      await chatStore.sendMessage(activeSessionId, content, userStore.DEFAULT_USER_ID)

      // 更新会话标题
      if (currentSession.value && messages.value.length <= 2) {
        await sessionStore.updateSession(currentSession.value.sessionId, content.slice(0, 20))
      }

      // 刷新待审批列表（发送消息后立即刷新）
      await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId || '')
      if (activeSessionId) {
        await sessionStore.loadSessionStatus(activeSessionId)
      }

      // 启动轮询监听审批状态变化
      startApprovalPolling()
    } catch (err: any) {
      console.error('发送消息失败:', err)

      // 根据错误类型显示不同的提示
      if (err.message?.includes('Failed to fetch')) {
        ElMessage.error('网络连接失败，请检查网络设置')
      } else {
        ElMessage.error(err.message || '发送消息失败，请重试')
      }
      throw err
    }
  } catch (error: any) {
    console.error('创建会话失败:', error)
    ElMessage.error(error.message || '操作失败')
  }
}

// 启动审批状态轮询
let approvalPollingTimer: number | undefined
function startApprovalPolling() {
  stopApprovalPolling() // 先停止之前的轮询
  approvalPollingTimer = window.setInterval(async () => {
    if (approvalStore.hasPendingApprovals) {
      console.log('Polling for approval updates...')
      await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId.value)
    }
  }, 5000) // 每5秒检查一次
}

function stopApprovalPolling() {
  if (approvalPollingTimer) {
    clearInterval(approvalPollingTimer)
    approvalPollingTimer = undefined
  }
}

// 生成工具组ID
function getToolGroupId(group: any): string {
  return group.parts?.map((p: any) => p.toolId || p.id).join('_') || ''
}

// 获取工具组展开状态
function getToolGroupExpanded(group: any): boolean {
  const groupId = getToolGroupId(group)
  return toolGroupExpandState.value.get(groupId) ?? false
}

// 获取工具展开状态
function getToolExpanded(part: any): boolean {
  const toolId = part.toolId || part.id
  return toolExpandState.value.get(toolId) ?? false
}

function toggleTool(part: any) {
  const toolId = part.toolId || part.id
  if (!toolId) return
  
  // 切换展开状态
  const currentState = getToolExpanded(toolId)
  toolExpandState.value.set(toolId, !currentState)
  
  // 触发响应式更新
  toolExpandState.value = new Map(toolExpandState.value)
}

function toggleToolGroup(group: any) {
  // 生成组ID
  const groupId = getToolGroupId(group)
  if (!groupId) return

  // 切换展开状态
  const currentState = toolGroupExpandState.value.get(groupId) ?? false
  const newState = !currentState
  toolGroupExpandState.value.set(groupId, newState)

  // 展开时同时展开所有工具详情，收起时同时收起所有工具
  if (group.parts && group.parts.length > 0) {
    group.parts.forEach((part: any) => {
      const toolId = part.toolId || part.id
      if (toolId) {
        toolExpandState.value.set(toolId, newState)
      }
    })
  }

  // 触发响应式更新
  toolGroupExpandState.value = new Map(toolGroupExpandState.value)
  toolExpandState.value = new Map(toolExpandState.value)
}

function formatSize(bytes: number) {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function formatTime(time: string) {
  const date = parseUtc8Date(time)
  if (!date) return ''
  const utcMs = date.getTime()
  const utc8 = new Date(utcMs + 8 * 60 * 60 * 1000)
  const yyyy = utc8.getUTCFullYear()
  const mm = String(utc8.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(utc8.getUTCDate()).padStart(2, '0')
  const hh = String(utc8.getUTCHours()).padStart(2, '0')
  const mi = String(utc8.getUTCMinutes()).padStart(2, '0')
  const ss = String(utc8.getUTCSeconds()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}`
}

onMounted(async () => {
  // 如果没有会话ID，尝试加载最近会话
  if (!sessionId.value) {
    await sessionStore.loadSessions(userStore.DEFAULT_USER_ID)
    const recentSession = sessionStore.sessions[0]
    if (recentSession) {
      router.push(`/chat/${recentSession.sessionId}`)
    } else {
      handleCreateNewSession()
    }
  } else {
    chatStore.loadMessages(sessionId.value)
    startStatusPolling(sessionId.value)
  }

  scrollToBottom()
})

onUnmounted(() => {
  stopStatusPolling()
  stopApprovalPolling()
})

async function handleCreateNewSession() {
  try {
    const newSession = await sessionStore.createSession(userStore.DEFAULT_USER_ID, {
      title: '新对话'
    })
    chatStore.clearMessages(newSession.sessionId)
    chatStore.loadMessages(newSession.sessionId)
  } catch (error) {
    console.error('创建会话失败:', error)
  }
}

let statusTimer: number | undefined

function startStatusPolling(activeSessionId: string) {
  stopStatusPolling()
  sessionStore.loadSessionStatus(activeSessionId)
  statusTimer = window.setInterval(() => {
    sessionStore.loadSessionStatus(activeSessionId)
  }, 5000)
}

function stopStatusPolling() {
  if (statusTimer) {
    clearInterval(statusTimer)
    statusTimer = undefined
  }
}

function parseUtc8Date(time: string) {
  if (!time) return null
  const trimmed = time.trim()
  const hasZone = /z$/i.test(trimmed) || /[+-]\d{2}:?\d{2}$/.test(trimmed)
  const normalized = hasZone
    ? trimmed
    : trimmed.includes(' ') && !trimmed.includes('T')
      ? `${trimmed.replace(' ', 'T')}+08:00`
      : `${trimmed}+08:00`
  const date = new Date(normalized)
  if (Number.isNaN(date.getTime())) return null
  return date
}

function groupParts(parts: any[]) {
  if (!parts) return []
  const groups: any[] = []
  let toolGroup: any | null = null
  let approvalToolGroup: any | null = null

  for (const part of parts) {
    if (part.type === 'tool') {
      // 确保每个part都有isExpanded属性
      if (typeof part.isExpanded === 'undefined') {
        part.isExpanded = false
      }
      
      // 判断是否为需要审批的工具
      if (part.needsApproval) {
        // 审批工具单独分组
        if (!approvalToolGroup) {
          approvalToolGroup = { type: 'approval-tool', parts: [], isExpanded: true }
        }
        approvalToolGroup.parts.push(part)
      } else {
        // 普通工具分组
        if (!toolGroup) {
          toolGroup = { type: 'tool-group', parts: [], isExpanded: false }
          groups.push(toolGroup)
        }
        toolGroup.parts.push(part)
      }
    } else {
      // 非工具类型的part，如果有待处理的审批工具组，先添加它
      if (approvalToolGroup) {
        groups.push(approvalToolGroup)
        approvalToolGroup = null
      }
      groups.push(part)
    }
  }
  
  // 最后如果有审批工具组，添加到末尾
  if (approvalToolGroup) {
    groups.push(approvalToolGroup)
  }
  
  return groups
}

function getToolGroupStatus(parts: any[]) {
  if (!parts || parts.length === 0) return 'completed'
  if (parts.some((p: any) => p.status === 'failed')) return 'failed'
  if (parts.some((p: any) => p.status === 'running' || !p.status)) return 'running'
  return 'completed'
}

function hasRenderableText(message: any) {
  if (!message?.parts) return false
  return message.parts.some((part: any) => part.type === 'text' && part.content && part.content.trim() !== '')
}

// 判断是否为敏感工具
function isSensitiveTool(toolName: string): boolean {
  if (!toolName) return false
  const normalizedName = toolName.toLowerCase()
  // 删除操作工具
  const deleteTools = ['fs_rm', 'fs_delete', 'fs_remove', 'file_delete', 'file_remove', 'rm', 'delete', 'remove']
  // 执行操作工具
  const executeTools = ['bash_execute', 'bash_exec', 'bash', 'shell_run', 'shell_execute', 'shell_exec', 'shell', 'execute', 'exec', 'run_command']
  // 写入操作工具
  const writeTools = ['fs_write', 'fs_edit', 'fs_create', 'file_write', 'file_edit', 'file_create', 'write', 'edit', 'create_file']

  return [...deleteTools, ...executeTools, ...writeTools].includes(normalizedName)
}

// 格式化工具输入参数
function formatToolInput(input: any): string {
  if (!input) return ''
  try {
    if (typeof input === 'string') {
      // 尝试解析JSON字符串
      const parsed = JSON.parse(input)
      return JSON.stringify(parsed, null, 2)
    }
    return JSON.stringify(input, null, 2)
  } catch {
    return String(input)
  }
}

// 处理工具审批通过
async function handleToolApprove(part: any) {
  try {
    approvingTool.value = part.toolId

    // 如果没有approvalId，尝试从待审批列表中查找
    let approvalId = part.approvalId

    if (!approvalId) {
      console.warn('Part has no approvalId, trying to find from pending approvals')
      await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId.value)

      // 从待审批列表中查找匹配的审批记录
      const pendingApproval = approvalStore.pendingApprovals.find((a: any) => {
        const matchesSession = !activeSessionId.value ||
                          a.sessionId === activeSessionId.value ||
                          a.agentId === activeSessionId.value
        if (!matchesSession) return false
        if (a.callId && a.callId === part.toolId) return true
        if (a.toolName && a.toolName.toLowerCase() === (part.toolName || '').toLowerCase()) return true
        return false
      })

      if (pendingApproval) {
        approvalId = pendingApproval.approvalId
        // 保存到part中，下次直接使用
        part.approvalId = approvalId
      } else {
        throw new Error('未找到对应的审批记录，请确认审批是否已创建')
      }
    }

    if (!approvalId) {
      throw new Error('无法获取审批ID')
    }

    console.log('Approving tool with ID:', approvalId)

    const response = await fetch(`/api/approvals/${approvalId}/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ note: '用户通过审批' })
    })

    if (!response.ok) {
      const errorText = await response.text()
      console.error('Approve failed:', errorText)
      throw new Error(errorText || '审批通过失败')
    }

    const result = await response.json()
    ElMessage.success(result.message || '审批已通过，工具执行将继续')

    // 刷新待审批列表
    await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId.value)

    // 标记工具为等待执行状态
    part.approvalId = undefined
    part.needsApproval = false
  } catch (error: any) {
    console.error('Failed to approve tool:', error)
    ElMessage.error(error.message || '审批通过失败')
  } finally {
    approvingTool.value = null
  }
}

// 处理工具审批拒绝
async function handleToolReject(part: any) {
  try {
    rejectingTool.value = part.toolId

    // 如果没有approvalId，尝试从待审批列表中查找
    let approvalId = part.approvalId

    if (!approvalId) {
      console.warn('Part has no approvalId, trying to find from pending approvals')
      await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId.value)

      // 从待审批列表中查找匹配的审批记录
      const pendingApproval = approvalStore.pendingApprovals.find((a: any) => {
        const matchesSession = !activeSessionId.value ||
                          a.sessionId === activeSessionId.value ||
                          a.agentId === activeSessionId.value
        if (!matchesSession) return false
        if (a.callId && a.callId === part.toolId) return true
        if (a.toolName && a.toolName.toLowerCase() === (part.toolName || '').toLowerCase()) return true
        return false
      })

      if (pendingApproval) {
        approvalId = pendingApproval.approvalId
        // 保存到part中，下次直接使用
        part.approvalId = approvalId
      } else {
        throw new Error('未找到对应的审批记录，请确认审批是否已创建')
      }
    }

    if (!approvalId) {
      throw new Error('无法获取审批ID')
    }

    console.log('Rejecting tool with ID:', approvalId)

    const response = await fetch(`/api/approvals/${approvalId}/reject`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason: '用户拒绝执行' })
    })

    if (!response.ok) {
      const errorText = await response.text()
      console.error('Reject failed:', errorText)
      throw new Error(errorText || '拒绝失败')
    }

    const result = await response.json()
    ElMessage.success(result.message || '已拒绝执行')

    // 更新工具状态为失败
    part.status = 'failed'
    part.errorMessage = '用户拒绝执行'
    part.needsApproval = false

    // 刷新待审批列表
    await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID, activeSessionId.value)
  } catch (error: any) {
    console.error('Failed to reject tool:', error)
    ElMessage.error(error.message || '拒绝失败')
  } finally {
    rejectingTool.value = null
  }
}
</script>

<style scoped>
.chat-view {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background-color: #f4f5f7;
}

.chat-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  position: relative;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  padding-bottom: 20px;
  background: transparent;
  min-height: 0;
  scroll-behavior: smooth;
  scroll-snap-type: y proximity;
}

.load-more-container {
  text-align: center;
  padding: 12px 0;
  margin-bottom: 16px;
}

.load-more-button {
  color: #409eff;
  padding: 8px 16px;
}

.session-status {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #fffbe6;
  border: 1px solid #faecd8;
  border-radius: 6px;
  margin-bottom: 12px;
  color: #8c6d1f;
  font-size: 13px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 120px;
  padding: 24px 0;
  color: #909399;
}

.empty-state p {
  margin-top: 16px;
  font-size: 16px;
}

.message-item {
  display: flex;
  gap: 16px;
  margin-bottom: 24px;
  animation: fadeIn 0.3s ease-in;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.message-item.user {
  flex-direction: row-reverse;
}

.message-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.message-item.user .message-avatar {
  background: #409eff;
  color: #fff;
  margin-left: 0;
}

.message-item.assistant .message-avatar {
  background: #67c23a;
  color: #fff;
  margin-right: 0;
}

.message-content {
  max-width: 80%;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.message-bubble {
  padding: 12px 16px;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  font-size: 14px;
  line-height: 1.6;
  color: #1f2329;
}

.user .message-bubble {
  background: #409eff;
  color: #fff;
  border-radius: 12px 12px 0 12px;
}

.assistant .message-bubble {
  border-radius: 0 12px 12px 12px;
}

/* Tool Call Styles - Enhanced */
.message-part-wrapper {
  margin-bottom: 8px;
}

.message-part-wrapper:last-child {
  margin-bottom: 0;
}

.tool-group-container {
  background: #fff;
  border-radius: 8px;
  border: 1px solid #e5e6eb;
  overflow: hidden;
  margin: 4px 0;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.tool-group-container.approval-tool-container {
  border-color: #E6A23C;
  box-shadow: 0 2px 8px rgba(230, 162, 60, 0.15);
}

.tool-approval-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: #fef9e7;
  border-bottom: 1px solid #f3d0a8;
  font-size: 13px;
  color: #e6a23c;
  font-weight: 500;
}

.tool-group-content {
  max-height: 0;
  overflow: hidden;
  transition: max-height 0.3s ease-out, padding 0.3s ease;
  padding: 0;
}

.tool-group-content.is-expanded {
  max-height: 2000px;
  padding: 4px 0;
  overflow: visible;
}

.tool-item.tool-needs-approval {
  background-color: #fef9e7;
  border-left: 4px solid #E6A23C;
}

.tool-group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  background: #f5f7fa;
  border-bottom: 1px solid #e5e6eb;
  font-size: 13px;
  color: #1f2329;
  cursor: pointer;
  user-select: none;
  transition: background 0.2s;
}

.tool-group-header:hover {
  background: #e4e7ed;
}

.tool-group-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
}

.tool-group-title .el-icon {
  transition: transform 0.2s ease;
}

.tool-group-title .el-icon.is-expanded {
  transform: rotate(90deg);
}

.tool-part-wrapper {
  border-bottom: 1px solid #e5e6eb;
}

.tool-part-wrapper:last-child {
  border-bottom: none;
}

.tool-item {
  transition: background-color 0.3s ease, border-color 0.3s ease;
}

.tool-item.tool-completed {
  background-color: #f0f9ff;
  border-left: 4px solid #67c23a;
}

.tool-item.tool-failed {
  background-color: #fef0f0;
  border-left: 4px solid #f56c6c;
}

.tool-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  cursor: pointer;
  user-select: none;
  background: #fff;
  transition: background 0.2s;
}

.tool-header:hover {
  background: #f5f7fa;
}

.tool-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #1f2329;
  font-weight: 500;
  flex: 1;
  min-width: 0;
}

.tool-title .el-icon {
  transition: transform 0.2s ease;
  flex-shrink: 0;
}

.tool-title .el-icon.is-expanded {
  transform: rotate(90deg);
}

.sensitive-tag {
  margin-left: 8px;
  flex-shrink: 0;
}

.tool-status {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.status-success {
  color: #67c23a;
  font-size: 16px;
}

.status-loading {
  display: flex;
  align-items: center;
  gap: 4px;
}

.status-loading .loading-ring {
  width: 18px;
  height: 18px;
  border: 2px solid #409eff;
  border-top-color: transparent;
  border-radius: 50%;
  animation: loading-spin 1s linear infinite;
}

@keyframes loading-spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.tool-content {
  max-height: 0;
  overflow: hidden;
  transition: max-height 0.3s ease-out, padding 0.3s ease;
  padding: 0 12px;
  background: #f9fafe;
  font-size: 12px;
  color: #606266;
  border-top: 1px solid transparent;
}

.tool-content.is-expanded {
  max-height: 1500px;
  padding: 12px;
  border-top-color: #e5e6eb;
  overflow: visible;
}

.tool-detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px 16px;
  margin-bottom: 8px;
}

.tool-detail-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.tool-detail-item.full-width {
  grid-column: 1 / -1;
}

.tool-detail-item .label {
  color: #909399;
  font-weight: 500;
  font-size: 11px;
  text-transform: uppercase;
}

.tool-detail-item .value {
  color: #303133;
  font-size: 12px;
}

.tool-detail-item .value.code {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  background: #fff;
  padding: 4px 8px;
  border-radius: 4px;
  border: 1px solid #e4e7ed;
  word-break: break-all;
}

.code-block {
  background: #1e1e1e;
  border-radius: 6px;
  padding: 12px;
  margin-top: 4px;
  overflow-x: auto;
}

.code-block pre {
  margin: 0;
  color: #d4d4d4;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}

.tool-approval-note {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background-color: #fef9e7;
  border: 1px solid #f3d0a8;
  border-radius: 4px;
  font-size: 13px;
  color: #e6a23c;
  margin-top: 8px;
  font-weight: 500;
}

.tool-approval-actions {
  display: flex;
  gap: 8px;
  margin-top: 12px;
  padding: 12px;
  background: #f5f7fa;
  border-top: 1px solid #e5e6eb;
}

.status-error {
  color: #f56c6c;
  font-size: 16px;
}

.tool-result-output,
.tool-result-error {
  margin-top: 12px;
  background: #fff;
  border: 1px solid #e5e6eb;
  border-radius: 6px;
  padding: 12px;
}

.tool-result-output {
  border-color: #d9ecff;
}

.tool-result-error {
  border-color: #fde2e2;
  background: #fef0f0;
}

.result-label {
  font-weight: 500;
  margin-bottom: 8px;
  color: #606266;
  font-size: 13px;
}

.tool-result-error .result-label {
  color: #f56c6c;
}

.plain-text {
  white-space: pre-wrap;
  word-break: break-word;
}

.message-time {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  text-align: right;
  opacity: 0.8;
}

/* Doubao Style Input Area */
.chat-input-container {
  flex: 0 0 auto;
  padding: 0 24px 70px;
  background: transparent;
  display: flex;
  justify-content: center;
  width: 100%;
}

.input-wrapper {
  width: 100%;
  max-width: 800px;
  background: #fff;
  border-radius: 24px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: box-shadow 0.3s;
}

.input-wrapper:focus-within {
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
}

.input-actions {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  width: 100%;
  flex-wrap: wrap;
}

.workspace-config {
  display: flex;
  align-items: center;
  border-right: 1px solid #e4e7ed;
  padding-right: 12px;
  margin-right: 12px;
}

.workspace-config .el-button {
  color: #67c23a;
}

.workspace-config .el-button:hover {
  background-color: #f0f9ff;
}

.action-btn {
  color: #606266;
  font-size: 18px;
  padding: 8px;
}

.action-btn:hover {
  color: #409eff;
  background-color: #f0f7ff;
}

.file-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background-color: #f0f2f5;
  border-radius: 16px;
  font-size: 13px;
  color: #606266;
  max-width: 100%;
  width: fit-content;
  margin-left: 44px; /* Align with text start */
}

.file-name {
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.close-icon {
  cursor: pointer;
  color: #909399;
  font-size: 14px;
}

.close-icon:hover {
  color: #f56c6c;
}

.input-wrapper :deep(.el-textarea) {
  flex: 1;
}

.input-wrapper :deep(.el-textarea__inner) {
  border: none;
  box-shadow: none;
  padding: 8px 0;
  background: transparent;
  font-size: 14px;
  line-height: 1.5;
  min-height: 24px !important;
}

.input-wrapper :deep(.el-textarea__inner:focus) {
  box-shadow: none;
}

@media (max-width: 768px) {
  .chat-messages {
    padding: 16px;
  }
  
  .message-content {
    max-width: 90%;
  }

  .chat-input-container {
    padding: 16px;
  }
}

.session-end-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 8px;
  color: #909399;
  font-size: 12px;
  padding: 0 4px;
}
</style>
