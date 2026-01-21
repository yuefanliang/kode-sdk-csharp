using System.Diagnostics;
using Kode.Agent.Boilerplate;
using Kode.Agent.Boilerplate.Models;
using Kode.Agent.Mcp;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Types;
using Kode.Agent.Sdk.Infrastructure.Sandbox;
using Kode.Agent.Sdk.Tools;
using Kode.Agent.Store.Json;
using Kode.Agent.Tools.Builtin;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/kode-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Starting Kode.Agent Boilerplate");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add controllers
    builder.Services.AddControllers();

    // Configure options
    builder.Services.AddSingleton(_ =>
        BoilerplateOptions.FromConfiguration(builder.Configuration, builder.Environment.ContentRootPath));

    // Configure agent dependencies
    builder.Services.AddSingleton<IAgentStore>(sp =>
    {
        var options = sp.GetRequiredService<BoilerplateOptions>();
        return new JsonAgentStore(options.StoreDir);
    });

    builder.Services.AddSingleton<IToolRegistry>(sp =>
    {
        var registry = new ToolRegistry();
        registry.RegisterBuiltinTools();
        return registry;
    });

    builder.Services.AddSingleton<ISandboxFactory, LocalSandboxFactory>();

    builder.Services.AddSingleton<IModelProvider>(sp =>
    {
        var options = sp.GetRequiredService<BoilerplateOptions>();
        var configuration = sp.GetRequiredService<IConfiguration>();

        return ModelProviderFactory.CreateFromConfiguration(
            configuration, 
            options.DefaultProvider,
            options.DefaultModel);
    });

    builder.Services.AddSingleton(sp => new AgentDependencies
    {
        Store = sp.GetRequiredService<IAgentStore>(),
        ToolRegistry = sp.GetRequiredService<IToolRegistry>(),
        SandboxFactory = sp.GetRequiredService<ISandboxFactory>(),
        ModelProvider = sp.GetRequiredService<IModelProvider>(),
        LoggerFactory = sp.GetService<ILoggerFactory>()
    });

    // Add MCP client manager
    builder.Services.AddMcpClientManager();

    // Add assistant service
    builder.Services.AddSingleton<AssistantService>();

    // Add OpenTelemetry activity source
    builder.Services.AddSingleton(new ActivitySource("Kode.Agent.Boilerplate"));

    // Configure OpenTelemetry
    var otelConfig = builder.Configuration.GetSection("OpenTelemetry");
    var otelEnabled = otelConfig.GetValue<bool>("Enabled");
    if (otelEnabled)
    {
        var serviceName = otelConfig["ServiceName"] ?? "Kode.Agent.Boilerplate";
        var serviceVersion = otelConfig["ServiceVersion"] ?? "1.0.0";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Kode.Agent.Boilerplate")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                var exporter = otelConfig["Exporter"]?.ToLowerInvariant();
                if (exporter == "otlp")
                {
                    var otlpEndpoint = otelConfig["OtlpEndpoint"];
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        tracing.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                    }
                }
                else
                {
                    tracing.AddConsoleExporter();
                }
            });

        Log.Information("OpenTelemetry enabled with exporter: {Exporter}", otelConfig["Exporter"]);
    }

    var app = builder.Build();

    // Load MCP tools
    var mcpManager = app.Services.GetRequiredService<McpClientManager>();
    var toolRegistry = app.Services.GetRequiredService<IToolRegistry>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    
    await LoadMcpToolsAsync(app.Configuration, mcpManager, toolRegistry, logger);

    // Map endpoints
    app.MapControllers();

    app.MapGet("/", () => Results.Json(new
    {
        name = "Kode.Agent Boilerplate",
        version = "1.0.0",
        openai_compatible = true,
        endpoints = new[]
        {
            "POST /v1/chat/completions",
            "POST /{sessionId}/v1/chat/completions",
            "GET  /health"
        },
        headers = new
        {
            X_Session_Id = "Session ID for multi-turn conversations"
        }
    }));

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    // OpenAI-compatible endpoints
    app.MapPost("/v1/chat/completions",
        async (HttpContext ctx, OpenAiChatCompletionRequest req, AssistantService svc) =>
            await svc.HandleChatCompletionsAsync(ctx, req));

    app.MapPost("/{sessionId}/v1/chat/completions",
        async (HttpContext ctx, OpenAiChatCompletionRequest req, AssistantService svc) =>
            await svc.HandleChatCompletionsAsync(ctx, req));

    Log.Information("Kode.Agent Boilerplate started successfully");
    Log.Information("Listening on: {Urls}", app.Configuration["Urls"]);
    Log.Information("Available endpoints:");
    Log.Information("  POST {BaseUrl}/v1/chat/completions", app.Configuration["Urls"]);
    Log.Information("  POST {BaseUrl}/{{sessionId}}/v1/chat/completions", app.Configuration["Urls"]);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static async Task LoadMcpToolsAsync(
    IConfiguration configuration,
    McpClientManager mcpManager,
    IToolRegistry toolRegistry,
    ILogger logger)
{
    var mcpServersSection = configuration.GetSection("McpServers");
    if (!mcpServersSection.Exists())
    {
        logger.LogInformation("No MCP servers configured");
        return;
    }

    var servers = mcpServersSection.GetChildren();
    foreach (var serverSection in servers)
    {
        var serverName = serverSection.Key;
        var transport = serverSection["transport"];
        var url = serverSection["url"];

        if (string.IsNullOrEmpty(transport) || string.IsNullOrEmpty(url))
        {
            logger.LogWarning("Skipping MCP server {ServerName}: missing transport or url", serverName);
            continue;
        }

        try
        {
            var mcpConfig = new McpConfig
            {
                Name = serverName,
                Transport = transport,
                Url = url
            };

            var tools = await McpToolProvider.GetToolsAsync(mcpManager, mcpConfig, logger);
            foreach (var tool in tools)
            {
                toolRegistry.Register(tool);
            }

            logger.LogInformation("Loaded {ToolCount} tools from MCP server: {ServerName}", tools.Count, serverName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load MCP server: {ServerName}", serverName);
        }
    }
}
