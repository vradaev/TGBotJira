namespace JIRAbot;

public interface IChatConfigService
{
    Task<(ChatConfig chatConfig, string channel)> GetChatConfigAndChannelAsync(long chatId);
}