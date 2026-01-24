using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 导出管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExportsController : ControllerBase
{
    private readonly IPdfExportService _pdfExportService;
    private readonly ILogger<ExportsController> _logger;

    public ExportsController(
        IPdfExportService pdfExportService,
        ILogger<ExportsController> logger)
    {
        _pdfExportService = pdfExportService;
        _logger = logger;
    }

    /// <summary>
    /// 导出会话为 PDF
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <returns>PDF 文件</returns>
    [HttpGet("sessions/{sessionId}/pdf")]
    public async Task<IActionResult> ExportSessionToPdf(
        string sessionId,
        [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "sessionId is required" });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        try
        {
            var pdfBytes = await _pdfExportService.ExportSessionToPdfAsync(sessionId, userId);
            _logger.LogInformation("Exported session {SessionId} to PDF for user {UserId}", sessionId, userId);

            return File(pdfBytes, "application/pdf", $"session-{sessionId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export session {SessionId}", sessionId);
            return StatusCode(500, new { error = $"Failed to export session: {ex.Message}" });
        }
    }

    /// <summary>
    /// 导出用户记忆为 PDF
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>PDF 文件</returns>
    [HttpGet("users/{userId}/memory/pdf")]
    public async Task<IActionResult> ExportMemoryToPdf(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        try
        {
            var pdfBytes = await _pdfExportService.ExportMemoryToPdfAsync(userId);
            _logger.LogInformation("Exported memory for user {UserId} to PDF", userId);

            return File(pdfBytes, "application/pdf", $"memory-{userId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export memory for user {UserId}", userId);
            return StatusCode(500, new { error = $"Failed to export memory: {ex.Message}" });
        }
    }
}
