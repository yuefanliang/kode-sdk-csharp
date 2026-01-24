import request from './request'
import type { Approval, ApprovalDecisionRequest, ApiResponse } from '@/types'

export const approvalApi = {
  // 获取待审批列表
  async getPending(userId: string): Promise<ApiResponse<Approval[]>> {
    const response = await request.get('/api/approvals/pending', {
      params: { userId }
    })
    return { data: response.data }
  },

  // 获取审批详情
  async get(approvalId: string): Promise<ApiResponse<Approval>> {
    const response = await request.get(`/api/approvals/${approvalId}`)
    return { data: response.data }
  },

  // 确认审批
  async confirm(approvalId: string, userId: string, data?: ApprovalDecisionRequest): Promise<ApiResponse<void>> {
    const response = await request.post(`/api/approvals/${approvalId}/confirm`, data || {}, {
      params: { userId }
    })
    return { data: response.data }
  },

  // 取消审批
  async cancel(approvalId: string, userId: string, data?: ApprovalDecisionRequest): Promise<ApiResponse<void>> {
    const response = await request.post(`/api/approvals/${approvalId}/cancel`, data || {}, {
      params: { userId }
    })
    return { data: response.data }
  }
}
