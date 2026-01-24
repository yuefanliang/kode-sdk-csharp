using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Skills;

/// <summary>
/// Tool for loading skill resource files.
/// </summary>
[Tool("skill_resource")]
public sealed class SkillResourceTool : ToolBase<SkillResourceArgs>
{
    public override string Name => "skill_resource";

    public override string Description =>
        "Load a resource file from an activated skill (references, assets).";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<SkillResourceArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        RequiresApproval = false
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "### skill_resource\n\n" +
            "Load the contents of a resource file from an activated Skill (files under references/ or assets/).\n\n" +
            "Use cases:\n" +
            "- Read reference docs provided by a Skill\n" +
            "- Load a Skill's templates or resource files\n" +
            "- View a Skill's example code\n\n" +
            "Notes:\n" +
            "- Only resources from activated Skills can be loaded\n" +
            "- resourcePath is a path relative to the Skill directory\n" +
            "- Supports files under references/ and assets/");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        SkillResourceArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Check if agent has skills manager
        if (context.Agent is not ISkillsAwareAgent skillsAware)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = "Skills not configured for this agent",
                recommendations = new[] { "Ensure skills are enabled in the agent configuration" }
            });
        }

        var manager = skillsAware.SkillsManager;
        if (manager == null)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = "Skills manager not available"
            });
        }

        var skill = manager.Get(args.SkillName);
        if (skill == null)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = $"Skill not found: {args.SkillName}",
                recommendations = new[]
                {
                    "Use skill_list to view available Skills",
                    "Verify the Skill name spelling"
                }
            });
        }

        if (!manager.IsActivated(args.SkillName))
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = $"Skill \"{args.SkillName}\" is not activated",
                recommendations = new[]
                {
                    $"Use skill_activate to activate \"{args.SkillName}\""
                }
            });
        }

        try
        {
            var content = await manager.LoadResourceAsync(args.SkillName, args.ResourcePath, cancellationToken);

            return ToolResult.Ok(new
            {
                ok = true,
                skillName = args.SkillName,
                resourcePath = args.ResourcePath,
                content
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Ok(new
            {
                ok = false,
                error = ex.Message,
                recommendations = new[]
                {
                    "Check that the resource path is correct",
                    "Confirm the file exists in the Skill directory",
                    $"Available resources: {System.Text.Json.JsonSerializer.Serialize(skill.Resources ?? new())}"
                }
            });
        }
    }
}

/// <summary>
/// Arguments for skill_resource tool.
/// </summary>
[GenerateToolSchema]
public class SkillResourceArgs
{
    /// <summary>
    /// Name of the skill.
    /// </summary>
    [ToolParameter(Description = "Name of the skill")]
    public required string SkillName { get; init; }

    /// <summary>
    /// Path relative to skill directory.
    /// </summary>
    [ToolParameter(Description = "Path relative to skill directory (e.g., \"references/guide.md\")")]
    public required string ResourcePath { get; init; }
}
