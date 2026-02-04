<template>
  <div class="system-skill-manager">
    <div class="skill-actions">
      <el-button type="primary" @click="showUploadDialog = true">
        <el-icon><Upload /></el-icon>上传技能包
      </el-button>
      <el-button @click="showGitHubDialog = true">
        <el-icon><Download /></el-icon>从GitHub导入
      </el-button>
      <el-button type="success" @click="scanSkills">
        <el-icon><Refresh /></el-icon>扫描技能目录
      </el-button>
    </div>
    
    <div class="current-path">
      <el-icon><Folder /></el-icon>
      <span>技能目录: {{ systemSkillPath || '未配置' }}</span>
    </div>
    
    <div v-if="loading" class="loading-state">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>加载中...</span>
    </div>
    
    <div v-else-if="skills.length === 0" class="empty-state">
      <el-icon :size="48" color="#c0c4cc"><Box /></el-icon>
      <p>暂无系统技能</p>
      <span>点击上方按钮添加技能包</span>
    </div>
    
    <div v-else class="skill-list">
      <div v-for="skill in skills" :key="skill.id" class="skill-card">
        <div class="skill-icon">
          <el-icon :size="32"><Document /></el-icon>
        </div>
        <div class="skill-info">
          <div class="skill-name">{{ skill.displayName || skill.skillId }}</div>
          <div v-if="skill.description" class="skill-desc">{{ skill.description }}</div>
          <div class="skill-meta">
            <el-tag size="small" type="info">{{ skill.skillId }}</el-tag>
            <span v-if="skill.version">版本: {{ skill.version }}</span>
          </div>
        </div>
        <div class="skill-status">
          <el-switch
            v-model="skill.isActive"
            @change="(val: boolean) => toggleSkill(skill, val)"
            :loading="toggling === skill.skillId"
          />
          <el-button type="danger" size="small" text @click="deleteSkill(skill)">
            <el-icon><Delete /></el-icon>
          </el-button>
        </div>
      </div>
    </div>
    
    <!-- 上传对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传系统技能包" width="500px">
      <el-form label-position="top">
        <el-form-item label="技能ID" required>
          <el-input v-model="uploadForm.skillId" placeholder="如: my-system-skill" />
        </el-form-item>
        <el-form-item label="选择文件" required>
          <el-upload
            ref="uploadRef"
            action="#"
            :auto-upload="false"
            :on-change="handleFileChange"
            :limit="1"
            accept=".zip"
          >
            <el-button type="primary">选择ZIP文件</el-button>
            <template #tip>
              <div class="el-upload__tip">请上传包含SKILL.md的ZIP压缩包</div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showUploadDialog = false">取消</el-button>
        <el-button type="primary" @click="uploadSkill" :loading="uploading">上传</el-button>
      </template>
    </el-dialog>
    
    <!-- GitHub导入对话框 -->
    <el-dialog v-model="showGitHubDialog" title="从GitHub批量导入技能" width="600px">
      <el-alert
        type="info"
        :closable="false"
        show-icon
        title="批量导入说明"
        description="此功能适用于GitHub仓库中包含多个技能子目录的情况。系统会遍历指定目录下的所有子目录，自动识别包含SKILL.md的技能包并导入。"
        style="margin-bottom: 16px;"
      />
      <el-form :model="githubForm" label-position="top">
        <el-form-item label="GitHub仓库地址" required>
          <el-input v-model="githubForm.gitUrl" placeholder="https://github.com/username/skills-repo.git" />
        </el-form-item>
        <el-form-item label="分支">
          <el-input v-model="githubForm.branch" placeholder="main" />
        </el-form-item>
        <el-form-item label="技能目录 (可选)">
          <el-input v-model="githubForm.subDir" placeholder="如: skills/ 或留空扫描根目录" />
          <div class="form-hint">如果技能包在仓库的子目录中，请填写相对路径</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showGitHubDialog = false">取消</el-button>
        <el-button type="primary" @click="importFromGitHub" :loading="importing">开始导入</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Upload, Download, Refresh, Loading, Box, Document, Delete, Folder } from '@element-plus/icons-vue'
import type { UploadFile } from 'element-plus'

interface SystemSkill {
  id: string
  skillId: string
  displayName: string
  description: string
  version: string
  isActive: boolean
  path: string
}

const skills = ref<SystemSkill[]>([])
const loading = ref(false)
const toggling = ref<string | null>(null)
const systemSkillPath = ref('')

// 对话框状态
const showUploadDialog = ref(false)
const showGitHubDialog = ref(false)
const uploading = ref(false)
const importing = ref(false)

// 表单数据
const uploadForm = ref({ skillId: '', file: null as File | null })
const githubForm = ref({ gitUrl: '', branch: '', subDir: '' })
const uploadRef = ref<any>(null)

// 加载系统技能列表
async function loadSkills() {
  loading.value = true
  try {
    const response = await fetch('/api/system/skills')
    if (response.ok) {
      const data = await response.json()
      skills.value = data.skills || []
      systemSkillPath.value = data.skillPath || ''
    }
  } catch (error) {
    console.error('Failed to load system skills:', error)
  } finally {
    loading.value = false
  }
}

