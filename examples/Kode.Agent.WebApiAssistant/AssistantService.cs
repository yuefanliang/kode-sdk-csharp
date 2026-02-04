using System.Net;
using System.Text.Json;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;
using Kode.Agent.Sdk.Core.Abstractions;
using AgentDependencies = Kode.Agent.Sdk.Core.Types.AgentDependencies;
using Kode.Agent.WebApiAssistant.Assistant;
using Kode.Agent.WebApiAssistant.OpenAI;
using Kode.Agent.WebApiAssistant.Services;

namespace Kode.Agent.WebApiAssistant;

/// <summary>
/// Options for getting an owned agent.
/// </summary>
public sealed record GetOwnedAgentOptions
{
    /// <summary>Whether this appears to be a new conversation</summary>
    public bool IsNewConversation { get; init; }
    /// <summary>The thread key (from OpenAI user field)</summary>
    public string? ThreadKey { get; init; }
    /// <summary>The auto request mode classification</summary>
    public AutoRequestMode AutoRequestMode { get; init; } = AutoRequestMode.Unknown;
    /// <summary>System prompt override</summary>
    public string? SystemPrompt { get; init; }
    /// <summary>Temperature override</summary>
    public double? Temperature { get; init; }
    /// <summary>Max tokens override</summary>
    public int? MaxTokens { get; init; }
}

/// <summary>
/// Result of GetOwnedAgentAsync operation.
/// </summary>
public sealed record GetOwnedAgentResult
{
    /// <summary>Lease that holds the agent instance and controls release/cleanup</summary>
    public AssistantAgentPool.Lease? Lease { get; init; }
    /// <summary>The agent ID</summary>
    public string? AgentId { get; init; }
    /// <summary>Whether the response has already been written (error case)</summary>
    public bool ResponseWritten { get; init; }
}

