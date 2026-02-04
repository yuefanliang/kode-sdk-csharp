import axios from 'axios'

export interface SystemConfig {
  id: number
  configKey: string
  configValue: string
  group: string
  displayName: string
  description: string
  valueType: string
  isEncrypted: boolean
  isEditable: boolean
  options: string | null
  sortOrder: number
}

export interface ConfigGroup {
  name: string
  configs: SystemConfig[]
}

// 获取所有配置分组
export async function getConfigGroups(): Promise<string[]> {
  const response = await axios.get('/api/systemconfig/groups')
  return response.data
}

// 获取所有配置
export async function getAllConfigs(): Promise<SystemConfig[]> {
  const response = await axios.get('/api/systemconfig')
  return response.data
}

// 按分组获取配置
export async function getConfigsByGroup(group: string): Promise<SystemConfig[]> {
  const response = await axios.get(`/api/systemconfig/group/${group}`)
  return response.data
}

// 获取单个配置
export async function getConfig(key: string): Promise<{ key: string; value: string }> {
  const response = await axios.get(`/api/systemconfig/${key}`)
  return response.data
}

// 更新配置
export async function updateConfig(key: string, value: string): Promise<void> {
  await axios.put(`/api/systemconfig/${key}`, { value })
}

// 批量更新配置
export async function updateConfigs(configs: Record<string, string>): Promise<void> {
  await axios.put('/api/systemconfig/batch', configs)
}

// 初始化默认配置
export async function initializeDefaults(): Promise<void> {
  await axios.post('/api/systemconfig/initialize')
}
