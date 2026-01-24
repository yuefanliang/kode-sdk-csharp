import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Message } from '@/types'

export const useChatStore = defineStore('chat', () => {
  const messages = ref<Message[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const storagePrefix = 'chat_messages_'

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

  function addToolPart(index: number, toolName: string, callId: string) {
    const message = messages.value[index]
    if (!message || message.role !== 'assistant') return
    
    if (!message.parts) message.parts = []
    
    // Check duplicate
    if (message.parts.some(p => p.type === 'tool' && p.toolId === callId)) return

    message.parts.push({
      type: 'tool',
      toolName,
      toolId: callId,
      status: 'running'
    })
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
  async function uploadFile(file: File) {
    const formData = new FormData()
    formData.append('file', file)
    const res = await fetch('/api/uploads', {
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

  function saveMessages(sessionId: string | null) {
    if (!sessionId || !isStorageAvailable()) return
    try {
      const key = getStorageKey(sessionId)
      window.localStorage.setItem(key, JSON.stringify(messages.value))
    } catch (err) {
      console.warn('Failed to save messages:', err)
    }
  }

  function loadMessages(sessionId: string | null) {
    if (!sessionId || !isStorageAvailable()) {
      messages.value = []
      return
    }
    try {
      const key = getStorageKey(sessionId)
      const raw = window.localStorage.getItem(key)
      if (!raw) {
        messages.value = []
        return
      }
      const parsed = JSON.parse(raw)
      messages.value = Array.isArray(parsed) ? parsed : []
    } catch (err) {
      console.warn('Failed to load messages:', err)
      messages.value = []
    }
  }

  // 发送消息
  async function sendMessage(sessionId: string | null, userMessage: string, threadKey?: string) {
    const baseLength = messages.value.length
    try {
      loading.value = true
      error.value = null

      // 添加用户消息
      addUserMessage(userMessage)
      saveMessages(sessionId)

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
        throw new Error(errorBody || '发送消息失败')
      }

      if (!response.body) {
        throw new Error('响应流不可用')
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder('utf-8')
      let buffer = ''
      let assistantIndex: number | null = null

      const handleEvent = (rawEvent: string) => {
        if (!rawEvent) return
        let eventName = 'message'
        const dataLines: string[] = []
        const lines = rawEvent.split('\n')
        for (const line of lines) {
          if (line.startsWith('event:')) {
            eventName = line.slice(6).trim()
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

        if (eventName === 'tool') {
          try {
            const payload = JSON.parse(data)
            if (payload?.toolName) {
              if (assistantIndex === null) {
                assistantIndex = startAssistantMessage()
              }
              addToolPart(assistantIndex, payload.toolName, payload.callId || '')
              saveMessages(sessionId)
            }
          } catch (err) {
            console.warn('Failed to parse tool event:', err)
          }
          return
        }

        if (eventName === 'tool_result') {
          try {
            const payload = JSON.parse(data)
            if (assistantIndex !== null && payload?.callId) {
              updateToolPart(assistantIndex, payload.callId, payload.isError ? payload.error : payload.result, payload.isError)
              saveMessages(sessionId)
            }
          } catch (err) {
            console.warn('Failed to parse tool_result event:', err)
          }
          return
        }

        try {
          const payload = JSON.parse(data)
          const delta = payload?.choices?.[0]?.delta?.content
          if (delta) {
            if (assistantIndex === null) {
              assistantIndex = startAssistantMessage()
            }
            appendAssistantMessage(assistantIndex, delta)
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
        saveMessages(sessionId)
      }

      const assistantContent = assistantIndex !== null
        ? messages.value[assistantIndex]?.content || ''
        : ''

      return assistantContent
    } catch (err: any) {
      error.value = err.message || '发送消息失败'
      console.error('Failed to send message:', err)

      messages.value = messages.value.slice(0, baseLength)
      saveMessages(sessionId)
      throw err
    } finally {
      loading.value = false
    }
  }

  // 清空消息
  function clearMessages(sessionId?: string | null) {
    messages.value = []
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
    initMessages,
    addUserMessage,
    addAssistantMessage,
    addSystemMessage,
    addToolMessage,
    sendMessage,
    clearMessages,
    loadMessages,
    saveMessages,
    getRecentMessages,
    uploadFile
  }
})
