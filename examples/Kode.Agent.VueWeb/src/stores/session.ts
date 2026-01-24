import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { sessionApi } from '@/api/session'
import type { Session, SessionCreateRequest, SessionStatus } from '@/types'

export const useSessionStore = defineStore('session', () => {
  const sessions = ref<Session[]>([])
  const currentSession = ref<Session | null>(null)
  const sessionStatus = ref<Record<string, SessionStatus>>({})
  const loading = ref(false)
  const error = ref<string | null>(null)

  const hasSessions = computed(() => sessions.value.length > 0)

  // 加载会话列表
  async function loadSessions(userId?: string) {
    const uid = userId || 'default-user-001'

    try {
      loading.value = true
      error.value = null
      const response = await sessionApi.list(uid)
      const list = Array.isArray(response.data) ? response.data : []
      sessions.value = list.filter((item): item is Session => !!item && !!item.sessionId)
      if (!currentSession.value && sessions.value.length > 0) {
        currentSession.value = sessions.value[0]
      }
    } catch (err: any) {
      error.value = err.message || '加载会话失败'
      console.error('Failed to load sessions:', err)
    } finally {
      loading.value = false
    }
  }

  // 创建新会话
  async function createSession(userId: string, data?: SessionCreateRequest) {
    try {
      loading.value = true
      error.value = null
      const response = await sessionApi.create(userId, data)
      const newSession = response.data

      if (!newSession || !newSession.sessionId) {
        throw new Error('创建会话失败')
      }

      sessions.value = [newSession, ...sessions.value.filter(s => s.sessionId !== newSession.sessionId)]
      currentSession.value = newSession

      return newSession
    } catch (err: any) {
      error.value = err.message || '创建会话失败'
      console.error('Failed to create session:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 更新会话
  async function updateSession(sessionId: string, title: string) {
    try {
      loading.value = true
      error.value = null
      const response = await sessionApi.update(sessionId, { title })
      const updated = response.data

      if (!updated || !updated.sessionId) {
        throw new Error('更新会话失败')
      }

      const index = sessions.value.findIndex(s => s.sessionId === sessionId)
      if (index !== -1) {
        sessions.value[index] = updated
      }

      if (currentSession.value?.sessionId === sessionId) {
        currentSession.value = updated
      }

      return updated
    } catch (err: any) {
      error.value = err.message || '更新会话失败'
      console.error('Failed to update session:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 删除会话
  async function deleteSession(sessionId: string) {
    try {
      loading.value = true
      error.value = null
      await sessionApi.delete(sessionId)

      sessions.value = sessions.value.filter(s => s.sessionId !== sessionId)

      if (currentSession.value?.sessionId === sessionId) {
        currentSession.value = sessions.value[0] || null
      }

      delete sessionStatus.value[sessionId]
    } catch (err: any) {
      error.value = err.message || '删除会话失败'
      console.error('Failed to delete session:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 切换会话
  function switchSession(sessionId: string) {
    const session = sessions.value.find(s => s.sessionId === sessionId)
    if (session) {
      currentSession.value = session
    }
  }

  // 设置当前会话
  function setCurrentSession(session: Session | null) {
    currentSession.value = session
  }

  async function loadSessionStatus(sessionId: string) {
    try {
      const response = await sessionApi.status(sessionId)
      if (response.data) {
        sessionStatus.value[sessionId] = response.data
      }
    } catch (err: any) {
      console.error('Failed to load session status:', err)
    }
  }

  return {
    sessions,
    currentSession,
    sessionStatus,
    loading,
    error,
    hasSessions,
    loadSessions,
    createSession,
    updateSession,
    deleteSession,
    switchSession,
    setCurrentSession,
    loadSessionStatus
  }
})