// 扫描技能目录
async function scanSkills() {
  loading.value = true
  try {
    const response = await fetch('/api/system/skills/scan', { method: 'POST' })
    if (response.ok) {
      const data = await response.json()
      skills.value = data.skills || []
      ElMessage.success(`扫描完成，发现 ${data.skills?.length || 0} 个技能`)
    } else {
      ElMessage.error('扫描失败')
    }
  } catch (error) {
    ElMessage.error('扫描失败')
  } finally {
    loading.value = false
  }
}

// 切换技能激活状态
async function toggleSkill(skill: SystemSkill, isActive: boolean) {
  toggling.value = skill.skillId
  try {
    const response = await fetch(`/api/system/skills/${skill.skillId}/toggle`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ isActive })
    })
    if (response.ok) {
      skill.isActive = isActive
      ElMessage.success(isActive ? '技能已激活' : '技能已停用')
    } else {
      throw new Error('Toggle failed')
    }
  } catch {
    ElMessage.error('操作失败')
    skill.isActive = !isActive
  } finally {
    toggling.value = null
  }
}

// 删除技能
async function deleteSkill(skill: SystemSkill) {
  try {
    await ElMessageBox.confirm(`确定要删除系统技能 "${skill.displayName || skill.skillId}" 吗？`, '确认删除')
    const response = await fetch(`/api/system/skills/${skill.skillId}`, { method: 'DELETE' })
    if (response.ok) {
      skills.value = skills.value.filter(s => s.id !== skill.id)
      ElMessage.success('删除成功')
    } else {
      throw new Error('Delete failed')
    }
  } catch {
    // 用户取消或删除失败
  }
}

// 文件选择处理
function handleFileChange(file: UploadFile) {
  if (file.raw) {
    uploadForm.value.file = file.raw
  }
}

// 上传技能
async function uploadSkill() {
  if (!uploadForm.value.skillId) {
    ElMessage.warning('请输入技能ID')
    return
  }
  if (!uploadForm.value.file) {
    ElMessage.warning('请选择ZIP文件')
    return
  }
  
  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', uploadForm.value.file)
    formData.append('skillId', uploadForm.value.skillId)
    
    const response = await fetch('/api/system/skills/upload', {
      method: 'POST',
      body: formData
    })
    
    if (response.ok) {
      ElMessage.success('上传成功')
      showUploadDialog.value = false
      uploadForm.value = { skillId: '', file: null }
      if (uploadRef.value) {
        uploadRef.value.clearFiles()
      }
      await loadSkills()
    } else {
      const error = await response.text()
      throw new Error(error)
    }
  } catch (error: any) {
    ElMessage.error(error.message || '上传失败')
  } finally {
    uploading.value = false
  }
}

// 从GitHub导入
async function importFromGitHub() {
  if (!githubForm.value.gitUrl) {
    ElMessage.warning('请输入GitHub仓库地址')
    return
  }
  
  importing.value = true
  try {
    const response = await fetch('/api/system/skills/import-github', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(githubForm.value)
    })
    
    if (response.ok) {
      const data = await response.json()
      let message = `成功导入 ${data.skills?.length || 0} 个技能`
      if (data.failed?.length > 0) {
        message += `，${data.failed.length} 个失败`
      }
      ElMessage.success(message)
      showGitHubDialog.value = false
      githubForm.value = { gitUrl: '', branch: '', subDir: '' }
      await loadSkills()
    } else {
      const error = await response.text()
      throw new Error(error)
    }
  } catch (error: any) {
    ElMessage.error(error.message || '导入失败')
  } finally {
    importing.value = false
  }
}

onMounted(loadSkills)
</script>

<style scoped>
.system-skill-manager {
  padding: 16px 0;
}

.skill-actions {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

.current-path {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 6px;
  margin-bottom: 20px;
  color: #606266;
  font-size: 14px;
}

.loading-state,
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px;
  color: #909399;
}

.empty-state p {
  margin: 16px 0 8px;
  font-size: 16px;
}

.empty-state span {
  font-size: 13px;
}

.skill-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
  max-height: calc(100vh - 350px);
  overflow-y: auto;
  padding-right: 8px;
}
.skill-list::-webkit-scrollbar {
  width: 6px;
}
.skill-list::-webkit-scrollbar-thumb {
  background: #c0c4cc;
  border-radius: 3px;
}

.skill-card {
  display: flex;
  gap: 12px;
  padding: 16px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  background: #fff;
  transition: all 0.3s;
}

.skill-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.skill-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  background: #f0f9ff;
  border-radius: 8px;
  color: #409eff;
  flex-shrink: 0;
}

.skill-info {
  flex: 1;
  min-width: 0;
}

.skill-name {
  font-weight: 500;
  font-size: 15px;
  color: #303133;
  margin-bottom: 4px;
}

.skill-desc {
  font-size: 13px;
  color: #606266;
  margin-bottom: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.skill-meta {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 12px;
  color: #909399;
}

.skill-status {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.form-hint {
  color: #909399;
  font-size: 12px;
  margin-top: 4px;
}
</style>
