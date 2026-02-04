import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Message } from '@/types'

export const useChatStore = defineStore('chat', () => {
  const messages = ref<Message[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const storagePrefix = 'chat_messages_'

  // 分页配置
  const pageSize = 50
  const currentPage = ref(0)

  // 保存防抖
  let saveTimer: ReturnType<typeof setTimeout> | null = null
  const SAVE_DEBOUNCE_MS = 1000 // 1秒防抖

  function getStorageKey(sessionId: string) {
    return `${storagePrefix}${sessionId}`
  }

  function isStorageAvailable() {
    return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined'
  }

  function formatUtc8Timestamp(date: Date = new Date()) {
    const utcMs = date.getTime()
    const utc8 = new Date(utcMs + 8 * 60 * 60 * 1000)
    const yyyy = utc8.getUTCFullYear()
    const mm = String(utc8.getUTCMonth() + 1).padStart(2, '0')
    const dd = String(utc8.getUTCDate()).padStart(2, '0')
    const hh = String(utc8.getUTCHours()).padStart(2, '0')
    const mi = String(utc8.getUTCMinutes()).padStart(2, '0')
    const ss = String(utc8.getUTCSeconds()).padStart(2, '0')
    return `${yyyy}-${mm}-${dd}T${hh}:${mi}:${ss}+08:00`
  }

  // 初始化会话消息
  function initMessages(initialMessages: Message[] = []) {
    messages.value = [...initialMessages]
  }

  // 添加用户消息
  function addUserMessage(content: string) {
    messages.value.push({
      role: 'user',
      content,
      timestamp: formatUtc8Timestamp()
    })
  }

  // 添加助手消息
  function addAssistantMessage(content: string) {
    messages.value.push({
      role: 'assistant',
      content,
      timestamp: formatUtc8Timestamp()
    })
  }

  function startAssistantMessage() {
    const message: Message = {
      role: 'assistant',
      content: '',
      timestamp: formatUtc8Timestamp(),
      parts: [{ type: 'text', content: '' }]
    }
    messages.value.push(message)
    return messages.value.length - 1
  }

  function appendAssistantMessage(index: number, delta: string) {
    const message = messages.value[index]
    if (!message || message.role !== 'assistant') return

    message.content += delta

    if (!message.parts) {
      message.parts = [{ type: 'text', content: message.content }]
    }

    const lastPart = message.parts[message.parts.length - 1]
    if (lastPart.type === 'text') {
      lastPart.content += delta
    } else {
      message.parts.push({ type: 'text', content: delta })
    }
  }

  function addAssistantErrorMessage(content: string) {
    const index = startAssistantMessage()
    appendAssistantMessage(index, content)
    return index
  }

  function addToolPart(index: number, toolName: string, callId: string, approvalId?: string) {
    const message = messages.value[index]
    if (!message || message.role !== 'assistant') return

    if (!message.parts) message.parts = []

    // 检查是否已存在相同toolId的part，如果存在则更新
    const existingPart = message.parts.find(p => p.type === 'tool' && p.toolId === callId)

    if (existingPart) {
      // 更新现有part
      if (approvalId) {
        existingPart.approvalId = approvalId
        existingPart.needsApproval = true
      }
      console.log('Updated existing tool part:', toolName, callId, 'approvalId:', approvalId)
    } else {
      // 添加新part
      message.parts.push({
        type: 'tool',
        toolName,
        toolId: callId,
        status: 'running',
        approvalId: approvalId, // 保存审批ID
        needsApproval: !!approvalId // 标记是否需要审批
      })
      console.log('Added new tool part:', toolName, callId, 'approvalId:', approvalId, 'needsApproval:', !!approvalId)
    }
  }

  function updateToolPart(index: number, callId: string, result: string, isError: boolean) {
    const message = messages.value[index]
    if (!message || !message.parts) return

    const part = message.parts.find(p => p.type === 'tool' && p.toolId === callId)
    if (part) {
      part.status = isError ? 'failed' : 'completed'
      if (isError) {
        part.errorMessage = result
      } else {
        part.output = result
      }
    }
  }

  function completeToolParts(index: number) {
    const message = messages.value[index]
    if (!message || !message.parts) return
    message.parts.forEach(p => {
      if (p.type === 'tool' && p.status !== 'completed') {
        p.status = 'completed'
      }
    })
  }

  // Upload file
  async function uploadFile(file: File, sessionId?: string) {
    const formData = new FormData()
    formData.append('file', file)

    // 构建查询参数，包含sessionId
    const url = sessionId ? `/api/uploads?sessionId=${encodeURIComponent(sessionId)}` : '/api/uploads'

    const res = await fetch(url, {
      method: 'POST',
      body: formData
    })
    if (!res.ok) throw new Error('Upload failed')
    return await res.json()
  }


  // 添加系统消息
  function addSystemMessage(content: string) {
    messages.value.push({
      role: 'system',
      content,
      timestamp: formatUtc8Timestamp()
    })
  }

  function addToolMessage(content: string) {
    messages.value.push({
      role: 'tool',
      content,
      timestamp: formatUtc8Timestamp()
    })
  }

  // 防抖保存消息
  function saveMessages(sessionId: string | null, immediate: boolean = false) {
    if (!sessionId || !isStorageAvailable()) return

    const save = () => {
      try {
        const key = getStorageKey(sessionId)
        window.localStorage.setItem(key, JSON.stringify(messages.value))
      } catch (err) {
        console.warn('Failed to save messages:', err)
      }
    }

    if (immediate) {
      save()
    } else {
      // 清除之前的定时器
      if (saveTimer) {
        clearTimeout(saveTimer)
      }
      // 设置新的定时器
      saveTimer = setTimeout(save, SAVE_DEBOUNCE_MS)
    }
  }

  function loadMessages(sessionId: string | null) {
    if (!sessionId || !isStorageAvailable()) {
      messages.value = []
      currentPage.value = 0
      return
    }
    try {
      const key = getStorageKey(sessionId)
      const raw = window.localStorage.getItem(key)
      if (!raw) {
        messages.value = []
        currentPage.value = 0
        return
      }
      const parsed = JSON.parse(raw)
      const allMessages = Array.isArray(parsed) ? parsed : []

      // 初始只加载第一页
      currentPage.value = 0
      messages.value = allMessages.slice(0, pageSize)

      // 存储完整消息列表用于分页
      ;(messages.value as any).$allMessages = allMessages
    } catch (err) {
      console.warn('Failed to load messages:', err)
      messages.value = []
      currentPage.value = 0
    }
  }

  // 加载更多消息（分页）
  function loadMoreMessages() {
    const allMessages = (messages.value as any).$allMessages
    if (!allMessages || !Array.isArray(allMessages)) return

    const totalMessages = allMessages.length
    const loadedCount = messages.value.length

    if (loadedCount >= totalMessages) {
      return false // 没有更多消息
    }

    // 加载下一页
    currentPage.value++
    const startIndex = currentPage.value * pageSize
    const newMessages = allMessages.slice(startIndex, startIndex + pageSize)

    // 将新消息追加到开头（旧消息）
    messages.value = [...newMessages, ...messages.value]

    return true
  }

  // 检查是否还有更多消息
  function hasMoreMessages() {
    const allMessages = (messages.value as any).$allMessages
    if (!allMessages || !Array.isArray(allMessages)) return false
    return messages.value.length < allMessages.length
  }

  // 发送消息
  async function sendMessage(sessionId: string | null, userMessage: string, threadKey?: string) {
    let assistantIndex: number | null = null
    try {
      loading.value = true
      error.value = null

      // 添加用户消息
      addUserMessage(userMessage)
      // 立即保存用户消息
      saveMessages(sessionId, true)

      const payloadMessages = messages.value.filter(m => m.role !== 'tool')
      const response = await fetch('/v1/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(sessionId ? { 'X-Session-Id': sessionId } : {})
        },
        body: JSON.stringify({
          messages: payloadMessages,
          model: 'gpt-3.5-turbo',
          temperature: 0.7,
          stream: true,
          user: threadKey || undefined
        })
      })

      if (!response.ok) {
        const errorBody = await response.text()
        console.error('Chat API error:', response.status, errorBody)
        throw new Error(errorBody || `发送消息失败 (${response.status})`)
      }

      if (!response.body) {
        throw new Error('响应流不可用')
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder('utf-8')
      let buffer = ''
      const handleEvent = (rawEvent: string) => {
        if (!rawEvent) return
        const dataLines: string[] = []
        const lines = rawEvent.split('\n')
        for (const line of lines) {
          if (line.startsWith('event:')) {
            continue
          } else if (line.startsWith('data:')) {
            dataLines.push(line.slice(5).trim())
          }
        }
        const data = dataLines.join('\n')
        if (!data) return
        if (data === '[DONE]') {
          if (assistantIndex !== null) {
            completeToolParts(assistantIndex)
            saveMessages(sessionId)
          }
          return
        }

        // 尝试解析 payload
        let payload: any = null
        try {
          payload = JSON.parse(data)
        } catch (err) {
          // 如果解析失败，尝试只解析文本内容
          const delta = payload?.choices?.[0]?.delta?.content
          if (delta && !delta.includes('[系统消息]') && !delta.includes('[工具]')) {
            if (assistantIndex === null) {
              assistantIndex = startAssistantMessage()
            }
            appendAssistantMessage(assistantIndex, delta)
            saveMessages(sessionId)
          }
          return
        }

        // 优先从 custom 字段获取事件数据
        const eventData = payload?.custom
        if (eventData) {
          if (eventData.type === 'stream_end') {
            const isError = eventData.is_error || String(eventData.reason || '').toLowerCase() === 'error'
            if (isError) {
              if (assistantIndex === null) {
                assistantIndex = startAssistantMessage()
              }
              const suffix = '\n\n对话结束：发生错误'
              if (!messages.value[assistantIndex]?.content?.includes('对话结束：发生错误')) {
                appendAssistantMessage(assistantIndex, suffix)
              }
              saveMessages(sessionId, true)
            }
            return
          }

          if (eventData.type === 'runtime_error') {
            const message = eventData.error || '对话失败'
            if (assistantIndex === null) {
              assistantIndex = startAssistantMessage()
            }
            appendAssistantMessage(assistantIndex, `对话失败：${message}`)
            saveMessages(sessionId, true)
            return
          }

          // 处理工具开始事件
          if (eventData.type === 'tool_start') {
            try {
              const toolData = eventData
              if (toolData?.tool_name) {
                if (assistantIndex === null) {
                  assistantIndex = startAssistantMessage()
                }
                addToolPart(assistantIndex, toolData.tool_name, toolData.tool_id || '', toolData.approval_id)
                saveMessages(sessionId)
                console.log('Tool start received:', toolData.tool_name, toolData.tool_id, toolData.approval_id ? 'needs approval' : 'no approval')
              }
            } catch (err) {
              console.warn('Failed to parse tool_start event:', err)
            }
            return
          }

          // 处理工具结束事件
          if (eventData.type === 'tool_end') {
            try {
              const toolData = eventData
              if (assistantIndex !== null && toolData?.tool_id) {
                if (toolData.success === false) {
                  updateToolPart(assistantIndex, toolData.tool_id, toolData.error || '执行失败', true)
                } else {
                  updateToolPart(assistantIndex, toolData.tool_id, '执行成功', false)
                }
                saveMessages(sessionId)
                console.log('Tool end received:', toolData.tool_name, 'success:', toolData.success)
              }
            } catch (err) {
              console.warn('Failed to parse tool_end event:', err)
            }
            return
          }

          // 处理工具错误事件
          if (eventData.type === 'tool_error') {
            try {
              const toolData = eventData
              if (assistantIndex !== null && toolData?.tool_id) {
                updateToolPart(assistantIndex, toolData.tool_id, toolData.error || '执行错误', true)
                saveMessages(sessionId)
                console.log('Tool error received:', toolData.tool_name, toolData.error)
              }
            } catch (err) {
              console.warn('Failed to parse tool_error event:', err)
            }
            return
          }

          // 处理审批请求事件
          if (eventData.type === 'approval_required') {
            try {
              const approvalData = eventData
              if (approvalData?.approval_id && approvalData?.tool_name) {
                if (assistantIndex === null) {
                  assistantIndex = startAssistantMessage()
                }
                addToolPart(assistantIndex, approvalData.tool_name, approvalData.tool_id || '', approvalData.approval_id)
                saveMessages(sessionId)
                console.log('Approval required received:', approvalData.tool_name, approvalData.approval_id, approvalData.tool_id)
                // 审批面板会通过定时轮询自动刷新待审批列表
              }
            } catch (err) {
              console.warn('Failed to parse approval_required event:', err)
            }
            return
          }
        }

        // 处理普通文本内容
        try {
          const delta = payload?.choices?.[0]?.delta?.content
          if (delta && !delta.includes('[系统消息]') && !delta.includes('[工具]')) {
            if (assistantIndex === null) {
              assistantIndex = startAssistantMessage()
            }
            appendAssistantMessage(assistantIndex, delta)
            // 不需要每次都保存，依赖防抖
            saveMessages(sessionId)
          }
        } catch (err) {
          console.warn('Failed to parse stream chunk:', err)
        }
      }

      while (true) {
        const { value, done } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        const parts = buffer.split('\n\n')
        buffer = parts.pop() || ''
        for (const part of parts) {
          handleEvent(part.trim())
        }
      }

      if (buffer.trim()) {
        handleEvent(buffer.trim())
      }

      if (assistantIndex !== null) {
        completeToolParts(assistantIndex)
        saveMessages(sessionId, true) // 最后一次保存
      }

      const assistantContent = assistantIndex !== null
        ? messages.value[assistantIndex]?.content || ''
        : ''

      return assistantContent
    } catch (err: any) {
      const message = err?.message || '发送消息失败'
      error.value = message
      console.error('Failed to send message:', err)

      if (assistantIndex === null) {
        assistantIndex = addAssistantErrorMessage(`对话失败：${message}`)
      } else {
        appendAssistantMessage(assistantIndex, `\n\n对话失败：${message}`)
      }
      saveMessages(sessionId, true)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 清空消息
  function clearMessages(sessionId?: string | null) {
    messages.value = []
    currentPage.value = 0
    if (!sessionId || !isStorageAvailable()) return
    try {
      window.localStorage.removeItem(getStorageKey(sessionId))
    } catch (err) {
      console.warn('Failed to clear messages:', err)
    }
  }

  // 获取最近的消息（不包括系统消息）
  function getRecentMessages(count: number = 10) {
    return messages.value
      .filter(m => m.role !== 'system')
      .slice(-count)
  }

  return {
    messages,
    loading,
    error,
    pageSize,
    currentPage,
    initMessages,
    addUserMessage,
    addAssistantMessage,
    addSystemMessage,
    addToolMessage,
    sendMessage,
    clearMessages,
    loadMessages,
    loadMoreMessages,
    hasMoreMessages,
    saveMessages,
    getRecentMessages,
    uploadFile
  }
})
