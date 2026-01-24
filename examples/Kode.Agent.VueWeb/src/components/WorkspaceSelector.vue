<template>
  <div class="workspace-selector">
    <div class="workspace-info">
      <el-dropdown trigger="click" @command="handleSelectWorkspace">
        <span class="workspace-name">
          {{ activeWorkspaceName }}
          <el-icon><ArrowDown /></el-icon>
        </span>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item
              v-for="workspace in workspaces"
              :key="workspace.workspaceId"
              :command="workspace.workspaceId"
              :class="{ 'is-active': isActiveWorkspace(workspace.workspaceId) }"
            >
              {{ workspace.name }}
            </el-dropdown-item>
            <el-dropdown-item divided command="create">
              <el-icon><Plus /></el-icon>
              创建工作区
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>

      <div class="workspace-path">
        <span class="path-label">目录</span>
        <span class="path-text">{{ activeWorkspacePath || '未设置' }}</span>
        <el-button
          size="small"
          text
          :icon="DocumentCopy"
          :disabled="!activeWorkspacePath"
          @click="handleCopyPath"
        />
      </div>
    </div>

    <el-dialog v-model="dialogVisible" title="创建工作区" width="500px">
      <el-form :model="form" :rules="rules" ref="formRef" label-width="80px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入工作区名称" />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="form.description"
            type="textarea"
            :rows="3"
            placeholder="请输入工作区描述（可选）"
          />
        </el-form-item>
        <el-form-item label="目录选择">
          <el-select
            v-model="selectedWorkDir"
            placeholder="选择已有目录"
            clearable
            filterable
            @change="applySelectedWorkDir"
          >
            <el-option
              v-for="option in workDirOptions"
              :key="option.value"
              :label="option.label"
              :value="option.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="工作目录" prop="workDir">
          <div class="workdir-input">
            <el-input v-model="form.workDir" placeholder="请输入工作目录（可选）" />
            <div class="workdir-actions">
              <el-button size="small" @click="applyDefaultWorkDir" :disabled="!defaultWorkDir">
                使用默认目录
              </el-button>
              <el-button size="small" @click="saveDefaultWorkDir">
                保存为默认
              </el-button>
            </div>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleCreate" :loading="loading">
          创建
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { ArrowDown, Plus, DocumentCopy } from '@element-plus/icons-vue'
import { useWorkspaceStore } from '@/stores/workspace'
import { useUserStore } from '@/stores/user'
import type { FormInstance, FormRules } from 'element-plus'

const workspaceStore = useWorkspaceStore()
const userStore = useUserStore()

const dialogVisible = ref(false)
const loading = ref(false)
const formRef = ref<FormInstance>()
const selectedWorkDir = ref('')
const defaultWorkDir = ref(loadDefaultWorkDir())

const form = ref({
  name: '',
  description: '',
  workDir: ''
})

const rules: FormRules = {
  name: [
    { required: true, message: '请输入工作区名称', trigger: 'blur' },
    {
      validator: (_, value, callback) => {
        const trimmed = (value || '').trim()
        if (!trimmed) {
          callback(new Error('工作区名称不能为空'))
          return
        }
        if (trimmed.length > 64) {
          callback(new Error('工作区名称不能超过64个字符'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ],
  description: [
    {
      validator: (_, value, callback) => {
        const trimmed = (value || '').trim()
        if (trimmed.length > 256) {
          callback(new Error('工作区描述不能超过256个字符'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ],
  workDir: [
    {
      validator: (_, value, callback) => {
        const trimmed = (value || '').trim()
        if (trimmed.length > 512) {
          callback(new Error('工作目录不能超过512个字符'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ]
}

const workspaces = computed(() => workspaceStore.workspaces)
const activeWorkspace = computed(() => workspaceStore.activeWorkspace)

const activeWorkspaceName = computed(() => {
  return activeWorkspace.value?.name || '选择工作区'
})

const activeWorkspacePath = computed(() => {
  return activeWorkspace.value?.workDir || ''
})

const workDirOptions = computed(() => {
  const options: Array<{ label: string; value: string }> = []
  if (defaultWorkDir.value) {
    options.push({ label: `默认目录：${defaultWorkDir.value}`, value: defaultWorkDir.value })
  }
  const uniqueDirs = new Set<string>()
  workspaces.value.forEach(workspace => {
    if (workspace.workDir) {
      uniqueDirs.add(workspace.workDir)
    }
  })
  uniqueDirs.forEach(dir => {
    options.push({ label: `已用目录：${dir}`, value: dir })
  })
  return options
})

function isActiveWorkspace(workspaceId: string) {
  return activeWorkspace.value?.workspaceId === workspaceId
}

async function handleSelectWorkspace(command: string) {
  if (command === 'create') {
    dialogVisible.value = true
    form.value = { name: '', description: '', workDir: defaultWorkDir.value || '' }
    selectedWorkDir.value = ''
    await nextTick()
    formRef.value?.clearValidate()
    return
  }

  try {
    await workspaceStore.switchWorkspace(userStore.DEFAULT_USER_ID, command)
    ElMessage.success('已切换工作区')
  } catch (error) {
    ElMessage.error('切换工作区失败')
  }
}

async function handleCreate() {
  if (!formRef.value) return

  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  try {
    loading.value = true
    const workDir = form.value.workDir.trim() || defaultWorkDir.value || undefined
    const payload = {
      name: form.value.name.trim(),
      description: form.value.description.trim() || undefined,
      workDir: workDir
    }
    await workspaceStore.createWorkspace(userStore.DEFAULT_USER_ID, payload)

    ElMessage.success('工作区创建成功')
    dialogVisible.value = false
  } catch (error) {
    console.error('创建工作区失败:', error)
    ElMessage.error('创建工作区失败')
  } finally {
    loading.value = false
  }
}

function applySelectedWorkDir(value: string) {
  if (!value) return
  form.value.workDir = value
}

function applyDefaultWorkDir() {
  if (!defaultWorkDir.value) return
  form.value.workDir = defaultWorkDir.value
}

function saveDefaultWorkDir() {
  const value = form.value.workDir.trim()
  if (!value) {
    ElMessage.error('请先输入有效目录')
    return
  }
  defaultWorkDir.value = value
  persistDefaultWorkDir(value)
  ElMessage.success('默认目录已保存')
}

async function handleCopyPath() {
  if (!activeWorkspacePath.value) return
  try {
    await navigator.clipboard.writeText(activeWorkspacePath.value)
    ElMessage.success('路径已复制')
  } catch (error) {
    ElMessage.error('复制失败')
  }
}

function loadDefaultWorkDir() {
  if (typeof window === 'undefined') return ''
  try {
    return window.localStorage.getItem('workspace.defaultWorkDir') || ''
  } catch {
    return ''
  }
}

function persistDefaultWorkDir(value: string) {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.setItem('workspace.defaultWorkDir', value)
  } catch {}
}
</script>

<style scoped>
.workspace-selector {
  display: flex;
  align-items: center;
}

.workspace-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.workspace-name {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  background: #f5f7fa;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.3s;
  font-size: 14px;
  color: #606266;
}

.workspace-name:hover {
  background: #e4e7ed;
  color: #409eff;
}

.workspace-name .el-icon {
  font-size: 14px;
}

.workspace-path {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #909399;
}

.path-label {
  color: #c0c4cc;
}

.path-text {
  max-width: 420px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.workdir-input {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.workdir-actions {
  display: flex;
  gap: 8px;
}

:deep(.el-dropdown-menu__item.is-active) {
  color: #409eff;
  background-color: #ecf5ff;
}
</style>
