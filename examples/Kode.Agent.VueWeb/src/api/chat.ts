import request from './request'
import type { Message, ChatCompletionResponse } from '@/types'

export const chatApi = {
  // 发送对话消息（支持多轮对话）
  async sendMessage(
    sessionId: string | null,
    messages: Message[],
    threadKey?: string
  ): Promise<ChatCompletionResponse> {
    const response = await request.post('/v1/chat/completions', {
      messages: messages,
      model: 'gpt-3.5-turbo',
      temperature: 0.7,
      stream: false,
      user: threadKey || undefined
    }, {
      headers: sessionId ? { 'X-Session-Id': sessionId } : undefined
    })
    return response.data
  }
}
