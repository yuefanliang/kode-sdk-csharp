using Kode.Agent.Sdk.Core.Skills;
using Kode.Agent.WebApiAssistant.Services;
using Kode.Agent.WebApiAssistant.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

namespace Kode.Agent.WebApiAssistant.Extensions;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加核心服务
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // 添加数据库上下文
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "app.db");
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // 添加持久化服务 - 改为 Scoped 生命周期
        services.AddScoped<IPersistenceService, SqlitePersistenceService>();

        // Scoped 服务（与数据库上下文生命周期一致）
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IApprovalService, ApprovalService>();
        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<SystemConfigService>();
        services.AddSingleton<SystemSkillService>();

        // 添加 Swagger/OpenAPI 支持
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Kode.Agent WebApi Assistant",
                Version = "v1",
                Description = "OpenAI-compatible API for Kode Agent SDK",
                Contact = new OpenApiContact
                {
                    Name = "Kode Agent",
                    Url = new Uri("https://github.com/JinFanZheng/kode-sdk-csharp")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // 包含 XML 注释
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    /// <summary>
    /// 配置 Skills 系统
    /// </summary>
    public static IServiceCollection AddSkillsSupport(
        this IServiceCollection services,
        Action<SkillsConfig>? configure = null)
    {
        // 配置 SkillsConfig
        var skillsConfig = new SkillsConfig
        {
            Paths = new[] { "Skills" },
            Trusted = Array.Empty<string>()
        };

        configure?.Invoke(skillsConfig);

        services.AddSingleton(skillsConfig);

        return services;
    }
}
