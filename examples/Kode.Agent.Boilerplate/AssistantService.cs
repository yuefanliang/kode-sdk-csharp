using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Kode.Agent.Boilerplate.Models;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Types;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.Boilerplate;

/// <summary>
/// Core assistant service handling OpenAI-compatible chat completions
/// </summary>
public sealed class AssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AgentDependencies _globalDeps;
    private readonly BoilerplateOptions _options;
    private readonly ILogger<AssistantService> _logger;
    private readonly ActivitySource _activitySource;

    public AssistantService(
        AgentDependencies globalDeps,
        BoilerplateOptions options,
        ILogger<AssistantService> logger,
        ActivitySource activitySource)
    {
        _globalDeps = globalDeps;
        _options = options;
        _logger = logger;
        _activitySource = activitySource;
    }

    public async Task HandleChatCompletionsAsync(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request)
    {
        using var activity = _activitySource.StartActivity("HandleChatCompletion");
        activity?.SetTag("stream", request.Stream);

        try
        {
            // Extract system prompt and input
            var (systemPrompt, input) = ExtractPromptAndInput(request);
            activity?.SetTag("input_length", input.Length);

            // Get or create agent
            var (agent, sessionId) = await GetOrCreateAgentAsync(
                httpContext,
                systemPrompt,
                request.Temperature,
                request.MaxTokens,
                httpContext.RequestAborted);

            activity?.SetTag("session_id", sessionId);

            // Set response header
            httpContext.Response.Headers["X-Session-Id"] = sessionId;

            // Execute agent
            await using (agent)
            {
                if (request.Stream)
                {
                    await StreamResponseAsync(httpContext, agent, input, sessionId);
                }
                else
                {
                    await NonStreamResponseAsync(httpContext, agent, input);
                }
            }
        }
        catch (BadHttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            httpContext.Response.StatusCode = ex.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = ex.Message,
                    type = "invalid_request_error"
                }
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error handling chat completion");
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = "Internal server error",
                    type = "server_error"
                }
            }, JsonOptions);
        }
    }

    /// <summary>
    /// Get or create agent based on session ID
    /// </summary>
    private async Task<(AgentImpl Agent, string SessionId)> GetOrCreateAgentAsync(
        HttpContext httpContext,
        string? systemPrompt,
        double? temperature,
        int? maxTokens,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("GetOrCreateAgent");

        // Get session ID from request (URL path or headers)
        var sessionId = GetSessionIdFromRequest(httpContext);
        activity?.SetTag("session_id_provided", sessionId != null);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            // Verify session exists
            if (!await _globalDeps.Store.ExistsAsync(sessionId, cancellationToken))
            {
                throw new BadHttpRequestException(
                    $"Session '{sessionId}' not found",
                    StatusCodes.Status404NotFound);
            }

            _logger.LogInformation("Resuming agent session: {SessionId}", sessionId);
            var agent = await CreateOrResumeAgentAsync(
                sessionId,
                systemPrompt,
                temperature,
                maxTokens,
                cancellationToken);

            return (agent, sessionId);
        }

        // Create new session
        var newSessionId = GenerateSessionId();
        _logger.LogInformation("Creating new agent session: {SessionId}", newSessionId);
        var newAgent = await CreateOrResumeAgentAsync(
            newSessionId,
            systemPrompt,
            temperature,
            maxTokens,
            cancellationToken);

        return (newAgent, newSessionId);
    }

    /// <summary>
    /// Get session ID from HTTP request
    /// Priority: URL path > X-Session-Id > X-Kode-Agent-Id
    /// </summary>
    private static string? GetSessionIdFromRequest(HttpContext httpContext)
    {
        // Try URL path parameter
        if (httpContext.Request.RouteValues.TryGetValue("sessionId", out var pathValue))
        {
            var sessionId = pathValue?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(sessionId))
            {
                return sessionId;
            }
        }

        // Try X-Session-Id header
        if (httpContext.Request.Headers.TryGetValue("X-Session-Id", out var sessionHeader))
        {
            var sessionId = sessionHeader.ToString()?.Trim();
            if (!string.IsNullOrEmpty(sessionId))
            {
                return sessionId;
            }
        }

        // Try X-Kode-Agent-Id header (backward compatibility)
        if (httpContext.Request.Headers.TryGetValue("X-Kode-Agent-Id", out var agentHeader))
        {
            var sessionId = agentHeader.ToString()?.Trim();
            if (!string.IsNullOrEmpty(sessionId))
            {
                return sessionId;
            }
        }

        return null;
    }

    /// <summary>
    /// Generate unique session ID
    /// </summary>
    private static string GenerateSessionId()
    {
        var chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Timestamp part (10 chars)
        var timePart = new char[10];
        var num = now;
        for (var i = 9; i >= 0; i--)
        {
            timePart[i] = chars[(int)(num % chars.Length)];
            num /= chars.Length;
        }

        // Random part (16 chars)
        var random = new char[16];
        var rand = new Random();
        for (var i = 0; i < 16; i++)
        {
            random[i] = chars[rand.Next(chars.Length)];
        }

        return $"agt_{new string(timePart)}{new string(random)}";
    }

    /// <summary>
    /// Create or resume agent
    /// </summary>
    private async Task<AgentImpl> CreateOrResumeAgentAsync(
        string sessionId,
        string? systemPrompt,
        double? temperature,
        int? maxTokens,
        CancellationToken cancellationToken)
    {
        var config = new AgentConfig
        {
            Model = _options.DefaultModel,
            SystemPrompt = systemPrompt ?? _options.DefaultSystemPrompt,
            Temperature = temperature,
            MaxTokens = maxTokens,
            Tools = ["*"],
            Permissions = _options.PermissionConfig,
            Skills = _options.SkillsConfig,
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = _options.WorkDir,
                EnforceBoundary = true,
                AllowPaths = [_options.WorkDir, _options.StoreDir]
            }
        };

        // Resume if exists, otherwise create new
        if (await _globalDeps.Store.ExistsAsync(sessionId, cancellationToken))
        {
            return await AgentImpl.ResumeFromStoreAsync(
                sessionId,
                _globalDeps,
                options: null,
                overrides: new AgentConfigOverrides
                {
                    Model = config.Model,
                    SystemPrompt = config.SystemPrompt,
                    Temperature = config.Temperature,
                    MaxTokens = config.MaxTokens,
                    Tools = config.Tools,
                    Permissions = config.Permissions,
                    Skills = config.Skills,
                    SandboxOptions = config.SandboxOptions
                },
                cancellationToken);
        }
        else
        {
            return await AgentImpl.CreateNewAsync(
                sessionId,
                config,
                _globalDeps,
                cancellationToken);
        }
    }

    /// <summary>
    /// Extract system prompt and user input from messages
    /// </summary>
    private static (string? SystemPrompt, string Input) ExtractPromptAndInput(OpenAiChatCompletionRequest request)
    {
        if (request.Messages == null || request.Messages.Count == 0)
        {
            throw new BadHttpRequestException("Messages array is required and must not be empty");
        }

        string? systemPrompt = null;
        var userMessages = new List<string>();

        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
            {
                systemPrompt = msg.GetTextContent();
            }
            else if (msg.Role == "user")
            {
                userMessages.Add(msg.GetTextContent());
            }
        }

        if (userMessages.Count == 0)
        {
            throw new BadHttpRequestException("At least one user message is required");
        }

        var input = string.Join("\n\n", userMessages);
        return (systemPrompt, input);
    }

    /// <summary>
    /// Handle streaming response
    /// </summary>
    private async Task StreamResponseAsync(
        HttpContext httpContext,
        AgentImpl agent,
        string input,
        string sessionId)
    {
        using var activity = _activitySource.StartActivity("StreamResponse");

        httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var streamId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await foreach (var chunk in agent.StreamAsync(input, httpContext.RequestAborted))
        {
            var sseChunk = new OpenAiStreamChunk
            {
                Id = streamId,
                Created = created,
                Model = _options.DefaultModel,
                Choices =
                [
                    new OpenAiStreamChoice
                    {
                        Index = 0,
                        Delta = new OpenAiStreamDelta { Content = chunk },
                        FinishReason = null
                    }
                ]
            };

            var json = JsonSerializer.Serialize(sseChunk, JsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n", httpContext.RequestAborted);
            await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
        }

        // Send final chunk
        var finalChunk = new OpenAiStreamChunk
        {
            Id = streamId,
            Created = created,
            Model = _options.DefaultModel,
            Choices =
            [
                new OpenAiStreamChoice
                {
                    Index = 0,
                    Delta = new OpenAiStreamDelta(),
                    FinishReason = "stop"
                }
            ]
        };

        var finalJson = JsonSerializer.Serialize(finalChunk, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {finalJson}\n\n", httpContext.RequestAborted);
        await httpContext.Response.WriteAsync("data: [DONE]\n\n", httpContext.RequestAborted);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
    }

    /// <summary>
    /// Handle non-streaming response
    /// </summary>
    private async Task NonStreamResponseAsync(
        HttpContext httpContext,
        AgentImpl agent,
        string input)
    {
        using var activity = _activitySource.StartActivity("NonStreamResponse");

        var result = await agent.RunAsync(input, httpContext.RequestAborted);

        var response = new OpenAiChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = _options.DefaultModel,
            Choices =
            [
                new OpenAiChatCompletionChoice
                {
                    Index = 0,
                    FinishReason = MapFinishReason(result.StopReason),
                    Message = new OpenAiChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = result.Response ?? ""
                    }
                }
            ],
            Usage = new OpenAiUsage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0
            }
        };

        await httpContext.Response.WriteAsJsonAsync(response, JsonOptions);
    }

    private static string MapFinishReason(string? stopReason) => stopReason switch
    {
        "max_tokens" => "length",
        "end_turn" => "stop",
        _ => "stop"
    };
}
