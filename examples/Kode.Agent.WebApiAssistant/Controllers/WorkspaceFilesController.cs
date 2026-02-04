using Microsoft.AspNetCore.Mvc;
using NPOI.XWPF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Text;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 工作区文件浏览控制器
/// </summary>
[ApiController]
[Route("api/workspace")]
public class WorkspaceFilesController : ControllerBase
{
    private readonly ILogger<WorkspaceFilesController> _logger;

    public WorkspaceFilesController(ILogger<WorkspaceFilesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 判断是否为支持的Office文件格式
    /// </summary>
    private bool IsOfficeFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext == ".docx" || ext == ".xlsx";
    }

    /// <summary>
    /// 列出指定路径下的文件和目录
    /// </summary>
    /// <param name="workspacePath">工作区根目录路径</param>
    /// <param name="path">文件路径（相对于工作区根目录）</param>
    /// <returns>文件列表</returns>
    [HttpGet("list")]
    public IActionResult ListFiles([FromQuery] string workspacePath = "", [FromQuery] string path = "")
    {
        try
        {
            // 使用指定的工作区路径，如果未指定则使用项目根目录
            var basePath = string.IsNullOrWhiteSpace(workspacePath)
                ? Directory.GetCurrentDirectory()
                : workspacePath;

            // 构建完整路径
            var fullPath = string.IsNullOrWhiteSpace(path)
                ? basePath
                : Path.Combine(basePath, path);

            if (!Directory.Exists(fullPath))
            {
                return NotFound(new { error = "Directory not found" });
            }

            var files = new List<object>();

            // 添加目录
            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                files.Add(new
                {
                    name = dirInfo.Name,
                    path = path != "" ? $"{path}/{dirInfo.Name}" : dirInfo.Name,
                    isDirectory = true,
                    modified = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            // 添加文件
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var fileInfo = new FileInfo(file);
                files.Add(new
                {
                    name = fileInfo.Name,
                    path = path != "" ? $"{path}/{fileInfo.Name}" : fileInfo.Name,
                    isDirectory = false,
                    size = fileInfo.Length,
                    modified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return Ok(new
            {
                path = path,
                files = files.OrderBy(f => ((dynamic)f).isDirectory ? 0 : 1)
                             .ThenBy(f => ((dynamic)f).name)
                             .ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files for path: {Path}, workspace: {Workspace}", path, workspacePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 读取文件内容
    /// </summary>
    /// <param name="workspacePath">工作区根目录路径</param>
    /// <param name="path">文件路径（相对于工作区根目录）</param>
    /// <returns>文件内容</returns>
    [HttpGet("read")]
    public IActionResult ReadFile([FromQuery] string workspacePath = "", [FromQuery] string path = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { error = "path is required" });
            }

            // 使用指定的工作区路径，如果未指定则使用项目根目录
            var basePath = string.IsNullOrWhiteSpace(workspacePath)
                ? Directory.GetCurrentDirectory()
                : workspacePath;

            var fullPath = Path.Combine(basePath, path);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new { error = "File not found" });
            }

            // 检查文件大小，避免读取过大的文件
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > 5 * 1024 * 1024) // 5MB
            {
                return BadRequest(new { error = "File too large to preview" });
            }

            // 处理Office文件
            if (IsOfficeFile(fullPath))
            {
                var content = ReadOfficeFile(fullPath);
                return Content(content, "text/plain; charset=utf-8");
            }
            else
            {
                // 普通文本文件
                var content = System.IO.File.ReadAllText(fullPath);
                return Content(content, "text/plain; charset=utf-8");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file: {Path}, workspace: {Workspace}", path, workspacePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 以文件流方式返回工作区文件（用于前端预览，不解析内容）
    /// </summary>
    /// <param name="workspacePath">工作区根目录路径</param>
    /// <param name="path">文件路径（相对于工作区根目录）</param>
    /// <returns>文件流</returns>
    [HttpGet("file")]
    public IActionResult GetFile([FromQuery] string workspacePath = "", [FromQuery] string path = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { error = "path is required" });
            }

            var basePath = string.IsNullOrWhiteSpace(workspacePath)
                ? Directory.GetCurrentDirectory()
                : workspacePath;

            var fullPath = Path.Combine(basePath, path);
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new { error = "File not found" });
            }

            var contentType = GetContentType(fullPath);
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serve file: {Path}, workspace: {Workspace}", path, workspacePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md" or ".log" => "text/plain; charset=utf-8",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// 读取Office文件内容
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文本内容</returns>
    private string ReadOfficeFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        var sb = new StringBuilder();

        try
        {
            // 使用FileShare.Read以避免文件被其他进程占用
            var fileStream = new System.IO.FileStream(
                filePath,
                System.IO.FileMode.Open,
                System.IO.FileAccess.Read,
                System.IO.FileShare.Read);

            if (ext == ".docx")
            {
                // 读取Word文档
                using (fileStream)
                using (var doc = new XWPFDocument(fileStream))
                {
                    // 读取所有段落
                    foreach (var para in doc.Paragraphs)
                    {
                        sb.AppendLine(para.ParagraphText);
                    }

                    // 读取表格
                    foreach (var table in doc.Tables)
                    {
                        sb.AppendLine("\n--- 表格 ---");
                        foreach (var row in table.Rows)
                        {
                            var cells = row.GetTableCells().Select(c => c.GetText()).Join("\t");
                            sb.AppendLine(cells);
                        }
                    }
                }
            }
            else if (ext == ".xlsx")
            {
                // 读取Excel文档
                using (fileStream)
                using (var workbook = new XSSFWorkbook(fileStream))
                {
                    // 读取所有工作表
                    foreach (ISheet sheet in workbook)
                    {
                        sb.AppendLine($"\n--- 工作表: {sheet.SheetName} ---\n");

                        foreach (IRow row in sheet)
                        {
                            var cells = new List<string>();
                            foreach (NPOI.SS.UserModel.ICell cell in row)
                            {
                                var cellValue = cell.CellType switch
                                {
                                    CellType.String => cell.StringCellValue,
                                    CellType.Numeric => cell.NumericCellValue.ToString(),
                                    CellType.Boolean => cell.BooleanCellValue.ToString(),
                                    CellType.Formula => cell.CellFormula,
                                    CellType.Blank => "",
                                    _ => ""
                                };
                                cells.Add(cellValue);
                            }
                            sb.AppendLine(cells.Join("\t"));
                        }
                    }
                }
            }
            else
            {
                fileStream.Dispose();
                sb.AppendLine("不支持的文件格式");
            }
        }
        catch (IOException ioEx)
        {
            // 处理文件占用错误
            _logger.LogError(ioEx, "File is locked or in use: {FilePath}", filePath);
            sb.AppendLine($"文件被占用或正在使用，无法读取: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read office file: {FilePath}", filePath);
            sb.AppendLine($"读取文件失败: {ex.Message}");
        }

        return sb.ToString();
    }
}

internal static class StringExtensions
{
    public static string Join<T>(this IEnumerable<T> source, string separator)
    {
        return string.Join(separator, source);
    }
}
