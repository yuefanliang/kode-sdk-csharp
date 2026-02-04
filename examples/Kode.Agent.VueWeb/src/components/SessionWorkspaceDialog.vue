<template>
  <el-dialog
    v-model="dialogVisible"
    title="会话工作区配置"
    width="600px"
    @close="handleClose"
  >
    <el-form
      :model="form"
      :rules="rules"
      ref="formRef"
      label-width="100px"
    >
      <el-alert
        title="工作区说明"
        type="info"
        :closable="false"
        style="margin-bottom: 20px"
      >
        为当前会话指定独立的工作目录，工作目录中的文件将被该会话的所有操作使用。
        如果不设置，将使用系统默认目录。
      </el-alert>

      <el-form-item label="工作目录" prop="workDirectory">
        <div class="workdir-input">
          <el-input
            v-model="form.workDirectory"
            placeholder="请输入工作目录路径"
            clearable
          >
            <template #prepend>
              <el-icon><FolderOpened /></el-icon>
            </template>
          </el-input>

          <div class="quick-actions">
            <el-button
              size="small"
              @click="applyDefaultDirectory"
              :disabled="!defaultDirectory"
            >
              使用默认目录
            </el-button>
            <el-button
              size="small"
              @click="useCurrentDirectory"
            >
              使用当前目录
            </el-button>
            <el-button
              size="small"
              @click="clearDirectory"
            >
              清空
            </el-button>
          </div>
        </div>

        <div class="directory-examples">
          <div class="example-title">常用路径示例：</div>
          <div class="example-list">
            <el-tag
              v-for="example in directoryExamples"
              :key="example.path"
              size="small"
              class="example-tag"
              @click="setWorkDirectory(example.path)"
            >
              {{ example.label }}
            </el-tag>
          </div>
        </div>
      </el-form-item>

      <el-form-item label="目录说明">
        <div class="directory-info">
          <div class="info-item">
            <span class="label">默认目录：</span>
            <span class="value">{{ defaultDirectory || '未设置' }}</span>
          </div>
          <div class="info-item">
            <span class="label">当前目录：</span>
            <span class="value">{{ currentWorkDirectory || '使用系统默认' }}</span>
          </div>
        </div>
      </el-form-item>

      <el-form-item>
        <div class="tips">
          <p>提示：</p>
          <ul>
            <li>工作目录应为绝对路径或相对路径</li>
            <li>相对路径相对于应用程序根目录</li>
            <li>确保应用程序有权限访问该目录</li>
            <li>修改工作目录后，已打开的文件可能需要重新加载</li>
          </ul>
        </div>
      </el-form-item>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="loading">
          保存配置
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { FolderOpened } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'

interface Props {
  modelValue: boolean
  sessionId: string
  currentWorkDirectory?: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  'save': [sessionId: string, workDirectory: string]
}>()

const loading = ref(false)
const formRef = ref<FormInstance>()
const defaultDirectory = ref('')

const form = ref({
  workDirectory: ''
})

const directoryExamples = [
  { path: 'D:/liangyuefanF/myAi/kode-sdk-csharp/examples/Kode.Agent.WebApiAssistant/data', label: '默认工作区' },
  { path: './data', label: '相对路径' },
  { path: './workspace', label: 'Workspace目录' },
  { path: '/tmp/workspace', label: '临时目录' },
  { path: 'C:/Users/Public/workspace', label: '公共目录' }
]

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const rules: FormRules = {
  workDirectory: [
    { required: false, message: '请输入工作目录', trigger: 'blur' },
    {
      validator: (_, value, callback) => {
        const trimmed = (value || '').trim()
        if (trimmed.length > 512) {
          callback(new Error('工作目录不能超过512个字符'))
          return
        }
        // 检查非法字符（保留合法的路径字符）
        // Windows路径允许的字符：盘符冒号:、路径分隔符\和/、空格、点等
        // 非法字符：< > " | ? *（文件名不允许的字符）
        const invalidChars = /[<>"|?*]/.test(trimmed)
        if (invalidChars) {
          callback(new Error('工作目录包含非法字符：< > " | ? *'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ]
}

function loadDefaultDirectory() {
  if (typeof window !== 'undefined') {
    try {
      const saved = localStorage.getItem('workspace.defaultWorkDir')
      defaultDirectory.value = saved || ''
    } catch {}
  }
}

function applyDefaultDirectory() {
  if (defaultDirectory.value) {
    form.value.workDirectory = defaultDirectory.value
  }
}

function useCurrentDirectory() {
  // 使用相对路径的当前目录
  form.value.workDirectory = './data'
}

function clearDirectory() {
  form.value.workDirectory = ''
}

function setWorkDirectory(path: string) {
  form.value.workDirectory = path
}

async function handleSubmit() {
  if (!formRef.value) return

  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  try {
    loading.value = true

    // 发送到后端保存
    const response = await fetch(
      `/api/sessionworkspaces/sessions/${props.sessionId}?userId=default-user-001`,
      {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          workDirectory: form.value.workDirectory.trim()
        })
      }
    )

    if (!response.ok) {
      const error = await response.json()
      throw new Error(error.error || '保存失败')
    }

    emit('save', props.sessionId, form.value.workDirectory.trim())
    ElMessage.success('工作区配置已保存')
    dialogVisible.value = false
  } catch (error: any) {
    console.error('Failed to save session workspace:', error)
    ElMessage.error(error.message || '保存失败')
  } finally {
    loading.value = false
  }
}

function handleClose() {
  formRef.value?.resetFields()
  dialogVisible.value = false
}

// 初始化
loadDefaultDirectory()

// 当对话框打开时，加载当前工作目录
watch(() => props.modelValue, (val) => {
  if (val && props.currentWorkDirectory) {
    form.value.workDirectory = props.currentWorkDirectory
  }
})
</script>

<style scoped>
.workdir-input {
  width: 100%;
}

.workdir-input .el-input {
  margin-bottom: 8px;
}

.quick-actions {
  display: flex;
  gap: 8px;
}

.directory-info {
  background: #f5f7fa;
  padding: 12px;
  border-radius: 4px;
}

.info-item {
  display: flex;
  margin-bottom: 8px;
  font-size: 13px;
}

.info-item:last-child {
  margin-bottom: 0;
}

.info-item .label {
  color: #909399;
  min-width: 80px;
  font-weight: 500;
}

.info-item .value {
  color: #303133;
  word-break: break-all;
  flex: 1;
}

.tips {
  background: #fffbe6;
  border-left: 3px solid #e6a23c;
  padding: 12px;
  border-radius: 4px;
  font-size: 13px;
  color: #606266;
  line-height: 1.6;
}

.tips p {
  margin: 0 0 8px 0;
  font-weight: 600;
}

.tips ul {
  margin: 0;
  padding-left: 20px;
}

.tips li {
  margin-bottom: 4px;
}

.directory-examples {
  margin-top: 16px;
  padding: 12px;
  background: #f8f9fa;
  border-radius: 4px;
  border: 1px solid #e9ecef;
}

.example-title {
  font-size: 12px;
  color: #6c757d;
  margin-bottom: 8px;
  font-weight: 500;
}

.example-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.example-tag {
  cursor: pointer;
  transition: all 0.2s;
}

.example-tag:hover {
  transform: translateY(-2px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
