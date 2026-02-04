<template>
  <div class="session-skill-manager">
    <div class="skill-header">
      <h3>会话技能</h3>
      <el-dropdown @command="handleAddCommand" trigger="click">
        <el-button type="primary" size="small">
          <el-icon><Plus /></el-icon>添加技能<el-icon class="el-icon--right"><ArrowDown /></el-icon>
        </el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="upload">上传压缩包</el-dropdown-item>
            <el-dropdown-item command="url">ZIP下载</el-dropdown-item>
            <el-dropdown-item command="git">Git仓库</el-dropdown-item>
            <el-dropdown-item command="github-import">GitHub批量导入</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>
    <div v-if="loading" class="skill-loading">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>加载中...</span>
    </div>
    <div v-else-if="skills.length === 0" class="skill-empty">
      <el-icon :size="32" color="#c0c4cc"><Box /></el-icon>
      <p>暂无技能</p>
      <span>点击上方按钮添加技能</span>
    </div>
    <div v-else class="skill-list">
      <div v-for="skill in skills" :key="skill.skillId" class="skill-item" :class="{ 'is-active': skill.isActive }">
        <div class="skill-info">
          <div class="skill-name">
            <el-icon><Document /></el-icon>
            <span>{{ skill.displayName || skill.skillId }}</span>
            <el-tag v-if="skill.isActive" type="success" size="small">已激活</el-tag>
          </div>
          <div v-if="skill.description" class="skill-desc">{{ skill.description }}</div>
          <div class="skill-meta">
            <span>来源: {{ skill.source }}</span>
            <span v-if="skill.version">版本: {{ skill.version }}</span>
          </div>
        </div>
        <div class="skill-actions">
          <el-switch v-model="skill.isActive" @change="(val: boolean) => toggleSkill(skill, val)" :loading="toggling === skill.skillId" />
          <el-button type="danger" size="small" text @click="removeSkill(skill)" :loading="removing === skill.skillId">
            <el-icon><Delete /></el-icon>
          </el-button>
        </div>
      </div>
    </div>
    
    <!-- 上传压缩包对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传技能压缩包" width="500px">
      <el-form label-position="top">
        <el-form-item label="技能ID" required>
          <el-input v-model="uploadForm.skillId" placeholder="如: my-skill" />
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
        <el-button type="primary" @click="uploadSkillFile" :loading="uploading">上传</el-button>
      </template>
    </el-dialog>
    
    <!-- ZIP下载对话框 -->
    <el-dialog v-model="showUrlDialog" title="从URL下载技能" width="500px">
      <el-form :model="addForm" label-position="top">
        <el-form-item label="技能ID"><el-input v-model="addForm.skillId" placeholder="如: code-audit" /></el-form-item>
        <el-form-item label="下载地址"><el-input v-model="addForm.sourceUrl" placeholder="https://example.com/skill.zip" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showUrlDialog = false">取消</el-button>
        <el-button type="primary" @click="addSkillFromUrl" :loading="adding">下载</el-button>
      </template>
    </el-dialog>
    
    <!-- Git克隆对话框 -->
    <el-dialog v-model="showGitDialog" title="从Git仓库克隆" width="500px">
      <el-form :model="addForm" label-position="top">
        <el-form-item label="技能ID"><el-input v-model="addForm.skillId" placeholder="如: code-audit" /></el-form-item>
        <el-form-item label="Git地址"><el-input v-model="addForm.gitUrl" placeholder="https://github.com/user/skill.git" /></el-form-item>
        <el-form-item label="分支 (可选)"><el-input v-model="addForm.branch" placeholder="main" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showGitDialog = false">取消</el-button>
        <el-button type="primary" @click="addSkillFromGit" :loading="adding">克隆</el-button>
      </template>
    </el-dialog>
    
    <!-- GitHub批量导入对话框 -->
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
import { Plus, Loading, Box, Document, Delete, ArrowDown } from '@element-plus/icons-vue'
import type { UploadFile } from 'element-plus'
import { 
  getSessionSkills, 
  downloadSkill, 
  cloneSkill, 
  uploadSkill,
  importSkillsFromGitHub,
  activateSkill, 
  deactivateSkill, 
  removeSkill as apiRemoveSkill, 
  type SessionSkill 
} from '@/api/sessionSkill'

const props = defineProps<{ sessionId: string }>()
const skills = ref<SessionSkill[]>([])
const loading = ref(false)
const toggling = ref<string | null>(null)
const removing = ref<string | null>(null)

// 对话框显示状态
const showUploadDialog = ref(false)
const showUrlDialog = ref(false)
const showGitDialog = ref(false)
const showGitHubDialog = ref(false)

// 表单状态
const adding = ref(false)
const uploading = ref(false)
const importing = ref(false)
const addForm = ref({ skillId: '', sourceUrl: '', gitUrl: '', branch: '' })
const uploadForm = ref({ skillId: '', file: null as File | null })
const githubForm = ref({ gitUrl: '', branch: '', subDir: '' })
const uploadRef = ref<any>(null)

async function loadSkills() {
  loading.value = true
  try { skills.value = await getSessionSkills(props.sessionId) }
  catch { ElMessage.error('加载技能失败') }
  finally { loading.value = false }
}

