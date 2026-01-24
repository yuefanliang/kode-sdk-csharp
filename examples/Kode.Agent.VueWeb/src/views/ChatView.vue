<template>
  <div class="chat-view">
    <div class="chat-container">
      <div class="chat-messages" ref="messagesContainer">
        <div v-if="showRunningStatus" class="session-status">
          <el-tag type="warning" size="small">运行中</el-tag>
          <span>会话仍在后台执行</span>
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
                  
                  <!-- Tool Group -->
                  <div v-else-if="item.type === 'tool-group'" class="tool-group-container">
                    <div v-for="(part, tIndex) in item.parts" :key="tIndex" class="tool-item">
                      <div class="tool-header" @click="toggleTool(part)">
                        <div class="tool-title">
                          <el-icon :class="{ 'is-expanded': part.isExpanded }"><ArrowRight /></el-icon>
                          <el-icon><Tools /></el-icon>
                          <span>调用工具: {{ part.toolName }}</span>
                        </div>
                        <div class="tool-status">
                           <div v-if="part.status === 'running' || !part.status" class="status-loading">
                             <span class="dot"></span>
                             <span class="dot"></span>
                             <span class="dot"></span>
                           </div>
                           <el-icon v-else-if="part.status === 'completed'" class="status-success"><Check /></el-icon>
                           <el-icon v-else-if="part.status === 'failed'" class="status-error"><Close /></el-icon>
                        </div>
                      </div>
                      <div v-if="part.isExpanded" class="tool-content">
                        <div class="tool-detail-item">
                          <span class="label">Call ID:</span>
                          <span class="value">{{ part.toolId }}</span>
                        </div>
                        <div v-if="part.status === 'completed' && part.output" class="tool-result-output">
                          <div class="result-label">执行结果:</div>
                          <pre>{{ part.output }}</pre>
                        </div>
                        <div v-if="part.status === 'failed' && part.errorMessage" class="tool-result-error">
                          <div class="result-label">异常信息:</div>
                          <pre>{{ part.errorMessage }}</pre>
                        </div>
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
              :disabled="loading || showRunningStatus"
              @keydown.enter.prevent="handleSend"
            />
            <el-button 
              type="primary" 
              :icon="Promotion" 
              circle 
              @click="handleSend"
              :disabled="(!inputMessage.trim() && !pendingFile) || loading || showRunningStatus"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ChatDotRound, User, Promotion, Position, Tools, Paperclip, Close, ArrowRight, Check } from '@element-plus/icons-vue'
import { useChatStore } from '@/stores/chat'
import { useSessionStore } from '@/stores/session'
import { useApprovalStore } from '@/stores/approval'
import { useUserStore } from '@/stores/user'
import MarkdownRenderer from '@/components/MarkdownRenderer.vue'
import { ElMessage } from 'element-plus'

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

const messages = computed(() => chatStore.messages)
const loading = computed(() => chatStore.loading)
const currentSession = computed(() => sessionStore.currentSession)
const currentStatus = computed(() => {
  if (!sessionId.value) return null
  return sessionStore.sessionStatus[sessionId.value]
})
const showRunningStatus = computed(() => {
  if (!currentStatus.value) return false
  return currentStatus.value.runtimeState === 'Working' || currentStatus.value.runtimeState === 'Paused'
})

const sessionId = computed(() => {
  return route.params.sessionId as string
})

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
    await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID)

    startStatusPolling(newSessionId)
  }
})

// 监听消息变化，自动滚动到底部
watch(messages, () => {
  nextTick(() => {
    scrollToBottom()
  })
}, { deep: true })

function scrollToBottom() {
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

function handleKeyDown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    handleSend()
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
  
  try {
    let finalContent = content
    
    // Upload file if exists
    if (pendingFile.value) {
      try {
        const size = formatSize(pendingFile.value.size)
        const res = await chatStore.uploadFile(pendingFile.value)
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

    // 发送消息
    await chatStore.sendMessage(activeSessionId, content, userStore.DEFAULT_USER_ID)

    // 更新会话标题
    if (currentSession.value && messages.value.length <= 2) {
      await sessionStore.updateSession(currentSession.value.sessionId, content.slice(0, 20))
    }

    // 刷新待审批列表
    await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID)
    if (activeSessionId) {
      await sessionStore.loadSessionStatus(activeSessionId)
    }
  } catch (error) {
    console.error('发送消息失败:', error)
  }
}

function toggleTool(part: any) {
  part.isExpanded = !part.isExpanded
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
  let currentToolGroup: any = null
  
  for (const part of parts) {
    if (part.type === 'tool') {
      if (!currentToolGroup) {
        currentToolGroup = { type: 'tool-group', parts: [part] }
        groups.push(currentToolGroup)
      } else {
        currentToolGroup.parts.push(part)
      }
    } else {
      currentToolGroup = null
      groups.push(part)
    }
  }
  return groups
}

function hasRenderableText(message: any) {
  if (!message?.parts) return false
  return message.parts.some((part: any) => part.type === 'text' && part.content && part.content.trim() !== '')
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

/* Tool Call Styles */
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

.tool-item {
  border-bottom: 1px solid #e5e6eb;
}

.tool-item:last-child {
  border-bottom: none;
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
}

.tool-title .el-icon {
  transition: transform 0.2s;
}

.tool-title .el-icon.is-expanded {
  transform: rotate(90deg);
}

.tool-status {
  display: flex;
  align-items: center;
}

.status-success {
  color: #67c23a;
  font-size: 16px;
}

.status-loading {
  display: flex;
  gap: 4px;
}

.status-loading .dot {
  width: 4px;
  height: 4px;
  background-color: #409eff;
  border-radius: 50%;
  animation: tool-loading 1.4s infinite ease-in-out both;
}

.status-loading .dot:nth-child(1) { animation-delay: -0.32s; }
.status-loading .dot:nth-child(2) { animation-delay: -0.16s; }

@keyframes tool-loading {
  0%, 80%, 100% { transform: scale(0); }
  40% { transform: scale(1); }
}

.tool-content {
  padding: 12px;
  border-top: 1px solid #e5e6eb;
  background: #f9fafe;
  font-size: 12px;
  color: #606266;
}

.tool-detail-item {
  display: flex;
  gap: 8px;
}

.tool-detail-item .label {
  color: #909399;
  font-weight: 500;
}

.status-error {
  color: #f56c6c;
  font-size: 16px;
}

.tool-result-output,
.tool-result-error {
  margin-top: 8px;
  background: #fff;
  border: 1px solid #e5e6eb;
  border-radius: 4px;
  padding: 8px;
}

.tool-result-error {
  border-color: #fde2e2;
  background: #fef0f0;
}

.result-label {
  font-weight: 500;
  margin-bottom: 4px;
  color: #606266;
}

.tool-result-error .result-label {
  color: #f56c6c;
}

.tool-result-output pre,
.tool-result-error pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
  font-family: monospace;
  font-size: 12px;
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
