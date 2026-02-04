using Kode.Agent.WebApiAssistant.Services.Persistence;
using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 消息服务实现
/// </summary>
public class MessageService : IMessageService
{
    private readonly IPersistenceService _persistenceService;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IPersistenceService persistenceService,
        ILogger<MessageService> logger)
    {
        _persistenceService = persistenceService;
        _logger = logger;
    }

    public async Task<MessageEntity> CreateMessageAsync(MessageEntity message)
    {
        try
        {
            return await _persistenceService.CreateMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create message for session: {SessionId}", message.SessionId);
            throw;
        }
    }

    public async Task<IReadOnlyList<MessageEntity>> GetMessagesAsync(string sessionId)
    {
        try
        {
            return await _persistenceService.ListMessagesAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for session: {SessionId}", sessionId);
            return Array.Empty<MessageEntity>();
        }
    }

    public async Task DeleteMessagesAsync(string sessionId)
    {
        try
        {
            await _persistenceService.DeleteMessagesAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete messages for session: {SessionId}", sessionId);
        }
    }
}
