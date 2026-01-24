import request from './request'
import type { Workspace, WorkspaceCreateRequest, ApiResponse } from '@/types'

export const workspaceApi = {
  // 获取用户的所有工作区
  async list(userId: string): Promise<ApiResponse<Workspace[]>> {
    const response = await request.get('/api/workspaces', {
      params: { userId }
    })
    return { data: response.data }
  },

  // 获取工作区详情
  async get(workspaceId: string): Promise<ApiResponse<Workspace>> {
    const response = await request.get(`/api/workspaces/${workspaceId}`)
    return { data: response.data }
  },

  // 创建新工作区
  async create(userId: string, data: WorkspaceCreateRequest): Promise<ApiResponse<Workspace>> {
    const response = await request.post('/api/workspaces', data, {
      params: { userId }
    })
    return { data: response.data }
  },

  // 更新工作区
  async update(workspaceId: string, data: WorkspaceCreateRequest): Promise<ApiResponse<Workspace>> {
    const response = await request.patch(`/api/workspaces/${workspaceId}`, data)
    return { data: response.data }
  },

  // 删除工作区
  async delete(workspaceId: string): Promise<ApiResponse<void>> {
    const response = await request.delete(`/api/workspaces/${workspaceId}`)
    return { data: response.data }
  },

  // 设置活动工作区
  async activate(userId: string, workspaceId: string): Promise<ApiResponse<void>> {
    const response = await request.post(`/api/workspaces/${workspaceId}/activate`, null, {
      params: { userId }
    })
    return { data: response.data }
  },

  // 获取活动工作区
  async getActive(userId: string): Promise<ApiResponse<Workspace>> {
    const response = await request.get('/api/workspaces/active', {
      params: { userId }
    })
    return { data: response.data }
  }
}
