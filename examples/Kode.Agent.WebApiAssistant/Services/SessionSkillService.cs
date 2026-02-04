using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace Kode.Agent.WebApiAssistant.Services;

public record ImportResult
{
    public List<SessionSkillEntity> Skills { get; init; } = new();
    public List<string> Failed { get; init; } = new();
}

/// <summary>
/// 会话级Skill管理服务
/// </summary>
public class SessionSkillService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SessionSkillService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _baseWorkDir;

    public SessionSkillService(
        AppDbContext dbContext,
        ILogger<SessionSkillService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
        _baseWorkDir = GetWorkDir();
    } 
    private string GetWorkDir()
    {
        var workDir = _configuration["Kode:WorkDir"];
        if (string.IsNullOrWhiteSpace(workDir))
        {
            workDir = Environment.GetEnvironmentVariable("KODE_WORK_DIR");
        }
        if (string.IsNullOrWhiteSpace(workDir))
        {
            workDir = Directory.GetCurrentDirectory();
        }

        // 确保路径不为空
        if (string.IsNullOrWhiteSpace(workDir))
        {
            workDir = AppContext.BaseDirectory;
        }

        return Path.GetFullPath(workDir);
    }

    /// <summary>
    /// 获取会话的所有Skill
    /// </summary>
    public async Task<List<SessionSkillEntity>> GetSessionSkillsAsync(string sessionId)
    {
        return await _dbContext.SessionSkills
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.SkillId)
            .ToListAsync();
    }

    /// <summary>
    /// 获取单个Skill
    /// </summary>
    public async Task<SessionSkillEntity?> GetSkillAsync(string sessionId, string skillId)
    {
        return await _dbContext.SessionSkills
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SkillId == skillId);
    }

    /// <summary>
    /// 下载并安装Skill到会话
    /// </summary>
    public async Task<SessionSkillEntity> DownloadSkillAsync(string sessionId, string skillId, string sourceUrl)
    {
        // 检查是否已存在
        var existing = await GetSkillAsync(sessionId, skillId);
        if (existing != null)
        {
            _logger.LogWarning("Skill {SkillId} already exists in session {SessionId}", skillId, sessionId);
            return existing;
        }

        // 创建会话Skill目录
        var sessionSkillDir = GetSessionSkillDir(sessionId);
        EnsureDirectoryExists(sessionSkillDir);

        var skillDir = Path.Combine(sessionSkillDir, skillId);
        EnsureDirectoryExists(skillDir);

        try
        {
            // 下载Skill文件
            var tempFile = Path.Combine(Path.GetTempPath(), $"{skillId}_{Guid.NewGuid()}.zip");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = await client.GetByteArrayAsync(sourceUrl);
                await File.WriteAllBytesAsync(tempFile, bytes);
            }

            // 解压Skill文件
            ZipFile.ExtractToDirectory(tempFile, skillDir, true);

            // 清理临时文件
            File.Delete(tempFile);

            // 读取SKILL.md获取元数据
            var (displayName, description, version) = ReadSkillMetadata(skillDir);

            // 创建数据库记录
            var skill = new SessionSkillEntity
            {
                SessionId = sessionId,
                SkillId = skillId,
                DisplayName = displayName ?? skillId,
                Description = description,
                Source = "downloaded",
                LocalPath = skillDir,
                RemoteUrl = sourceUrl,
                IsActive = false,
                Version = version,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.SessionSkills.Add(skill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Downloaded and installed skill {SkillId} for session {SessionId}",
                skillId, sessionId);

            return skill;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download skill {SkillId} from {SourceUrl}", skillId, sourceUrl);

            // 清理失败的安装
            if (Directory.Exists(skillDir))
            {
                Directory.Delete(skillDir, true);
            }

            throw;
        }
    }

    /// <summary>
    /// 从Git仓库克隆Skill
    /// </summary>
    public async Task<SessionSkillEntity> CloneSkillFromGitAsync(string sessionId, string skillId, string gitUrl, string? branch = null)
    {
        // 检查是否已存在
        var existing = await GetSkillAsync(sessionId, skillId);
        if (existing != null)
        {
            _logger.LogWarning("Skill {SkillId} already exists in session {SessionId}", skillId, sessionId);
            return existing;
        }

        // 创建会话Skill目录
        var sessionSkillDir = GetSessionSkillDir(sessionId);
        EnsureDirectoryExists(sessionSkillDir);

        var skillDir = Path.Combine(sessionSkillDir, skillId);

        try
        {
            // 使用git clone
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {(branch != null ? $"-b {branch}" : "")} {gitUrl} \"{skillDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Git clone failed: {error}");
            }

            // 读取SKILL.md获取元数据
            var (displayName, description, version) = ReadSkillMetadata(skillDir);

            // 创建数据库记录
            var skill = new SessionSkillEntity
            {
                SessionId = sessionId,
                SkillId = skillId,
                DisplayName = displayName ?? skillId,
                Description = description,
                Source = "downloaded",
                LocalPath = skillDir,
                RemoteUrl = gitUrl,
                IsActive = false,
                Version = version,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.SessionSkills.Add(skill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Cloned skill {SkillId} from {GitUrl} for session {SessionId}",
                skillId, gitUrl, sessionId);

            return skill;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clone skill {SkillId} from {GitUrl}", skillId, gitUrl);

            // 清理失败的安装
            if (Directory.Exists(skillDir))
            {
                Directory.Delete(skillDir, true);
            }

            throw;
        }
    }

    /// <summary>
    /// 激活/停用会话Skill
    /// </summary>
    public async Task<bool> ToggleSkillActiveAsync(string sessionId, string skillId, bool isActive)
    {
        var skill = await GetSkillAsync(sessionId, skillId);
        if (skill == null)
        {
            return false;
        }

        skill.IsActive = isActive;
        skill.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("{Action} skill {SkillId} for session {SessionId}",
            isActive ? "Activated" : "Deactivated", skillId, sessionId);

        return true;
    }

    /// <summary>
    /// 删除会话Skill
    /// </summary>
    public async Task<bool> RemoveSkillAsync(string sessionId, string skillId)
    {
        var skill = await GetSkillAsync(sessionId, skillId);
        if (skill == null)
        {
            return false;
        }

        // 删除本地文件
        if (!string.IsNullOrEmpty(skill.LocalPath) && Directory.Exists(skill.LocalPath))
        {
            try
            {
                Directory.Delete(skill.LocalPath, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete skill directory: {Path}", skill.LocalPath);
            }
        }

        _dbContext.SessionSkills.Remove(skill);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Removed skill {SkillId} from session {SessionId}", skillId, sessionId);

        return true;
    }

    /// <summary>
    /// 获取会话激活的Skill路径列表
    /// </summary>
    public async Task<List<string>> GetActiveSkillPathsAsync(string sessionId)
    {
        var skills = await _dbContext.SessionSkills
            .Where(s => s.SessionId == sessionId && s.IsActive)
            .Select(s => s.LocalPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToListAsync();

        return skills!;
    }

    /// <summary>
    /// 更新Skill配置
    /// </summary>
    public async Task<bool> UpdateSkillConfigAsync(string sessionId, string skillId, string configJson)
    {
        var skill = await GetSkillAsync(sessionId, skillId);
        if (skill == null)
        {
            return false;
        }

        skill.ConfigJson = configJson;
        skill.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 上传Skill压缩包
    /// </summary>
    public async Task<SessionSkillEntity> UploadSkillAsync(string sessionId, string skillId, IFormFile file)
    {
        // 检查是否已存在
        var existing = await GetSkillAsync(sessionId, skillId);
        if (existing != null)
        {
            _logger.LogWarning("Skill {SkillId} already exists in session {SessionId}", skillId, sessionId);
            return existing;
        }

        // 创建会话Skill目录
        var sessionSkillDir = GetSessionSkillDir(sessionId);
        EnsureDirectoryExists(sessionSkillDir);

        var skillDir = Path.Combine(sessionSkillDir, skillId);
        EnsureDirectoryExists(skillDir);

        try
        {
            // 保存上传的文件
            var tempFile = Path.Combine(Path.GetTempPath(), $"{skillId}_{Guid.NewGuid()}.zip");
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 解压Skill文件
            ZipFile.ExtractToDirectory(tempFile, skillDir, true);

            // 清理临时文件
            File.Delete(tempFile);

            // 读取SKILL.md获取元数据
            var (displayName, description, version) = ReadSkillMetadata(skillDir);

            // 创建数据库记录
            var skill = new SessionSkillEntity
            {
                SessionId = sessionId,
                SkillId = skillId,
                DisplayName = displayName ?? skillId,
                Description = description,
                Source = "uploaded",
                LocalPath = skillDir,
                RemoteUrl = null,
                IsActive = false,
                Version = version,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.SessionSkills.Add(skill);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Uploaded and installed skill {SkillId} for session {SessionId}",
                skillId, sessionId);

            return skill;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload skill {SkillId} for session {SessionId}", skillId, sessionId);

            // 清理失败的安装
            if (Directory.Exists(skillDir))
            {
                Directory.Delete(skillDir, true);
            }

            throw;
        }
    }

    /// <summary>
    /// 从GitHub批量导入技能
    /// </summary>
    public async Task<ImportResult> ImportSkillsFromGitHubAsync(string sessionId, string gitUrl, string? branch = null, string? subDir = null)
    {
        var result = new ImportResult();
        var tempCloneDir = Path.Combine(Path.GetTempPath(), $"github_import_{Guid.NewGuid()}");

        try
        {
            // 克隆仓库到临时目录
            EnsureDirectoryExists(tempCloneDir);

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone --depth 1 {(branch != null ? $"-b {branch}" : "")} {gitUrl} \"{tempCloneDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Git clone failed: {error}");
            }

            // 确定扫描目录
            var scanDir = tempCloneDir;
            if (!string.IsNullOrEmpty(subDir))
            {
                scanDir = Path.Combine(tempCloneDir, subDir);
                if (!Directory.Exists(scanDir))
                {
                    throw new Exception($"Subdirectory not found: {subDir}");
                }
            }

            // 扫描包含SKILL.md的子目录
            var skillDirs = ScanForSkillDirectories(scanDir);
            _logger.LogInformation("Found {Count} potential skills in {ScanDir}", skillDirs.Count, scanDir);

            // 导入每个技能
            var sessionSkillDir = GetSessionSkillDir(sessionId);
            EnsureDirectoryExists(sessionSkillDir);

            foreach (var skillSourceDir in skillDirs)
            {
                try
                {
                    var skillName = Path.GetFileName(skillSourceDir);
                    
                    // 检查是否已存在
                    var existing = await GetSkillAsync(sessionId, skillName);
                    if (existing != null)
                    {
                        _logger.LogWarning("Skill {SkillName} already exists in session {SessionId}, skipping", skillName, sessionId);
                        result.Failed.Add($"{skillName}: already exists");
                        continue;
                    }

                    var skillDir = Path.Combine(sessionSkillDir, skillName);
                    
                    // 复制技能目录
                    CopyDirectory(skillSourceDir, skillDir);

                    // 读取SKILL.md获取元数据
                    var (displayName, description, version) = ReadSkillMetadata(skillDir);

                    // 创建数据库记录
                    var skill = new SessionSkillEntity
                    {
                        SessionId = sessionId,
                        SkillId = skillName,
                        DisplayName = displayName ?? skillName,
                        Description = description,
                        Source = "github",
                        LocalPath = skillDir,
                        RemoteUrl = gitUrl,
                        IsActive = false,
                        Version = version,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _dbContext.SessionSkills.Add(skill);
                    await _dbContext.SaveChangesAsync();

                    result.Skills.Add(skill);
                    _logger.LogInformation("Imported skill {SkillName} for session {SessionId}", skillName, sessionId);
                }
                catch (Exception ex)
                {
                    var skillName = Path.GetFileName(skillSourceDir);
                    _logger.LogError(ex, "Failed to import skill {SkillName}", skillName);
                    result.Failed.Add($"{skillName}: {ex.Message}");
                }
            }

            _logger.LogInformation("Imported {SuccessCount} skills, {FailedCount} failed for session {SessionId}",
                result.Skills.Count, result.Failed.Count, sessionId);

            return result;
        }
        finally
        {
            // 清理临时目录
            try
            {
                if (Directory.Exists(tempCloneDir))
                {
                    Directory.Delete(tempCloneDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory: {Path}", tempCloneDir);
            }
        }
    }

    /// <summary>
    /// 扫描包含SKILL.md的技能目录
    /// </summary>
    private List<string> ScanForSkillDirectories(string rootDir)
    {
        var skillDirs = new List<string>();

        // 检查根目录本身是否是技能目录
        var rootSkillMd = Path.Combine(rootDir, "SKILL.md");
        if (File.Exists(rootSkillMd))
        {
            skillDirs.Add(rootDir);
            return skillDirs;
        }

        // 扫描一级子目录
        foreach (var dir in Directory.GetDirectories(rootDir))
        {
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (File.Exists(skillMdPath))
            {
                skillDirs.Add(dir);
            }
            else
            {
                // 递归扫描二级子目录（限制深度）
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    var subSkillMdPath = Path.Combine(subDir, "SKILL.md");
                    if (File.Exists(subSkillMdPath))
                    {
                        skillDirs.Add(subDir);
                    }
                }
            }
        }

        return skillDirs;
    }

    /// <summary>
    /// 复制目录
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        EnsureDirectoryExists(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relativePath);
            var destFileDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFileDir))
            {
                EnsureDirectoryExists(destFileDir);
            }
            File.Copy(file, destFile, true);
        }
    }

    /// <summary>
    /// 获取会话Skill目录
    /// </summary>
    private string GetSessionSkillDir(string sessionId)
    {
        var sessionWorkDir = Path.Combine(_baseWorkDir, "session-workspaces", sessionId, ".skills");
        return sessionWorkDir;
    }

    /// <summary>
    /// 确保目录存在，如果不存在则创建
    /// </summary>
    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("Created directory: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create directory: {Path}", path);
                throw new InvalidOperationException($"无法创建目录: {path}", ex);
            }
        }
    }

    /// <summary>
    /// 读取Skill元数据
    /// </summary>
    private (string? displayName, string? description, string? version) ReadSkillMetadata(string skillDir)
    {
        var skillMdPath = Path.Combine(skillDir, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            return (null, null, null);
        }

        try
        {
            var content = File.ReadAllText(skillMdPath);
            var lines = content.Split('\n');

            string? displayName = null;
            string? description = null;
            string? version = null;

            foreach (var line in lines.Take(20)) // 只读取前20行
            {
                if (line.StartsWith("# ") && displayName == null)
                {
                    displayName = line.Substring(2).Trim();
                }
                else if (line.StartsWith("name:") && displayName == null)
                {
                    displayName = line.Substring(5).Trim();
                }
                else if (line.StartsWith("description:") && description == null)
                {
                    description = line.Substring(12).Trim();
                }
                else if (line.StartsWith("version:") && version == null)
                {
                    version = line.Substring(8).Trim();
                }
            }

            // 如果没有找到description，使用第一行非空内容
            if (string.IsNullOrEmpty(description))
            {
                description = lines
                    .SkipWhile(l => string.IsNullOrWhiteSpace(l) || l.StartsWith("#"))
                    .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            }

            return (displayName, description, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read skill metadata from {Path}", skillMdPath);
            return (null, null, null);
        }
    }
}
