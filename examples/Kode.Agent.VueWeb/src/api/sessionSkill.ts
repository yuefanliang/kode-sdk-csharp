import axios from 'axios'

export interface SessionSkill {
  id: number
  skillId: string
  displayName: string
  description: string
  source: string
  isActive: boolean
  version: string
  remoteUrl: string
  createdAt: string
  updatedAt: string
}

export interface DownloadSkillRequest {
  skillId: string
  sourceUrl: string
}

export interface CloneSkillRequest {
  skillId: string
  gitUrl: string
  branch?: string
}

// 获取会话的所有Skill
export async function getSessionSkills(sessionId: string): Promise<SessionSkill[]> {
  const response = await axios.get(`/api/sessions/${sessionId}/skills`)
  return response.data
}

// 获取单个Skill
export async function getSessionSkill(sessionId: string, skillId: string): Promise<SessionSkill> {
  const response = await axios.get(`/api/sessions/${sessionId}/skills/${skillId}`)
  return response.data
}

// 下载Skill
export async function downloadSkill(
  sessionId: string,
  request: DownloadSkillRequest
): Promise<{ message: string; skill: SessionSkill }> {
  const response = await axios.post(`/api/sessions/${sessionId}/skills/download`, request)
  return response.data
}

// 从Git克隆Skill
export async function cloneSkill(
  sessionId: string,
  request: CloneSkillRequest
): Promise<{ message: string; skill: SessionSkill }> {
  const response = await axios.post(`/api/sessions/${sessionId}/skills/clone`, request)
  return response.data
}

// 激活Skill
export async function activateSkill(sessionId: string, skillId: string): Promise<void> {
  await axios.post(`/api/sessions/${sessionId}/skills/${skillId}/activate`)
}

// 停用Skill
export async function deactivateSkill(sessionId: string, skillId: string): Promise<void> {
  await axios.post(`/api/sessions/${sessionId}/skills/${skillId}/deactivate`)
}

// 删除Skill
export async function removeSkill(sessionId: string, skillId: string): Promise<void> {
  await axios.delete(`/api/sessions/${sessionId}/skills/${skillId}`)
}

// 更新Skill配置
export async function updateSkillConfig(sessionId: string, skillId: string, configJson: string): Promise<void> {
  await axios.put(`/api/sessions/${sessionId}/skills/${skillId}/config`, { configJson })
}

// 获取激活的Skill路径
export async function getActiveSkillPaths(sessionId: string): Promise<string[]> {
  const response = await axios.get(`/api/sessions/${sessionId}/skills/active-paths`)
  return response.data
}

// 上传Skill压缩包
export async function uploadSkill(
  sessionId: string,
  skillId: string,
  file: File
): Promise<{ message: string; skill: SessionSkill }> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('skillId', skillId)
  
  const response = await axios.post(`/api/sessions/${sessionId}/skills/upload`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  })
  return response.data
}

// 从GitHub目录批量导入技能
export async function importSkillsFromGitHub(
  sessionId: string,
  gitUrl: string,
  branch?: string,
  subDir?: string
): Promise<{ message: string; skills: SessionSkill[]; failed: string[] }> {
  const response = await axios.post(`/api/sessions/${sessionId}/skills/import-github`, {
    gitUrl,
    branch,
    subDir
  })
  return response.data
}
