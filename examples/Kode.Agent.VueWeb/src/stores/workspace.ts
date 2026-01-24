import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { workspaceApi } from '@/api/workspace'
import type { Workspace, WorkspaceCreateRequest } from '@/types'

export const useWorkspaceStore = defineStore('workspace', () => {
  const workspaces = ref<Workspace[]>([])
  const activeWorkspace = ref<Workspace | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const hasWorkspaces = computed(() => workspaces.value.length > 0)

  // 加载工作区列表
  async function loadWorkspaces(userId?: string) {
    const uid = userId || 'default-user-001'

    try {
      loading.value = true
      error.value = null
      const response = await workspaceApi.list(uid)
      const list = Array.isArray(response.data) ? response.data : []
      workspaces.value = list.filter(item => !!item && !!item.workspaceId)

      // 加载活动工作区
      try {
        const activeResponse = await workspaceApi.getActive(uid)
        activeWorkspace.value = activeResponse.data || null
      } catch {
        // 如果没有活动工作区，设置第一个为活动
        if (workspaces.value.length > 0) {
          await setActiveWorkspace(uid, workspaces.value[0].workspaceId)
        }
      }
    } catch (err: any) {
      error.value = err.message || '加载工作区失败'
      console.error('Failed to load workspaces:', err)
    } finally {
      loading.value = false
    }
  }

  // 创建工作区
  async function createWorkspace(userId: string, data: WorkspaceCreateRequest) {
    try {
      loading.value = true
      error.value = null
      const response = await workspaceApi.create(userId, data)
      const newWorkspace = response.data

      if (!newWorkspace || !newWorkspace.workspaceId) {
        throw new Error('创建工作区失败')
      }

      workspaces.value = [...workspaces.value, newWorkspace]

      await setActiveWorkspace(userId, newWorkspace.workspaceId)

      return newWorkspace
    } catch (err: any) {
      error.value = err.message || '创建工作区失败'
      console.error('Failed to create workspace:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 更新工作区
  async function updateWorkspace(workspaceId: string, data: WorkspaceCreateRequest) {
    try {
      loading.value = true
      error.value = null
      const response = await workspaceApi.update(workspaceId, data)
      const updated = response.data!

      const index = workspaces.value.findIndex(w => w.workspaceId === workspaceId)
      if (index !== -1) {
        workspaces.value[index] = updated
      }

      if (activeWorkspace.value?.workspaceId === workspaceId) {
        activeWorkspace.value = updated
      }

      return updated
    } catch (err: any) {
      error.value = err.message || '更新工作区失败'
      console.error('Failed to update workspace:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 删除工作区
  async function deleteWorkspace(workspaceId: string) {
    try {
      loading.value = true
      error.value = null
      await workspaceApi.delete(workspaceId)

      workspaces.value = workspaces.value.filter(w => w.workspaceId !== workspaceId)

      if (activeWorkspace.value?.workspaceId === workspaceId) {
        activeWorkspace.value = workspaces.value[0] || null
      }
    } catch (err: any) {
      error.value = err.message || '删除工作区失败'
      console.error('Failed to delete workspace:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 设置活动工作区
  async function setActiveWorkspace(userId: string, workspaceId: string) {
    try {
      loading.value = true
      error.value = null
      await workspaceApi.activate(userId, workspaceId)

      const workspace = workspaces.value.find(w => w.workspaceId === workspaceId)
      activeWorkspace.value = workspace || null
    } catch (err: any) {
      error.value = err.message || '设置活动工作区失败'
      console.error('Failed to set active workspace:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 切换工作区
  async function switchWorkspace(userId: string, workspaceId: string) {
    if (activeWorkspace.value?.workspaceId === workspaceId) return
    await setActiveWorkspace(userId, workspaceId)
  }

  return {
    workspaces,
    activeWorkspace,
    loading,
    error,
    hasWorkspaces,
    loadWorkspaces,
    createWorkspace,
    updateWorkspace,
    deleteWorkspace,
    setActiveWorkspace,
    switchWorkspace
  }
})
