using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Skills;

/// <summary>
/// Tool for listing available skills.
/// </summary>
[Tool("skill_list")]
public sealed class SkillListTool : ToolBase<SkillListArgs>
{
    public override string Name => "skill_list";

    public override string Description =>
        "List available skills with their names, descriptions, and activation status.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<SkillListArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        RequiresApproval = false
    };

    private static readonly string[] value = new[] { "Ensure skills are enabled in the agent configuration" };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "### skill_list\n\n" +
            "List currently available Skills. Returns each Skill's name, description, and activation status.\n\n" +
            "Use cases:\n" +
            "- When you need to see which Skills are available\n" +
            "- Before choosing which Skill to activate\n" +
            "- To check a Skill's activation status");
    }

    protected override Task<ToolResult> ExecuteAsync(
        SkillListArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        // Check if agent has skills manager
        if (context.Agent is not ISkillsAwareAgent skillsAware)
        {
            return System.Threading.Tasks.Task.FromResult(ToolResult.Ok(new
            {
                ok = false,
                error = "Skills not configured for this agent",
                recommendations = value
            }));
        }

        var manager = skillsAware.SkillsManager;
        if (manager == null)
        {
            return System.Threading.Tasks.Task.FromResult(ToolResult.Ok(new
            {
                ok = false,
                error = "Skills manager not available",
                recommendations = value
            }));
        }

        var skills = manager.List();
        var skillInfos = skills.Select(s => new
        {
            name = s.Name,
            description = s.Description,
            activated = manager.IsActivated(s.Name),
            hasScripts = (s.Resources?.Scripts?.Count ?? 0) > 0,
            hasReferences = (s.Resources?.References?.Count ?? 0) > 0,
            hasAssets = (s.Resources?.Assets?.Count ?? 0) > 0
        }).ToList();

        return System.Threading.Tasks.Task.FromResult(ToolResult.Ok(new
        {
            ok = true,
            skills = skillInfos,
            count = skills.Count,
            activatedCount = skills.Count(s => manager.IsActivated(s.Name))
        }));
    }
}

/// <summary>
/// Arguments for skill_list tool (no parameters required).
/// </summary>
[GenerateToolSchema]
public class SkillListArgs
{
    // No parameters required
}

// Note: ISkillsAwareAgent is defined in Kode.Agent.Sdk.Core.Abstractions.
