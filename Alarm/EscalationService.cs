using NLog;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace JIRAbot.Alarm;

public class EscalationService
{
     private readonly TelegramBotClient _botClient;
        private readonly string _channelId;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<(long chatId, long messageId), SosRequest> _sosRequests = new Dictionary<(long, long), SosRequest>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EscalationService(TelegramBotClient botClient, string channelId)
        {
            _botClient = botClient;
            _channelId = channelId;
            _httpClient = new HttpClient();
        }

        public async Task SendAlertAsync(string message, InlineKeyboardMarkup inlineKeyboard, long originalChatId, int sosMessageId, string userName)
        {
            Logger.Info("Sent alarm: {0}, uset: {1}, chat: {2}", message, userName, originalChatId);
            var alertMessage = await _botClient.SendTextMessageAsync(
                chatId: _channelId,
                text: message,
                replyMarkup: inlineKeyboard
            );
            
            Logger.Info("Alarm Sent, ID message: {0}", alertMessage.MessageId);

            // Создаем новый запрос тревоги
            var uniqueMessageId = alertMessage.MessageId;
            var sosRequest = new SosRequest
            {
                SosMessageId = sosMessageId,
                ChatId = originalChatId
            };

            // Сохраняем запрос тревоги
            _sosRequests[(originalChatId, sosMessageId)] = sosRequest;
            
            Logger.Info("Add to Dictionary. message: {0} chatid {1}", sosMessageId, originalChatId);
     
        }
        

        // Метод для обработки принятия тревоги
        public async Task HandleAcceptSos(long chatId, int uniqueMessageId, int alarmMessageId, long alarmChatId, string userName, string channelName)
        {
            // Проверяем, существует ли запрос тревоги
            if (_sosRequests.TryGetValue((chatId, uniqueMessageId), out var sosRequest))
            {
                Logger.Info("Checked in Dictionary. message {0} chatid {1}",uniqueMessageId,chatId);
                // Отправляем ответ в ту же группу с указанием, что тревога принята
                  await _botClient.EditMessageTextAsync(alarmChatId, alarmMessageId,
                      $"✅ Тревога принята пользователем {userName} для клиента: {channelName} ");
                  
                  Logger.Info("Alarm updated in the chatid: {0}, messageid: {1}", alarmChatId, alarmMessageId);

                // Отправляем сообщение в чат, где была вызвана команда sos
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Пользователь {userName} проверяет тревогу.",
                    replyToMessageId: uniqueMessageId // Реплай на оригинальное сообщение
                );

                // Удаляем обработанный запрос из словаря
                _sosRequests.Remove((chatId, uniqueMessageId));
                Logger.Info("Removed from Dictionary. message {0} chatid {1}", uniqueMessageId, chatId );
            }
            else
            {
                Logger.Error("No active SOS request found for this chat and message ID" );
            }
        }

        // Класс для хранения информации о тревоге
        public class SosRequest
        {
            public int SosMessageId { get; set; }
            public long ChatId { get; set; }
        }
    }