import axios from 'axios'
import { ElMessage } from 'element-plus'
import { formatErrorForDisplay } from '@/utils/error-handler'

const request = axios.create({
  baseURL: '',
  timeout: 60000
})

// 请求拦截器
request.interceptors.request.use(
  (config) => {
    return config
  },
  (error) => {
    console.error('Request error:', error)
    return Promise.reject(error)
  }
)

// 响应拦截器
request.interceptors.response.use(
  (response) => {
    return response
  },
  (error) => {
    // 不显示404错误（由业务逻辑处理）
    if (error.response?.status !== 404) {
      const message = formatErrorForDisplay(error)
      ElMessage.error(message)
    }

    console.error('Response error:', {
      status: error.response?.status,
      statusText: error.response?.statusText,
      data: error.response?.data,
      message: error.message
    })

    return Promise.reject(error)
  }
)

export default request
