import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { userApi } from '@/api/user'
import { ElMessage } from 'element-plus'
import { isNotFoundError } from '@/utils/error-handler'
import type { User } from '@/types'

export const useUserStore = defineStore('user', () => {
  const user = ref<User | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  // 默认用户ID（不需要登录）
  const DEFAULT_USER_ID = 'default-user-001'

  const isLoggedIn = computed(() => !!user.value)

  // 初始化默认用户
  async function initDefaultUser() {
    try {
      loading.value = true
      error.value = null

      // 尝试获取用户信息
      const response = await userApi.getProfile(DEFAULT_USER_ID)

      if (response.data) {
        user.value = response.data
      }
    } catch (err: any) {
      // 如果是404，用户不存在，需要创建
      if (isNotFoundError(err)) {
        try {
          ElMessage.info('正在创建默认用户...')
          // 使用新的 API 创建指定 userId 的用户
          const createResponse = await userApi.createUser(DEFAULT_USER_ID, {
            username: 'Default User',
            email: 'default@example.com'
          })
          user.value = createResponse.data!
          ElMessage.success('默认用户创建成功')
        } catch (createError: any) {
          error.value = createError.message || '初始化用户失败'
          console.error('Failed to initialize user:', createError)
          ElMessage.error('创建用户失败，请检查后端服务')
        }
      } else {
        // 其他错误
        error.value = err.message || '获取用户信息失败'
        console.error('Failed to fetch user:', err)
        ElMessage.error('连接服务器失败，请检查后端服务')
      }
    } finally {
      loading.value = false
    }
  }

  // 获取用户信息
  async function fetchProfile() {
    if (!user.value) return

    try {
      loading.value = true
      error.value = null
      const response = await userApi.getProfile(user.value.userId)
      user.value = response.data!
    } catch (err: any) {
      error.value = err.message || '获取用户信息失败'
      console.error('Failed to fetch profile:', err)
    } finally {
      loading.value = false
    }
  }

  // 登出
  async function logout() {
    if (!user.value) return

    try {
      loading.value = true
      error.value = null
      await userApi.logout(user.value.userId)
      user.value = null
    } catch (err: any) {
      error.value = err.message || '登出失败'
      console.error('Failed to logout:', err)
    } finally {
      loading.value = false
    }
  }

  return {
    user,
    loading,
    error,
    isLoggedIn,
    DEFAULT_USER_ID,
    initDefaultUser,
    fetchProfile,
    logout
  }
})

