using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Kode.Agent.WebApiAssistant.Services;

namespace Kode.Agent.WebApiAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ISessionWorkspaceService _sessionWorkspaceService;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(
        IWebHostEnvironment environment,
        ISessionWorkspaceService sessionWorkspaceService,
        ILogger<UploadsController> logger)
    {
        _environment = environment;
        _sessionWorkspaceService = sessionWorkspaceService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? sessionId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        // 确定目标目录：优先使用会话工作区，否则使用默认uploads目录
        string targetDir;
        bool isInWorkspace = false;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            try
            {
                // 获取会话的工作区配置
                const string defaultUserId = "default-user-001";
                var sessionWorkspace = await _sessionWorkspaceService.GetSessionWorkspaceAsync(sessionId, defaultUserId);
                if (sessionWorkspace != null && !string.IsNullOrWhiteSpace(sessionWorkspace.WorkDirectory))
                {
                    targetDir = sessionWorkspace.WorkDirectory;
                    isInWorkspace = true;
                    _logger.LogInformation("Uploading file to workspace: {WorkDir}", targetDir);
                }
                else
                {
                    // 回退到默认目录
                    targetDir = Path.Combine(_environment.ContentRootPath, "files");
                    _logger.LogInformation("No workspace configured, using default directory: {WorkDir}", targetDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get workspace for session {SessionId}, using default directory", sessionId);
                targetDir = Path.Combine(_environment.ContentRootPath, "files");
            }
        }
        else
        {
            targetDir = Path.Combine(_environment.ContentRootPath, "files");
            _logger.LogInformation("No sessionId provided, using default directory: {WorkDir}", targetDir);
        }

        // 确保目标目录存在
        Directory.CreateDirectory(targetDir);

        // 生成唯一文件名：时间戳 + 随机数 + 原始扩展名
        var ext = Path.GetExtension(file.FileName);
        var random = Path.GetRandomFileName().Replace(".", "").Substring(0, 8);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var newFileName = $"{timestamp}_{random}{ext}";
        var filePath = Path.Combine(targetDir, newFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 根据保存位置返回不同的URL
        string url;
        if (isInWorkspace && Path.IsPathRooted(targetDir))
        {
            // 如果保存到工作区，返回相对路径（AI可以读取）
            var relativePath = newFileName;
            url = relativePath;
        }
        else
        {
            // 如果保存到默认目录，返回完整URL
            url = $"/api/uploads/{newFileName}";
        }

        _logger.LogInformation("File uploaded successfully: {FileName} to {Path}", newFileName, filePath);

        return Ok(new
        {
            fileName = newFileName,
            originalName = file.FileName,
            path = filePath,
            url = url,
            isInWorkspace = isInWorkspace,
            workspace = isInWorkspace ? targetDir : null
        });
    }

    [HttpGet("{fileName}")]
    public IActionResult GetFile(string fileName)
    {
        var uploadsPath = Path.Combine(_environment.ContentRootPath, "files");
        var filePath = Path.Combine(uploadsPath, fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(filePath, contentType, fileName);
    }
}
