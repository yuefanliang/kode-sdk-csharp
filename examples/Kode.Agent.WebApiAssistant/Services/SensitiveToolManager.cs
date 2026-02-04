using Kode.Agent.WebApiAssistant.Models.Entities;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 敏感工具管理器
/// </summary>
public static class SensitiveToolManager
{
    /// <summary>
    /// 敏感工具列表（删除文件、执行命令等）
    /// </summary>
    private static readonly HashSet<string> SensitiveDeleteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "fs_rm", "fs_delete", "fs_remove", "file_delete", "file_remove",
        "rm", "delete", "remove"
    };

    private static readonly HashSet<string> SensitiveExecuteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash_execute", "bash_exec", "bash",
        "shell_run", "shell_execute", "shell_exec", "shell",
        "execute", "exec", "run_command"
    };

    private static readonly HashSet<string> SensitiveWriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "fs_write", "fs_edit", "fs_create", "file_write", "file_edit", "file_create",
        "write", "edit", "create_file"
    };

    /// <summary>
    /// 判断工具是否为敏感操作
    /// </summary>
    public static (bool IsSensitive, ToolOperationType OperationType) GetToolOperationInfo(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return (false, ToolOperationType.Other);

        var normalizedName = toolName.ToLowerInvariant().Trim();

        // 检查删除操作
        if (SensitiveDeleteTools.Contains(normalizedName))
            return (true, ToolOperationType.Delete);

        // 检查执行操作
        if (SensitiveExecuteTools.Contains(normalizedName))
            return (true, ToolOperationType.Execute);

        // 检查写入操作
        if (SensitiveWriteTools.Contains(normalizedName))
            return (false, ToolOperationType.Write);

        // 检查读取操作
        if (normalizedName.Contains("read") || normalizedName.Contains("fs_") || normalizedName.Contains("file_"))
            return (false, ToolOperationType.Read);

        return (false, ToolOperationType.Other);
    }

    /// <summary>
    /// 获取敏感操作的描述文本
    /// </summary>
    public static string GetSensitiveOperationDescription(ToolOperationType operationType, string toolName)
    {
        return operationType switch
        {
            ToolOperationType.Delete => $"删除文件操作 ({toolName})",
            ToolOperationType.Execute => $"执行命令操作 ({toolName})",
            ToolOperationType.Write => $"写入文件操作 ({toolName})",
            ToolOperationType.Read => $"读取文件操作 ({toolName})",
            _ => $"工具操作 ({toolName})"
        };
    }

    /// <summary>
    /// 获取确认提示文本
    /// </summary>
    public static string GetConfirmationMessage(ToolOperationType operationType, string toolName, object? arguments)
    {
        var operationDesc = GetSensitiveOperationDescription(operationType, toolName);
        var baseMessage = $"检测到敏感操作：{operationDesc}\n\n";

        return operationType switch
        {
            ToolOperationType.Delete => $"{baseMessage}此操作将永久删除文件或目录，删除后无法恢复。\n\n请确认是否继续执行？",
            ToolOperationType.Execute => $"{baseMessage}此操作将执行系统命令，可能对系统造成影响。\n\n请确认是否继续执行？",
            ToolOperationType.Write => $"{baseMessage}此操作将修改或创建文件。\n\n请确认是否继续执行？",
            _ => $"{baseMessage}请确认是否继续执行？"
        };
    }
}
