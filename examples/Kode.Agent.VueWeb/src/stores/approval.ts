import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { approvalApi } from '@/api/approval'
import type { Approval } from '@/types'

export const useApprovalStore = defineStore('approval', () => {
  const pendingApprovals = ref<Approval[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const hasPendingApprovals = computed(() => pendingApprovals.value.length > 0)

  // 加载待审批列表
  async function loadPendingApprovals(userId?: string, sessionId?: string) {
    const uid = userId || 'default-user-001'

    try {
      loading.value = true
      error.value = null
      const response = await approvalApi.getPending(uid)
      const approvals = response.data || []
      pendingApprovals.value = sessionId
        ? approvals.filter(approval => approval.sessionId === sessionId || approval.agentId === sessionId)
        : approvals
    } catch (err: any) {
      error.value = err.message || '加载待审批列表失败'
      console.error('Failed to load pending approvals:', err)
    } finally {
      loading.value = false
    }
  }

  // 批准审批
  async function confirmApproval(approvalId: string, note?: string) {
    try {
      loading.value = true
      error.value = null
      await approvalApi.approve(approvalId, { note })

      // 从列表中移除该审批
      pendingApprovals.value = pendingApprovals.value.filter(
        a => a.approvalId !== approvalId
      )
    } catch (err: any) {
      error.value = err.message || '确认审批失败'
      console.error('Failed to confirm approval:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 拒绝审批
  async function cancelApproval(approvalId: string, note?: string) {
    try {
      loading.value = true
      error.value = null
      await approvalApi.reject(approvalId, { reason: note })

      // 从列表中移除该审批
      pendingApprovals.value = pendingApprovals.value.filter(
        a => a.approvalId !== approvalId
      )
    } catch (err: any) {
      error.value = err.message || '取消审批失败'
      console.error('Failed to cancel approval:', err)
      throw err
    } finally {
      loading.value = false
    }
  }

  return {
    pendingApprovals,
    loading,
    error,
    hasPendingApprovals,
    loadPendingApprovals,
    confirmApproval,
    cancelApproval
  }
})