async function toggleSkill(skill: SessionSkill, isActive: boolean) {
  toggling.value = skill.skillId
  try {
    if (isActive) await activateSkill(props.sessionId, skill.skillId)
    else await deactivateSkill(props.sessionId, skill.skillId)
    skill.isActive = isActive
    ElMessage.success(isActive ? '技能已激活' : '技能已停用')
  } catch {
    ElMessage.error('操作失败')
    skill.isActive = !isActive
  } finally { toggling.value = null }
}

async function removeSkill(skill: SessionSkill) {
  try {
    await ElMessageBox.confirm(`确定要删除技能 "${skill.displayName || skill.skillId}" 吗？`, '确认删除')
    removing.value = skill.skillId
    await apiRemoveSkill(props.sessionId, skill.skillId)
    skills.value = skills.value.filter(s => s.skillId !== skill.skillId)
    ElMessage.success('删除成功')
  } catch { } finally { removing.value = null }
}

// 处理添加菜单命令
function handleAddCommand(command: string) {
  switch (command) {
    case 'upload':
      showUploadDialog.value = true
      break
    case 'url':
      showUrlDialog.value = true
      break
    case 'git':
      showGitDialog.value = true
      break
    case 'github-import':
      showGitHubDialog.value = true
      break
  }
}

// 文件选择处理
function handleFileChange(file: UploadFile) {
  if (file.raw) {
    uploadForm.value.file = file.raw
  }
}

// 上传技能文件
async function uploadSkillFile() {
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
    await uploadSkill(props.sessionId, uploadForm.value.skillId, uploadForm.value.file)
    ElMessage.success('上传成功')
    showUploadDialog.value = false
    uploadForm.value = { skillId: '', file: null }
    if (uploadRef.value) {
      uploadRef.value.clearFiles()
    }
    await loadSkills()
  } catch (error: any) {
    ElMessage.error(error.message || '上传失败')
  } finally {
    uploading.value = false
  }
}

// 从URL下载技能
async function addSkillFromUrl() {
  if (!addForm.value.skillId) { ElMessage.warning('请输入技能ID'); return }
  if (!addForm.value.sourceUrl) { ElMessage.warning('请输入下载地址'); return }
  
  adding.value = true
  try {
    await downloadSkill(props.sessionId, { skillId: addForm.value.skillId, sourceUrl: addForm.value.sourceUrl })
    ElMessage.success('添加成功')
    showUrlDialog.value = false
    addForm.value = { skillId: '', sourceUrl: '', gitUrl: '', branch: '' }
    await loadSkills()
  } catch (error: any) { 
    ElMessage.error(error.message || '添加失败') 
  } finally { 
    adding.value = false 
  }
}

// 从Git克隆技能
async function addSkillFromGit() {
  if (!addForm.value.skillId) { ElMessage.warning('请输入技能ID'); return }
  if (!addForm.value.gitUrl) { ElMessage.warning('请输入Git地址'); return }
  
  adding.value = true
  try {
    await cloneSkill(props.sessionId, { 
      skillId: addForm.value.skillId, 
      gitUrl: addForm.value.gitUrl, 
      branch: addForm.value.branch || undefined 
    })
    ElMessage.success('添加成功')
    showGitDialog.value = false
    addForm.value = { skillId: '', sourceUrl: '', gitUrl: '', branch: '' }
    await loadSkills()
  } catch (error: any) { 
    ElMessage.error(error.message || '添加失败') 
  } finally { 
    adding.value = false 
  }
}

// 从GitHub批量导入
async function importFromGitHub() {
  if (!githubForm.value.gitUrl) {
    ElMessage.warning('请输入GitHub仓库地址')
    return
  }
  
  importing.value = true
  try {
    const result = await importSkillsFromGitHub(
      props.sessionId,
      githubForm.value.gitUrl,
      githubForm.value.branch || undefined,
      githubForm.value.subDir || undefined
    )
    
    let message = `成功导入 ${result.skills.length} 个技能`
    if (result.failed.length > 0) {
      message += `，${result.failed.length} 个失败`
    }
    ElMessage.success(message)
    showGitHubDialog.value = false
    githubForm.value = { gitUrl: '', branch: '', subDir: '' }
    await loadSkills()
  } catch (error: any) {
    ElMessage.error(error.message || '导入失败')
  } finally {
    importing.value = false
  }
}

onMounted(loadSkills)
</script>

<style scoped>
.session-skill-manager { padding: 16px; }
.skill-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.skill-header h3 { margin: 0; font-size: 16px; }
.skill-loading { display: flex; align-items: center; justify-content: center; gap: 8px; padding: 40px; color: #909399; }
.skill-empty { display: flex; flex-direction: column; align-items: center; padding: 40px; color: #909399; text-align: center; }
.skill-empty p { margin: 12px 0 4px; font-size: 14px; }
.skill-empty span { font-size: 12px; }
.skill-list { display: flex; flex-direction: column; gap: 8px; }
.skill-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; border: 1px solid #e4e7ed; border-radius: 6px; background: #fff; }
.skill-item.is-active { border-color: #67c23a; background: #f0f9ff; }
.skill-info { flex: 1; min-width: 0; }
.skill-name { display: flex; align-items: center; gap: 8px; font-weight: 500; }
.skill-desc { color: #606266; font-size: 12px; margin-top: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.skill-meta { color: #909399; font-size: 11px; margin-top: 4px; }
.skill-meta span { margin-right: 12px; }
.skill-actions { display: flex; align-items: center; gap: 8px; }
.form-hint { color: #909399; font-size: 12px; margin-top: 4px; }
</style>