public sealed class AssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AgentDependencies _globalDeps;
    private readonly AssistantOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenAiRoutingStateManager _routingStateManager;
    private readonly AssistantAgentPool _agentPool;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        AgentDependencies globalDeps,
        AssistantOptions options,
        IServiceProvider serviceProvider,
        AssistantAgentPool agentPool,
        ILogger<AssistantService> logger)
    {
        _globalDeps = globalDeps;
        _options = options;
        _serviceProvider = serviceProvider;
        _agentPool = agentPool;
        _logger = logger;
        _routingStateManager = new OpenAiRoutingStateManager(
            options.WorkDir,
            serviceProvider.GetService<ILogger<OpenAiRoutingStateManager>>());
    }

    /// <summary>
    /// Get or create an agent based on session routing rules.
    /// This implements the same logic as the TypeScript getOwnedAgent function.
    /// </summary>
    private async Task<GetOwnedAgentResult> GetOwnedAgentAsync(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request,
        GetOwnedAgentOptions opts,
        CancellationToken cancellationToken)
    {
        // For now, we don't have user authentication, so userId is null (global routing)
        string? userId = null;

        // Get session ID from path and headers
        var agentIdFromPath = SessionRouting.NormalizeSessionId(
            httpContext.Request.RouteValues.TryGetValue("sessionId", out var pathVal) ? pathVal?.ToString() : null);

        var agentIdFromHeader = SessionRouting.NormalizeSessionId(
            httpContext.Request.Headers.TryGetValue("X-Session-Id", out var sessionHeader) ? sessionHeader.ToString() : null);

        // Check for conflict between path and header
        if (agentIdFromPath != null && agentIdFromHeader != null && agentIdFromPath != agentIdFromHeader)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = "sessionId in path must match X-Session-Id header when both are provided",
                    type = "invalid_request_error",
                    param = "sessionId",
                    code = (string?)null
                }
            }, JsonOptions, cancellationToken);
            return new GetOwnedAgentResult { ResponseWritten = true };
        }

        var explicitAgentId = agentIdFromPath ?? agentIdFromHeader;

        // Case 1: Explicit session ID provided
        if (explicitAgentId != null)
        {
            var lease = await _agentPool.LeaseAsync(explicitAgentId, opts, cancellationToken);
            SetSessionIdHeaders(httpContext, explicitAgentId);
            return new GetOwnedAgentResult { Lease = lease, AgentId = explicitAgentId };
        }

        // Case 2: Thread key provided (OpenAI user field)
        var threadKey = SessionRouting.NormalizeThreadKey(opts.ThreadKey);
        if (threadKey != null)
        {
            var state = _routingStateManager.LoadRoutingState(userId);
            var mappedId = state.ThreadKeyToAgentId?.GetValueOrDefault(threadKey);

            if (mappedId != null && await _globalDeps.Store.ExistsAsync(mappedId, cancellationToken))
            {
                var lease = await _agentPool.LeaseAsync(mappedId, opts, cancellationToken);
                SetSessionIdHeaders(httpContext, mappedId);
                return new GetOwnedAgentResult { Lease = lease, AgentId = mappedId };
            }

            // First time seeing this threadKey: create new session and bind it
            var newAgentId = AssistantBuilder.GenerateAgentId();
            var newLease = await _agentPool.LeaseAsync(newAgentId, opts, cancellationToken);

            var nextState = state with
            {
                ThreadKeyToAgentId = new Dictionary<string, string>(state.ThreadKeyToAgentId ?? new())
                {
                    [threadKey] = newAgentId
                }
            };
            _routingStateManager.SaveRoutingState(userId, nextState);
            SetSessionIdHeaders(httpContext, newAgentId);
            return new GetOwnedAgentResult { Lease = newLease, AgentId = newAgentId };
        }

        // Case 3: Auto default session (no explicit session, no threadKey)
        {
            var state = _routingStateManager.LoadRoutingState(userId);
            var autoDefault = state.AutoDefaultAgentId;
            var currentMode = opts.AutoRequestMode;

            // Conservative strategy: only create new session in auto mode when:
            // - Last request was "history" mode AND current request looks like new conversation
            // This handles both:
            // - Clients that always send single user message: reuse same session
            // - Clients that send history: switching from history to single-user indicates "new conversation"
            var lastMode = SessionRouting.ParseMode(state.AutoLastRequestMode);
            var shouldCreateNewAutoSession = opts.IsNewConversation && lastMode == AutoRequestMode.History;

            if (!shouldCreateNewAutoSession && autoDefault != null && await _globalDeps.Store.ExistsAsync(autoDefault, cancellationToken))
            {
                var lease = await _agentPool.LeaseAsync(autoDefault, opts, cancellationToken);
                _routingStateManager.SaveRoutingState(userId, state with
                {
                    AutoDefaultAgentId = autoDefault,
                    AutoLastRequestMode = SessionRouting.ModeToString(currentMode),
                    AutoLastRequestAt = DateTimeOffset.UtcNow.ToString("O")
                });
                SetSessionIdHeaders(httpContext, autoDefault);
                return new GetOwnedAgentResult { Lease = lease, AgentId = autoDefault };
            }

            // Create new auto-default session
            var newAgentId = AssistantBuilder.GenerateAgentId();
            var newLease = await _agentPool.LeaseAsync(newAgentId, opts, cancellationToken);
            _routingStateManager.SaveRoutingState(userId, state with
            {
                AutoDefaultAgentId = newAgentId,
                AutoLastRequestMode = SessionRouting.ModeToString(currentMode),
                AutoLastRequestAt = DateTimeOffset.UtcNow.ToString("O")
            });
            SetSessionIdHeaders(httpContext, newAgentId);
            return new GetOwnedAgentResult { Lease = newLease, AgentId = newAgentId };
        }
    }

    /// <summary>
    /// Create or resume an agent with the given ID.
    /// </summary>
    // Agent creation is handled by AssistantAgentPool (pooled or per-request).

    /// <summary>
    /// Set session ID response headers.
    /// </summary>
    private static void SetSessionIdHeaders(HttpContext httpContext, string agentId)
    {
        httpContext.Response.Headers["X-Session-Id"] = agentId;
    }

    public async Task HandleChatCompletionsAsync(HttpContext httpContext, OpenAiChatCompletionRequest request)
    {
        if (!Authorize(httpContext))
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = "Unauthorized",
                    type = "authentication_error"
                }
            }, JsonOptions);
            return;
        }

        string? systemPrompt;
        string input;
        try
        {
            (systemPrompt, input) = ExtractPromptAndInput(request);
        }
        catch (BadHttpRequestException ex)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = ex.Message,
                    type = "invalid_request_error",
                    param = "messages",
                    code = (string?)null
                }
            }, JsonOptions);
            return;
        }

        // Classify the request to determine session routing behavior
        var classification = SessionRouting.ClassifyAutoRequestFromMessages(request.Messages);

        // Get or create agent based on session routing rules
        var agentResult = await GetOwnedAgentAsync(
            httpContext,
            request,
            new GetOwnedAgentOptions
            {
                IsNewConversation = classification.IsNewConversation,
                AutoRequestMode = classification.Mode,
                ThreadKey = request.User,
                SystemPrompt = systemPrompt,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            },
            httpContext.RequestAborted);

        // If response was already written (error case), return early
        if (agentResult.ResponseWritten)
        {
            return;
        }

        var lease = agentResult.Lease!;
        var agent = lease.Agent;
        var agentId = agentResult.AgentId!;

        await using (lease)
        {
            if (request.Stream)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                httpContext.Response.ContentType = "text/event-stream";
                httpContext.Response.Headers["Cache-Control"] = "no-cache";
                httpContext.Response.Headers["X-Accel-Buffering"] = "no";

                await StreamAsOpenAiSseAsync(httpContext, agent, input, _options.DefaultModel!, agentId);
                return;
            }

            var result = await agent.RunAsync(input, httpContext.RequestAborted);

            var response = new OpenAiChatCompletionResponse
            {
                Id = "chatcmpl-" + Guid.NewGuid().ToString("N"),
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = _options.DefaultModel!,
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
    }

    private bool Authorize(HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return true;

        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var auth))
        {
            return false;
        }

        var value = auth.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var token = value["Bearer ".Length..].Trim();

        return string.Equals(token, _options.ApiKey, StringComparison.Ordinal);
    }

    private static (string? SystemPrompt, string Input) ExtractPromptAndInput(OpenAiChatCompletionRequest request)
    {
        if (request.Messages.Count == 0)
        {
            throw new BadHttpRequestException("messages is required and must be a non-empty array");
        }

        var systemPrompts = request.Messages
            .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.GetTextContent())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var systemPrompt = systemPrompts.Count > 0 ? string.Join("\n", systemPrompts) : null;

        var lastNonSystem = request.Messages
            .LastOrDefault(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase));
        if (lastNonSystem == null)
        {
            throw new BadHttpRequestException("At least one user message is required");
        }

        if (!string.Equals(lastNonSystem.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException("At least one user message is required");
        }

        var input = lastNonSystem.GetTextContent();
        return (systemPrompt, input);
    }

    private async Task StreamAsOpenAiSseAsync(
        HttpContext httpContext,
        AgentImpl agent,
        string input,
        string model,
        string agentId)
    {
        var streamId = "chatcmpl-" + Guid.NewGuid().ToString("N");
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _logger?.LogInformation("Starting SSE stream for agent {AgentId} with input: {Input}", agentId, input);

        // Note: Session ID headers are already set by SetSessionIdHeaders in GetOwnedAgentAsync

        await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
        {
            Id = streamId,
            Created = created,
            Model = model,
            Choices =
            [
                new OpenAiChatCompletionChunkChoice
                {
                    Index = 0,
                    Delta = new OpenAiChatCompletionDelta { Role = "assistant" }
                }
            ]
        });

        // Get stream processor and approval service from request scope
        var streamProcessor = httpContext.RequestServices.GetService<StreamProcessorService>();
        var approvalService = httpContext.RequestServices.GetService<IApprovalService>();

        var sessionContext = streamProcessor?.GetOrCreateSession(agentId);
        var userId = GetUserId(httpContext) ?? "default-user-001";

        if (sessionContext != null)
        {
            sessionContext.UserId = userId;
        }

        try
        {
            // 启动审批监听任务（监听 Control 通道）
            var approvalCts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
            var approvalTask = Task.Run(async () =>
            {
                try
                {
                    if (approvalService == null)
                    {
                        _logger?.LogWarning("ApprovalService not available");
                        return;
                    }

                    await foreach (var envelope in agent.EventBus.SubscribeAsync(
                        EventChannel.Control,
                        cancellationToken: approvalCts.Token))
                    {
                        if (envelope.Event is PermissionRequiredEvent permission)
                        {
                            _logger?.LogInformation("收到审批请求 - 工具: {Tool}, CallId: {CallId}",
                                permission.Call.Name, permission.Call.Id);

                            // 创建审批记录
                            var approvalId = await approvalService.CreateApprovalAsync(
                                agentId,
                                userId,
                                permission.Call.Name,
                                permission.Call.InputPreview);

                            // 注册回调
                            if (permission.Respond != null)
                            {
                                async Task wrappedCallback(string decision, object? options)
                                {
                                    var opts = options != null ?
                                        System.Text.Json.JsonSerializer.Deserialize<PermissionRespondOptions>(
                                            System.Text.Json.JsonSerializer.Serialize(options)) : null;
                                    await permission.Respond(decision, opts);
                                }
                                approvalService.RegisterApprovalCallback(permission.Call.Id, wrappedCallback);
                            }

                            // 发送审批事件到前端
                            await WriteApprovalEventAsync(httpContext, streamId, created, model,
                                new
                                {
                                    type = "approval_required",
                                    approval_id = approvalId,
                                    tool_name = permission.Call.Name,
                                    tool_id = permission.Call.Id,
                                    input_preview = permission.Call.InputPreview
                                });

                            // 等待审批完成（轮询方式）
                            var approved = false;
                            var rejected = false;
                            var maxWaitTime = TimeSpan.FromMinutes(5);
                            var startTime = DateTime.UtcNow;

                            while (DateTime.UtcNow - startTime < maxWaitTime &&
                                   !approvalCts.Token.IsCancellationRequested)
                            {
                                var currentApproval = await approvalService.GetApprovalAsync(approvalId);

                                if (currentApproval == null)
                                {
                                    _logger?.LogWarning("审批记录不存在 - 审批ID: {ApprovalId}", approvalId);
                                    await agent.DenyToolCallAsync(permission.Call.Id, "审批记录丢失");
                                    rejected = true;
                                    break;
                                }

                                _logger?.LogDebug("检查审批状态 - 审批ID: {ApprovalId}, 状态: {Status}",
                                    approvalId, currentApproval.Decision);

                                if (currentApproval.Decision == "approved")
                                {
                                    // 批准工具调用
                                    await agent.ApproveToolCallAsync(permission.Call.Id);
                                    _logger?.LogInformation("工具调用已批准 - CallId: {CallId}",
                                        permission.Call.Id);
                                    approved = true;
                                    break;
                                }
                                else if (currentApproval.Decision == "denied")
                                {
                                    // 拒绝工具调用
                                    await agent.DenyToolCallAsync(permission.Call.Id,
                                        "用户拒绝执行");
                                    _logger?.LogInformation("工具调用被拒绝 - CallId: {CallId}",
                                        permission.Call.Id);
                                    rejected = true;
                                    break;
                                }

                                // 继续等待
                                await Task.Delay(TimeSpan.FromSeconds(2), approvalCts.Token);
                            }

                            // 检查是否超时
                            if (!approved && !rejected)
                            {
                                _logger?.LogWarning("审批超时 - CallId: {CallId}", permission.Call.Id);
                                await agent.DenyToolCallAsync(permission.Call.Id, "审批超时");
                                rejected = true;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "审批监听任务失败");
                }
            }, approvalCts.Token);

            // 处理对话流
            await foreach (var envelope in agent.ChatStreamAsync(
                input,
                opts: null,
                cancellationToken: httpContext.RequestAborted))
            {
                switch (envelope.Event)
                {
                    case TextChunkEvent textChunk when !string.IsNullOrEmpty(textChunk.Delta):
                        await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
                        {
                            Id = streamId,
                            Created = created,
                            Model = model,
                            Choices =
                            [
                                new OpenAiChatCompletionChunkChoice
                                {
                                    Index = 0,
                                    Delta = new OpenAiChatCompletionDelta { Content = textChunk.Delta }
                                }
                            ]
                        });
                        break;

                    case ToolStartEvent toolStart:
                        _logger?.LogInformation("工具开始执行 - {ToolName}", toolStart.Call.Name);
                        await WriteToolEventAsync(httpContext, streamId, created, model,
                            new
                            {
                                type = "tool_start",
                                tool_name = toolStart.Call.Name,
                                tool_id = toolStart.Call.Id
                            });
                        break;

                    case ToolEndEvent toolEnd:
                        _logger?.LogInformation("工具执行完成 - {ToolName}, 成功: {Success}",
                            toolEnd.Call.Name, !toolEnd.Call.IsError);
                        await WriteToolEventAsync(httpContext, streamId, created, model,
                            new
                            {
                                type = "tool_end",
                                tool_name = toolEnd.Call.Name,
                                tool_id = toolEnd.Call.Id,
                                success = !toolEnd.Call.IsError,
                                error = toolEnd.Call.Error
                            });
                        break;

                    case ToolErrorEvent toolError:
                        _logger?.LogError("工具执行错误 - {ToolName}: {Error}",
                            toolError.Call.Name, toolError.Error);

                        await WriteToolEventAsync(httpContext, streamId, created, model,
                            new
                            {
                                type = "tool_error",
                                tool_name = toolError.Call.Name,
                                tool_id = toolError.Call.Id,
                                error = toolError.Error
                            });
                        break;

                    case DoneEvent done:
                        _logger?.LogInformation("对话流完成 - 原因: {Reason}", done.Reason);

                        // 取消审批监听
                        approvalCts.Cancel();
                        try { await approvalTask; } catch { }

                        await WriteMetadataEventAsync(httpContext, streamId, created, model,
                            new
                            {
                                type = "stream_end",
                                reason = done.Reason.ToString(),
                                is_error = string.Equals(done.Reason.ToString(), "error", StringComparison.OrdinalIgnoreCase)
                            });

                        await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
                        {
                            Id = streamId,
                            Created = created,
                            Model = model,
                            Choices =
                            [
                                new OpenAiChatCompletionChunkChoice
                                {
                                    Index = 0,
                                    Delta = new OpenAiChatCompletionDelta(),
                                    FinishReason = MapFinishReason(done.Reason)
                                }
                            ]
                        });

                        // 发送会话统计信息
                        if (streamProcessor != null)
                        {
                            var stats = streamProcessor.GetSessionStats(agentId);
                            await WriteMetadataEventAsync(httpContext, streamId, created, model,
                                new
                                {
                                    type = "session_stats",
                                    file_id = stats.CurrentFileId,
                                    error_count = stats.ErrorCount,
                                    pending_approval_count = stats.PendingApprovalCount
                                });
                        }

                        await httpContext.Response.WriteAsync("data: [DONE]\n\n");
                        await httpContext.Response.Body.FlushAsync();
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; cancel approval listener
            _logger?.LogInformation("客户端断开连接");
        }
        catch (IOException)
        {
            // Connection aborted mid-write; ignore.
            _logger?.LogWarning("连接中止");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SSE流处理异常");
            try
            {
                await WriteErrorEventAsync(httpContext, streamId, created, model, new
                {
                    type = "runtime_error",
                    error = ex.Message
                });
            }
            catch { }
        }
        finally
        {
            // 清理资源
            try
            {
                await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
                {
                    Id = streamId,
                    Created = created,
                    Model = model,
                    Choices =
                        [
                            new OpenAiChatCompletionChunkChoice
                            {
                                Index = 0,
                                Delta = new OpenAiChatCompletionDelta(),
                                FinishReason = "stop"
                            }
                        ]
                });
                await httpContext.Response.WriteAsync("data: [DONE]\n\n");
            }
            catch { }
        }
    }

    private static async Task WriteSseAsync(HttpContext httpContext, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    private static async Task WriteSseEventAsync(HttpContext httpContext, string eventName, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"event: {eventName}\n");
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    private static async Task WriteErrorEventAsync(
        HttpContext httpContext,
        string streamId,
        long created,
        string model,
        object errorData)
    {
        var payload = new
        {
            id = streamId,
            created = created,
            model = model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = $"[系统消息] 错误: {JsonSerializer.Serialize(errorData)}" }
                }
            },
            custom = errorData
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    /// <summary>
    /// 写入审批事件到SSE流
    /// </summary>
    private static async Task WriteApprovalEventAsync(
        HttpContext httpContext,
        string streamId,
        long created,
        string model,
        object approvalData)
    {
        var payload = new
        {
            id = streamId,
            created = created,
            model = model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = $"[系统消息] 等待审批: {JsonSerializer.Serialize(approvalData)}" }
                }
            },
            custom = approvalData
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    /// <summary>
    /// 写入工具事件到SSE流
    /// </summary>
    private static async Task WriteToolEventAsync(
        HttpContext httpContext,
        string streamId,
        long created,
        string model,
        object toolData)
    {
        var payload = new
        {
            id = streamId,
            created = created,
            model = model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = $"[工具] {JsonSerializer.Serialize(toolData)}" }
                }
            },
            custom = toolData
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    /// <summary>
    /// 写入元数据事件到SSE流
    /// </summary>
    private static async Task WriteMetadataEventAsync(
        HttpContext httpContext,
        string streamId,
        long created,
        string model,
        object metadata)
    {
        var payload = new
        {
            id = streamId,
            created = created,
            model = model,
            choices = Array.Empty<object>(),
            custom = metadata
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    private static string MapFinishReason(StopReason stopReason)
    {
        return stopReason switch
        {
            StopReason.EndTurn => "stop",
            StopReason.MaxIterations => "length",
            StopReason.Cancelled => "stop",
            StopReason.AwaitingApproval => "stop",
            StopReason.Error => "stop",
            _ => "stop"
        };
    }

    private static string MapFinishReason(string reason)
    {
        // Map DoneEvent.Reason string values to OpenAI finish_reason
        return reason?.ToLowerInvariant() switch
        {
            "completed" => "stop",
            "interrupted" => "stop",
            "cancelled" => "stop",
            "error" => "stop",
            "length" => "length",
            "max_iterations" => "length",
            _ => "stop"
        };
    }

    private static string? GetUserId(HttpContext httpContext)
    {
        // Try to get userId from headers or query parameters
        if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader))
        {
            return userIdHeader.ToString();
        }

        if (httpContext.Request.Query.TryGetValue("userId", out var userIdQuery))
        {
            return userIdQuery.ToString();
        }

        return null;
    }
}
