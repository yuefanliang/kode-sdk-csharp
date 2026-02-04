<template>
  <div class="settings-view">
    <div class="settings-header">
      <h1>系统设置</h1>
      <p>管理应用程序的所有配置选项</p>
    </div>

    <div class="settings-content">
      <el-tabs v-model="activeGroup" tab-position="left" class="settings-tabs">
        <el-tab-pane
          v-for="group in configGroups"
          :key="group.name"
          :label="group.name"
          :name="group.name"
        >
          <div class="group-panel">
            <h2>{{ group.name }}</h2>
            <p class="group-description">{{ getGroupDescription(group.name) }}</p>

            <el-form label-position="top" class="settings-form">
              <el-form-item v-for="config in group.configs" :key="config.configKey">
                <template #label>
                  <div class="config-label">
                    <span>{{ config.displayName }}</span>
                    <el-tooltip v-if="config.description" :content="config.description">
                      <el-icon class="info-icon"><InfoFilled /></el-icon>
                    </el-tooltip>
                  </div>
                </template>

                <el-input
                  v-if="config.valueType === 'password'"
                  v-model="formData[config.configKey]"
                  type="password"
                  show-password
                  :disabled="!config.isEditable"
                />
                <el-input
                  v-else-if="config.valueType === 'textarea'"
                  v-model="formData[config.configKey]"
                  type="textarea"
                  :rows="4"
                  :disabled="!config.isEditable"
                />
                <el-switch
                  v-else-if="config.valueType === 'boolean'"
                  v-model="formData[config.configKey]"
                  :disabled="!config.isEditable"
                  active-value="true"
                  inactive-value="false"
                />
                <el-select
                  v-else-if="config.valueType === 'select'"
                  v-model="formData[config.configKey]"
                  :disabled="!config.isEditable"
                >
                  <el-option v-for="opt in parseOptions(config.options)" :key="opt" :label="opt" :value="opt" />
                </el-select>
                <el-input v-else v-model="formData[config.configKey]" :disabled="!config.isEditable" />
              </el-form-item>

              <el-form-item>
                <el-button type="primary" :loading="saving" @click="saveGroup(group.name)">保存</el-button>
                <el-button @click="resetGroup(group.name)">重置</el-button>
              </el-form-item>
            </el-form>
          </div>
        </el-tab-pane>
        
        <!-- 系统技能管理标签页 -->
        <el-tab-pane label="系统技能" name="SystemSkills">
          <div class="group-panel">
            <h2>系统技能管理</h2>
            <p class="group-description">上传和管理系统级别的技能包，所有会话都可以使用</p>
            
            <SystemSkillManager />
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import { InfoFilled } from '@element-plus/icons-vue'
import { getAllConfigs, updateConfigs, type SystemConfig } from '@/api/systemConfig'
import SystemSkillManager from '@/components/SystemSkillManager.vue'

const activeGroup = ref('')
const configGroups = ref<{ name: string; configs: SystemConfig[] }[]>([])
const formData = reactive<Record<string, string>>({})
const originalData = reactive<Record<string, string>>({})
const saving = ref(false)

const groupDescriptions: Record<string, string> = {
  'AI Provider': '配置默认的AI服务提供商（OpenAI 或 Anthropic）',
  'OpenAI': 'OpenAI API配置',
  'Anthropic': 'Anthropic Claude API配置',
  'System': '系统基础配置',
  'Tool Permissions': '工具使用权限配置',
  'Skills': 'Skill系统配置（系统技能管理请使用右侧"系统技能"标签页）',
  'Sandbox': '沙箱配置',
  'Agent Pool': 'Agent池配置',
  'File Upload': '文件上传配置'
}

function getGroupDescription(name: string) {
  return groupDescriptions[name] || ''
}

function parseOptions(options: string | null) {
  try {
    return options ? JSON.parse(options) : []
  } catch {
    return []
  }
}

async function loadConfigs() {
  const configs = await getAllConfigs()
  const groups: Record<string, SystemConfig[]> = {}
  configs.forEach(c => {
    if (!groups[c.group]) groups[c.group] = []
    groups[c.group].push(c)
  })
  configGroups.value = Object.entries(groups).map(([name, configs]) => ({
    name,
    configs: configs.sort((a, b) => a.sortOrder - b.sortOrder)
  }))
  configs.forEach(c => {
    formData[c.configKey] = c.configValue || ''
    originalData[c.configKey] = c.configValue || ''
  })
  if (configGroups.value.length > 0) activeGroup.value = configGroups.value[0].name
}

async function saveGroup(groupName: string) {
  saving.value = true
  try {
    const group = configGroups.value.find(g => g.name === groupName)
    if (!group) return
    const updates: Record<string, string> = {}
    group.configs.forEach(c => {
      if (formData[c.configKey] !== originalData[c.configKey]) {
        updates[c.configKey] = formData[c.configKey]
      }
    })
    if (Object.keys(updates).length === 0) {
      ElMessage.info('无更改')
      return
    }
    await updateConfigs(updates)
    Object.assign(originalData, updates)
    ElMessage.success('保存成功')
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

function resetGroup(groupName: string) {
  const group = configGroups.value.find(g => g.name === groupName)
  if (!group) return
  group.configs.forEach(c => {
    formData[c.configKey] = originalData[c.configKey]
  })
}

onMounted(loadConfigs)
</script>

<style scoped>
.settings-view { padding: 24px; max-width: 1200px; margin: 0 auto; }
.settings-header { margin-bottom: 24px; }
.settings-header h1 { margin: 0 0 8px; font-size: 24px; }
.settings-header p { color: #909399; margin: 0; }
.settings-content { background: #fff; border-radius: 8px; box-shadow: 0 2px 12px rgba(0,0,0,0.1); }
.settings-tabs { min-height: 600px; }
.group-panel {
  padding: 24px;
  max-height: calc(100vh - 200px);
  overflow-y: auto;
}
.group-panel::-webkit-scrollbar {
  width: 6px;
}
.group-panel::-webkit-scrollbar-thumb {
  background: #c0c4cc;
  border-radius: 3px;
}
.group-panel h2 { margin: 0 0 8px; font-size: 18px; }
.group-description { color: #909399; margin-bottom: 24px; }
.settings-form { max-width: 600px; }
.config-label { display: flex; align-items: center; gap: 8px; }
.info-icon { color: #909399; cursor: help; }
</style>
