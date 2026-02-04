using System.IO.Compression;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 系统级Skill管理服务 - 管理全局可用的技能包
/// </summary>
public class SystemSkillService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemSkillService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _workDir;
    
    // 内存中存储系统技能列表
    private static readonly List<SystemSkillInfo> _skills = new();
    private static readonly object _skillsLock = new();

    public SystemSkillService(
        IConfiguration configuration,
        ILogger<SystemSkillService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _workDir = GetWorkDir();
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
        if (string.IsNullOrWhiteSpace(workDir))
        {
            workDir = AppContext.BaseDirectory;
        }
        return Path.GetFullPath(workDir);
    }

    /// <summary>
    /// 获取技能目录路径
    /// </summary>
    public string GetSkillDirectory()
    {
        // 从配置读取技能目录（使用作用域获取Scoped服务）
        string? skillsDir = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<SystemConfigService>();
            skillsDir = configService.GetConfigAsync("Kode:SkillsDir").Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get skills dir from database, using default");
        }
        
        skillsDir ??= _configuration["Kode:SkillsDir"] ?? "skills";
        
        if (Path.IsPathRooted(skillsDir))
        {
            return skillsDir;
        }
        
        return Path.Combine(_workDir, skillsDir);
    }

    /// <summary>
    /// 获取所有系统技能
    /// </summary>
    public List<SystemSkillInfo> GetAllSkills()
    {
        lock (_skillsLock)
        {
            return _skills.ToList();
        }
    }

    /// <summary>
    /// 扫描技能目录
    /// </summary>
    public async Task<List<SystemSkillInfo>> ScanSkillDirectoryAsync()
    {
        var skillDir = GetSkillDirectory();
        var foundSkills = new List<SystemSkillInfo>();

        if (!Directory.Exists(skillDir))
        {
            _logger.LogInformation("Skill directory does not exist: {Path}", skillDir);
            Directory.CreateDirectory(skillDir);
            return foundSkills;
        }

        // 扫描一级子目录
        foreach (var dir in Directory.GetDirectories(skillDir))
        {
            var skillInfo = ScanSkillDirectory(dir);
            if (skillInfo != null)
            {
                foundSkills.Add(skillInfo);
            }
        }

        // 更新内存中的技能列表
        lock (_skillsLock)
        {
            _skills.Clear();
            _skills.AddRange(foundSkills);
        }

        _logger.LogInformation("Scanned skill directory, found {Count} skills", foundSkills.Count);
        return foundSkills;
    }

    /// <summary>
    /// 扫描单个技能目录
    /// </summary>
    private SystemSkillInfo? ScanSkillDirectory(string dir)
    {
        var skillMdPath = Path.Combine(dir, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            return null;
        }

        var skillId = Path.GetFileName(dir);
        var (displayName, description, version) = ReadSkillMetadata(dir);

        // 检查是否已存在
        SystemSkillInfo? existing = null;
        lock (_skillsLock)
        {
            existing = _skills.FirstOrDefault(s => s.SkillId == skillId);
        }

        return new SystemSkillInfo
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
            SkillId = skillId,
            DisplayName = displayName ?? skillId,
            Description = description,
            Version = version,
            IsActive = existing?.IsActive ?? true, // 扫描发现的技能默认激活
            Path = dir,
            Source = existing?.Source ?? "scanned",
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 上传技能压缩包
    /// </summary>
    public async Task<SystemSkillInfo> UploadSkillAsync(string skillId, IFormFile file)
    {
        var skillDir = Path.Combine(GetSkillDirectory(), skillId);
        
        if (Directory.Exists(skillDir))
        {
            throw new InvalidOperationException($"Skill {skillId} already exists");
        }

        Directory.CreateDirectory(skillDir);

        try
        {
            // 保存上传的文件
            var tempFile = Path.Combine(Path.GetTempPath(), $"{skillId}_{Guid.NewGuid()}.zip");
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 解压
            ZipFile.ExtractToDirectory(tempFile, skillDir, true);
            File.Delete(tempFile);

            // 读取元数据
            var (displayName, description, version) = ReadSkillMetadata(skillDir);

            var skill = new SystemSkillInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                SkillId = skillId,
                DisplayName = displayName ?? skillId,
                Description = description,
                Version = version,
                IsActive = true, // 上传后自动激活
                Path = skillDir,
                Source = "uploaded",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            lock (_skillsLock)
            {
                _skills.RemoveAll(s => s.SkillId == skillId);
                _skills.Add(skill);
            }

            _logger.LogInformation("Uploaded skill {SkillId}", skillId);
            return skill;
        }
        catch
        {
            // 清理失败的目录
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
    public async Task<SystemSkillImportResult> ImportSkillsFromGitHubAsync(string gitUrl, string? branch = null, string? subDir = null)
    {
        var result = new SystemSkillImportResult();
        var tempCloneDir = Path.Combine(Path.GetTempPath(), $"github_sys_import_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempCloneDir);

            // 克隆仓库
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

            // 扫描技能目录
            var skillDirs = ScanForSkillDirectories(scanDir);
            var skillRootDir = GetSkillDirectory();
            Directory.CreateDirectory(skillRootDir);

            foreach (var skillSourceDir in skillDirs)
            {
                try
                {
                    var skillName = Path.GetFileName(skillSourceDir);
                    var skillDestDir = Path.Combine(skillRootDir, skillName);

                    // 检查是否已存在
                    if (Directory.Exists(skillDestDir))
                    {
                        result.Failed.Add($"{skillName}: already exists");
                        continue;
                    }

                    // 复制技能目录
                    CopyDirectory(skillSourceDir, skillDestDir);

                    // 读取元数据
                    var (displayName, description, version) = ReadSkillMetadata(skillDestDir);

                    var skill = new SystemSkillInfo
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        SkillId = skillName,
                        DisplayName = displayName ?? skillName,
                        Description = description,
                        Version = version,
                        IsActive = true, // 导入后自动激活
                        Path = skillDestDir,
                        Source = "github",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    lock (_skillsLock)
                    {
                        _skills.RemoveAll(s => s.SkillId == skillName);
                        _skills.Add(skill);
                    }

                    result.Skills.Add(skill);
                    _logger.LogInformation("Imported skill {SkillName} from GitHub", skillName);
                }
                catch (Exception ex)
                {
                    var skillName = Path.GetFileName(skillSourceDir);
                    result.Failed.Add($"{skillName}: {ex.Message}");
                }
            }

            return result;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempCloneDir))
                {
                    Directory.Delete(tempCloneDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory");
            }
        }
    }

    /// <summary>
    /// 切换技能激活状态
    /// </summary>
    public bool ToggleSkillActive(string skillId, bool isActive)
    {
        lock (_skillsLock)
        {
            var skill = _skills.FirstOrDefault(s => s.SkillId == skillId);
            if (skill == null)
            {
                return false;
            }

            skill.IsActive = isActive;
            skill.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("{Action} system skill {SkillId}", 
                isActive ? "Activated" : "Deactivated", skillId);
            return true;
        }
    }

    /// <summary>
    /// 删除技能
    /// </summary>
    public bool DeleteSkill(string skillId)
    {
        lock (_skillsLock)
        {
            var skill = _skills.FirstOrDefault(s => s.SkillId == skillId);
            if (skill == null)
            {
                return false;
            }

            // 删除目录
            if (Directory.Exists(skill.Path))
            {
                try
                {
                    Directory.Delete(skill.Path, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete skill directory: {Path}", skill.Path);
                }
            }

            _skills.Remove(skill);
            _logger.LogInformation("Deleted system skill {SkillId}", skillId);
            return true;
        }
    }

    /// <summary>
    /// 获取激活的技能路径列表
    /// </summary>
    public List<string> GetActiveSkillPaths()
    {
        lock (_skillsLock)
        {
            return _skills
                .Where(s => s.IsActive && Directory.Exists(s.Path))
                .Select(s => s.Path)
                .ToList();
        }
    }

    /// <summary>
    /// 扫描包含SKILL.md的目录
    /// </summary>
    private List<string> ScanForSkillDirectories(string rootDir)
    {
        var skillDirs = new List<string>();

        // 检查根目录
        if (File.Exists(Path.Combine(rootDir, "SKILL.md")))
        {
            skillDirs.Add(rootDir);
            return skillDirs;
        }

        // 扫描一级子目录
        foreach (var dir in Directory.GetDirectories(rootDir))
        {
            if (File.Exists(Path.Combine(dir, "SKILL.md")))
            {
                skillDirs.Add(dir);
            }
        }

        return skillDirs;
    }

    /// <summary>
    /// 复制目录
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relativePath);
            var destFileDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFileDir))
            {
                Directory.CreateDirectory(destFileDir);
            }
            File.Copy(file, destFile, true);
        }
    }

    /// <summary>
    /// 读取技能元数据
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

            foreach (var line in lines.Take(20))
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

/// <summary>
/// 系统技能信息
/// </summary>
public class SystemSkillInfo
{
    public string Id { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public bool IsActive { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 系统技能导入结果
/// </summary>
public class SystemSkillImportResult
{
    public List<SystemSkillInfo> Skills { get; init; } = new();
    public List<string> Failed { get; init; } = new();
}


