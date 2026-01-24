using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;

namespace Kode.Agent.Tools.Builtin.Skills;

/// <summary>
/// Tool for activating a skill.
/// </summary>
[Tool("skill_activate")]
public sealed class SkillActivateTool : ToolBase<SkillActivateArgs>
{
    public override string Name => "skill_activate";

    public override string Description =>
        "Activate a skill and load its full instructions into context.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<SkillActivateArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = false
    };

    public override ValueTask<string?> GetPromptAsync(ToolContext context)
    {
        return ValueTask.FromResult<string?>(
            "### skill_activate\n\n" +
            "Activate a Skill and load its full instructions into context.\n\n" +
            "Use cases:\n" +
            "- When the task requires a specific Skill's expertise\n" +
            "- After using skill_list to see available Skills and choosing one to activate\n" +
            "- When you need to follow a specific development guideline or workflow\n\n" +
            "Notes:\n" +
            "- After activation, the Skill's full instructions are injected into context\n" +
            "- If the Skill includes scripts/, you can load them via skill_resource\n" +
            "- Activation is persistent and will be automatically restored on Resume");
    }

    protected override async Task<ToolResult> ExecuteAsync(
        SkillActivateArgs args,
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

        try
        {
            var skill = await skillsAware.ActivateSkillAsync(args.Name, cancellationToken);

            return ToolResult.Ok(new
            {
                ok = true,
                message = $"Skill \"{skill.Name}\" activated",
                description = skill.Description,
                hasScripts = (skill.Resources?.Scripts?.Count ?? 0) > 0,
                hasReferences = (skill.Resources?.References?.Count ?? 0) > 0,
                hasAssets = (skill.Resources?.Assets?.Count ?? 0) > 0,
                resources = skill.Resources
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
                    "Use skill_list to view available Skills",
                    "Verify the Skill name spelling"
                }
            });
        }
    }
}

/// <summary>
/// Arguments for skill_activate tool.
/// </summary>
[GenerateToolSchema]
public class SkillActivateArgs
{
    /// <summary>
    /// Name of the skill to activate.
    /// </summary>
    [ToolParameter(Description = "Name of the skill to activate")]
    public required string Name { get; init; }
}
