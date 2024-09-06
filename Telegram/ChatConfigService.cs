using Microsoft.EntityFrameworkCore;

namespace JIRAbot
{
    public class ChatConfigService : IChatConfigService
    {
        private readonly AppDbContext _context;

        public ChatConfigService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<(ChatConfig chatConfig, string channel)> GetChatConfigAndChannelAsync(long chatId)
        {
            // Преобразование chatId в строку для поиска в базе данных
            var groupIdString = chatId.ToString();

            // Поиск группы по идентификатору группы
            var group = await _context.Groups
                .Include(g => g.Client) // Включение клиента для получения его данных
                .FirstOrDefaultAsync(g => g.GroupId == groupIdString);

            if (group != null)
            {
                // Создание конфигурации чата и извлечение имени клиента
                var chatConfig = new ChatConfig
                {
                    ChatId = chatId,
                    ClientName = group.Client?.Name // Имя клиента может быть null
                };

                // Имя клиента также используется как канал
                var channel = group.Client?.Name;
                return (chatConfig, channel);
            }

            // Возврат null если группа не найдена
            return (null, null);
        }
    }
}