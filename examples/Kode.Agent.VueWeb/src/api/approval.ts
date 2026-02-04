import request from './request'
import type { Approval, ApiResponse } from '@/types'

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

  // 批准工具调用
  async approve(approvalId: string, data?: { note?: string }): Promise<ApiResponse<any>> {
    const response = await request.post(`/api/approvals/${approvalId}/approve`, data || {})
    return { data: response.data }
  },

  // 拒绝工具调用
  async reject(approvalId: string, data?: { reason?: string }): Promise<ApiResponse<any>> {
    const response = await request.post(`/api/approvals/${approvalId}/reject`, data || {})
    return { data: response.data }
  }
}
