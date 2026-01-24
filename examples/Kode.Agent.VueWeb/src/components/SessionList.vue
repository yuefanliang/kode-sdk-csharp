<template>
  <div class="session-list">
    <div class="session-header">
      <span>会话列表</span>
      <el-button
        type="primary"
        size="small"
        :icon="Plus"
        @click="handleCreateSession"
      >
        新建
      </el-button>
    </div>

    <div v-if="loading" class="loading-container">
      <el-icon class="is-loading"><Loading /></el-icon>
    </div>

    <div v-else class="session-items">
      <div
        v-for="session in sessions"
        :key="session.sessionId"
        class="session-item"
        :class="{ 'is-active': isActiveSession(session.sessionId) }"
        @click="handleSelectSession(session.sessionId)"
      >
        <div class="session-content">
          <div class="session-title">{{ session.title || '新对话' }}</div>
          <div class="session-time">{{ formatTime(session.updatedAt) }}</div>
        </div>
        <el-dropdown trigger="click" @command="(cmd: string) => handleCommand(cmd, session)">
          <el-icon class="session-menu"><MoreFilled /></el-icon>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="delete">
                <el-icon><Delete /></el-icon>
                删除
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>

      <el-empty
        v-if="sessions.length === 0"
        description="暂无会话"
        :image-size="60"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Loading, MoreFilled, Delete } from '@element-plus/icons-vue'
import { useSessionStore } from '@/stores/session'
import { useRouter } from 'vue-router'
import type { Session } from '@/types'

const sessionStore = useSessionStore()
const router = useRouter()

const loading = ref(false)

const sessions = computed(() => sessionStore.sessions)
const currentSession = computed(() => sessionStore.currentSession)

function isActiveSession(sessionId: string) {
  return currentSession.value?.sessionId === sessionId
}

async function loadSessions() {
  loading.value = true
  try {
    await sessionStore.loadSessions('default-user-001')
  } catch (error) {
    console.error('加载会话失败:', error)
  } finally {
    loading.value = false
  }
}

async function handleCreateSession() {
  try {
    const newSession = await sessionStore.createSession('default-user-001', {
      title: '新对话'
    })
    router.push(`/chat/${newSession.sessionId}`)
  } catch (error) {
    ElMessage.error('创建会话失败')
  }
}

function handleSelectSession(sessionId: string) {
  sessionStore.switchSession(sessionId)
  router.push(`/chat/${sessionId}`)
}

async function handleCommand(command: string, session: Session) {
  if (command === 'delete') {
    try {
      await ElMessageBox.confirm(
        `确定要删除"${session.title || '新对话'}"吗？`,
        '提示',
        {
          confirmButtonText: '确定',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )

      await sessionStore.deleteSession(session.sessionId)
      ElMessage.success('删除成功')

      // 如果删除的是当前会话，创建新会话
      if (currentSession.value?.sessionId === session.sessionId) {
        await handleCreateSession()
      }
    } catch (error) {
      if (error !== 'cancel') {
        ElMessage.error('删除失败')
      }
    }
  }
}

function formatTime(time?: string) {
  if (!time) return ''

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

function parseUtc8Date(time: string) {
  const trimmed = time.trim()
  const hasZone = /z$/i.test(trimmed) || /[+-]\d{2}:?\d{2}$/.test(trimmed)
  const normalized = hasZone
    ? trimmed
    : trimmed.includes(' ') && !trimmed.includes('T')
      ? `${trimmed.replace(' ', 'T')}Z`
      : `${trimmed}Z`
  const date = new Date(normalized)
  if (Number.isNaN(date.getTime())) return null
  return date
}

onMounted(() => {
  loadSessions()
})
</script>

<style scoped>
.session-list {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.session-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px;
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.loading-container {
  display: flex;
  justify-content: center;
  padding: 20px;
}

.session-items {
  flex: 1;
  overflow-y: auto;
  padding: 0 10px 10px;
}

.session-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px;
  margin-bottom: 8px;
  background: #fff;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
  border: 1px solid transparent;
}

.session-item:hover {
  background: #ecf5ff;
  border-color: #d9ecff;
}

.session-item.is-active {
  background: #409eff;
  color: #fff;
}

.session-item.is-active .session-time {
  color: rgba(255, 255, 255, 0.8);
}

.session-content {
  flex: 1;
  overflow: hidden;
}

.session-title {
  font-size: 14px;
  color: #303133;
  margin-bottom: 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.session-item.is-active .session-title {
  color: #fff;
}

.session-time {
  font-size: 12px;
  color: #909399;
}

.session-menu {
  padding: 4px;
  cursor: pointer;
  color: #909399;
  transition: color 0.3s;
}

.session-menu:hover {
  color: #409eff;
}

.session-item.is-active .session-menu {
  color: rgba(255, 255, 255, 0.8);
}
</style>
