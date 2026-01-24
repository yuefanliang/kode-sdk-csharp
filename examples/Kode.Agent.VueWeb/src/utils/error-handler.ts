/**
 * 错误处理工具
 */

export interface ErrorResponse {
  error?: string
  message?: string
  [key: string]: any
}

/**
 * 从错误对象中提取错误消息
 */
export function extractErrorMessage(error: any): string {
  if (typeof error === 'string') {
    return error
  }

  if (error?.response?.data) {
    const data = error.response.data
    if (typeof data === 'string') {
      return data
    }
    if (data.error) {
      return data.error
    }
    if (data.message) {
      return data.message
    }
    if (data.title) {
      return data.title
    }
  }

  if (error?.message) {
    return error.message
  }

  return '发生未知错误'
}

/**
 * 判断是否是404错误
 */
export function isNotFoundError(error: any): boolean {
  return error?.response?.status === 404
}

/**
 * 判断是否是网络错误
 */
export function isNetworkError(error: any): boolean {
  return !error?.response && !!error?.message
}

/**
 * 判断是否是服务器错误 (5xx)
 */
export function isServerError(error: any): boolean {
  const status = error?.response?.status
  return status >= 500 && status < 600
}

/**
 * 判断是否是客户端错误 (4xx)
 */
export function isClientError(error: any): boolean {
  const status = error?.response?.status
  return status >= 400 && status < 500
}

/**
 * 格式化错误消息用于显示
 */
export function formatErrorForDisplay(error: any): string {
  if (isNotFoundError(error)) {
    return '请求的资源不存在'
  }

  if (isNetworkError(error)) {
    return '网络连接失败，请检查网络设置'
  }

  if (isServerError(error)) {
    return '服务器错误，请稍后重试'
  }

  return extractErrorMessage(error)
}
