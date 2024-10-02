using Microsoft.EntityFrameworkCore;
using NLog;

namespace JIRAbot.Alarm;

public class NotificationService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly SmscConfig _smscConfig;
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _dbContext;
    
    public NotificationService(SmscConfig smscConfig, AppDbContext dbContext)
    {
        _smscConfig = smscConfig;
        _httpClient = new HttpClient();
        _dbContext = dbContext;
    }
    
    public async Task<List<string>> GetDutyOfficersPhoneNumbersAsync()
    {
        // Получаем список всех дежурных
        var phoneNumbers = await _dbContext.DutyOfficers
            .Select(d => d.PhoneNumber)
            .ToListAsync();

        return phoneNumbers;
    }

    public async Task SendSmsAsync(string alertId, string message)
    {
        Logger.Info("Sending sms. AlertId: {0}", alertId, message);

        var phoneNumbers = await GetDutyOfficersPhoneNumbersAsync();

        // Если нет номеров телефонов, логируем ошибку
        if (!phoneNumbers.Any())
        {
            Logger.Warn("No duty officers found. SMS cannot be sent.");
            return;
        }
        foreach (var phoneNumber in phoneNumbers)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"login", _smscConfig.Login},
                {"psw", _smscConfig.ApiKey},
                {"phones", phoneNumber},
                {"mes", message},
                {"sender", _smscConfig.Sender},
                {"fmt", "3"}
            };

            var url = $"https://smsc.ru/sys/send.php?{string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"))}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                Logger.Info("SMS sent successfully to {0}. AlertId: {1}", phoneNumber, alertId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error sending SMS to {0}. AlertId: {1}", phoneNumber, alertId);
            }
        }  
       
    }

    public async Task MakeCallAsync(string alertId, string message)
    {
        Logger.Info("Making call. AlertId: {0}", alertId);

        // Получаем телефонные номера из базы данных
        var phoneNumbers = await GetDutyOfficersPhoneNumbersAsync(); 

        foreach (var phoneNumber in phoneNumbers)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"login", _smscConfig.Login},
                {"psw", _smscConfig.ApiKey},
                {"phones", phoneNumber},
                {"mes", message}, // Сообщение, которое будет озвучено
                {"call", "1"}, // Указываем, что требуется звонок
                {"fmt", "3"} // Ответ в формате JSON
            };

            var url = $"https://smsc.ru/sys/send.php?{string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"))}";

            try
            {
                Logger.Info("Calling: {0}", phoneNumber);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                Logger.Info("Call initiated successfully. AlertId: {0}, Phone: {1}", alertId, phoneNumber);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initiating call for AlertId: {0}, Phone: {1}", alertId, phoneNumber);
            }
        }
    }
}