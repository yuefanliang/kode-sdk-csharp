using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.WebApiAssistant.Models.Entities;
using QuestPDF.Infrastructure;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// PDF 导出服务
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// 导出会话为 PDF
    /// </summary>
    Task<byte[]> ExportSessionToPdfAsync(string sessionId, string userId);

    /// <summary>
    /// 导出记忆为 PDF
    /// </summary>
    Task<byte[]> ExportMemoryToPdfAsync(string userId);
}

/// <summary>
/// PDF 导出服务实现
/// </summary>
public class PdfExportService : IPdfExportService
{
    private readonly ILogger<PdfExportService> _logger;
    private readonly ISessionService _sessionService;
    private readonly IAgentStore _store;
    private readonly string _workDir;

    public PdfExportService(
        ILogger<PdfExportService> logger,
        ISessionService sessionService,
        IAgentStore store)
    {
        _logger = logger;
        _sessionService = sessionService;
        _store = store;
        _workDir = Directory.GetCurrentDirectory();
    }

    public async Task<byte[]> ExportSessionToPdfAsync(string sessionId, string userId)
    {
        _logger.LogInformation("Exporting session {SessionId} to PDF", sessionId);

        var session = await _sessionService.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var agentId = session.AgentId;
        var messages = await _store.LoadMessagesAsync(agentId);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);

                // 标题
                page.Header().Element(header =>
                {
                    header.Row(row =>
                    {
                        row.RelativeItem().Text("会话导出")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken1);

                        row.RelativeItem().AlignRight()
                            .Text($"导出时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken1);
                    });
                });

                // 内容
                page.Content().Element(content =>
                {
                    content.Column(column =>
                    {
                        // 会话信息
                        column.Item().Element(info =>
                        {
                            info.Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2);
                            info.Row(row =>
                            {
                                row.ConstantItem(150).Text("会话ID:").Bold();
                                row.ConstantItem(150).Text(session.SessionId);
                            });
                            info.Row(row =>
                            {
                                row.ConstantItem(150).Text("标题:").Bold();
                                row.ConstantItem(150).Text(session.Title);
                            });
                            info.Row(row =>
                            {
                                row.ConstantItem(150).Text("用户 ID:").Bold();
                                row.ConstantItem(150).Text(userId);
                            });
                            info.Row(row =>
                            {
                                row.ConstantItem(150).Text("创建时间:").Bold();
                                row.RelativeItem().Text(session.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                            });
                        });

                        column.Item().Height(20);

                        // 消息列表
                        column.Item().Text("对话内容:").Bold().FontSize(14);
                        column.Item().Height(10);

                        foreach (var msg in messages)
                        {
                            var role = msg.Role.ToString().ToLowerInvariant();
                            string text = string.Empty;
                            if (msg.Content != null)
                            {
                                // Concatenate text blocks
                                var parts = new List<string>();
                                foreach (var c in msg.Content)
                                {
                                    if (c is Kode.Agent.Sdk.Core.Types.TextContent tc)
                                    {
                                        parts.Add(tc.Text);
                                    }
                                }
                                text = string.Join("\n", parts);
                            }
                            var color = role == "user"
                                ? Colors.Blue.Lighten4
                                : role == "assistant"
                                    ? Colors.Green.Lighten4
                                    : Colors.Grey.Lighten2;

                            column.Item()
                                .Background(color)
                                .Padding(10)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Element(msg =>
                                {
                                    msg.Row(row =>
                                    {
                                        row.ConstantItem(80).Text($"{role}:").Bold();
                                        row.RelativeItem().Text(text);
                                    });
                                });
                        }
                    });
                });

                // 页脚
                page.Footer().AlignCenter()
                    .Text($"导出时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        });

        var pdfBytes = document.GeneratePdf();
        _logger.LogInformation("Successfully exported session {SessionId} to PDF", sessionId);

        return pdfBytes;
    }

    public async Task<byte[]> ExportMemoryToPdfAsync(string userId)
    {
        _logger.LogInformation("Exporting memory for user {UserId} to PDF", userId);

        var dataDir = Path.Combine(_workDir, ".memory");
        var profilePath = Path.Combine(dataDir, "profile.json");
        var factsDir = Path.Combine(dataDir, "facts");

        // 读取用户配置
        Dictionary<string, object>? profile = null;
        if (File.Exists(profilePath))
        {
            var profileJson = await File.ReadAllTextAsync(profilePath);
            profile = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(profileJson);
        }

        // 读取事实记忆
        var facts = new List<string>();
        if (Directory.Exists(factsDir))
        {
            var factFiles = Directory.GetFiles(factsDir, "*.json");
            foreach (var factFile in factFiles)
            {
                var content = await File.ReadAllTextAsync(factFile);
                facts.Add(content);
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);

                // 标题
                page.Header().Text($"用户记忆导出 - {userId}")
                    .FontSize(20)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                // 内容
                page.Content().Element(content =>
                {
                    content.Column(column =>
                    {
                        // 用户配置
                        column.Item().Element(section =>
                        {
                            section.Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2);
                            section.Text("用户配置:").Bold().FontSize(14);
                            section.Element(_ => _.Height(10));

                            if (profile != null && profile.Count > 0)
                            {
                                foreach (var kvp in profile)
                                {
                                    section.Row(row =>
                                    {
                                        row.ConstantItem(200).Text($"{kvp.Key}:").Bold();
                                        row.RelativeItem().Text(kvp.Value?.ToString() ?? "");
                                    });
                                }
                            }
                            else
                            {
                                section.Text("无配置信息").Italic().FontColor(Colors.Grey.Darken2);
                            }
                        });

                        column.Item().Height(20);

                        // 事实记忆
                        column.Item().Element(section =>
                        {
                            section.Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2);
                            section.Text($"事实记忆 ({facts.Count}条):").Bold().FontSize(14);
                            section.Element(_ => _.Height(10));

                            if (facts.Count > 0)
                            {
                                foreach (var fact in facts)
                                {
                                    section.Element(c =>
                                    {
                                        c.Background(Colors.Grey.Lighten4)
                                         .Padding(10)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten2)
                                         .Text(fact);
                                    });
                                }
                            }
                            else
                            {
                                section.Text("无记忆记录").Italic().FontColor(Colors.Grey.Darken2);
                            }
                        });
                    });
                });

                // 页脚
                page.Footer().AlignCenter()
                    .Text($"导出时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            });
        });

        var pdfBytes = document.GeneratePdf();
        _logger.LogInformation("Successfully exported memory for user {UserId} to PDF", userId);

        return pdfBytes;
    }
}
