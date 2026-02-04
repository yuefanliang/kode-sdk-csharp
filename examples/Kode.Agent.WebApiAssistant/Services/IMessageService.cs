using Kode.Agent.WebApiAssistant.Services.Persistence.Entities;

namespace Kode.Agent.WebApiAssistant.Services;

/// <summary>
/// 消息服务接口
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// 创建消息
    /// </summary>
    Task<MessageEntity> CreateMessageAsync(MessageEntity message);

    /// <summary>
    /// 获取会话的所有消息
    /// </summary>
    Task<IReadOnlyList<MessageEntity>> GetMessagesAsync(string sessionId);

    /// <summary>
    /// 删除会话的所有消息
    /// </summary>
    Task DeleteMessagesAsync(string sessionId);
}
