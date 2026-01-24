import request from './request'
import type { Session, SessionCreateRequest, SessionUpdateRequest, SessionStatus, ApiResponse } from '@/types'

export const sessionApi = {
  // 获取用户的所有会话
  async list(userId: string): Promise<ApiResponse<Session[]>> {
    const response = await request.get('/api/sessions', {
      params: { userId }
    })
    return { data: response.data }
  },

  // 获取会话详情
  async get(sessionId: string): Promise<ApiResponse<Session>> {
    const response = await request.get(`/api/sessions/${sessionId}`)
    return { data: response.data }
  },

  async status(sessionId: string): Promise<ApiResponse<SessionStatus>> {
    const response = await request.get(`/api/sessions/${sessionId}/status`)
    return { data: response.data }
  },

  // 创建新会话
  async create(userId: string, data?: SessionCreateRequest): Promise<ApiResponse<Session>> {
    const response = await request.post('/api/sessions', data || {}, {
      params: { userId }
    })
    return { data: response.data }
  },

  // 更新会话
  async update(sessionId: string, data: SessionUpdateRequest): Promise<ApiResponse<Session>> {
    const response = await request.patch(`/api/sessions/${sessionId}`, data)
    return { data: response.data }
  },

  // 删除会话
  async delete(sessionId: string): Promise<ApiResponse<void>> {
    const response = await request.delete(`/api/sessions/${sessionId}`)
    return { data: response.data }
  }
}
