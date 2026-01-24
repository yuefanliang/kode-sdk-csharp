import request from './request'
import type { User, UserCreateRequest, ApiResponse } from '@/types'

export const userApi = {
  // 注册用户（使用username作为userId）
  async register(data: UserCreateRequest): Promise<ApiResponse<User>> {
    const response = await request.post('/api/users/register', data)
    return { data: response.data }
  },

  // 创建指定userId的用户
  async createUser(userId: string, data?: UserCreateRequest): Promise<ApiResponse<User>> {
    const response = await request.post('/api/users/create', data || {}, {
      params: { userId }
    })
    return { data: response.data }
  },

  // 获取用户信息
  async getProfile(userId: string): Promise<ApiResponse<User>> {
    const response = await request.get('/api/users/profile', {
      params: { userId }
    })
    return { data: response.data }
  },

  // 用户登出
  async logout(userId: string): Promise<ApiResponse<void>> {
    const response = await request.post('/api/users/logout', null, {
      params: { userId }
    })
    return { data: response.data }
  }
}

