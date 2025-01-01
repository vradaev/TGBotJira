using JIRAbot.SuperSet;
using NLog;
using Quartz;

namespace JIRAbot.Job;

public class DashboardJob : IJob
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly SendDashboardService _dashboardService;
    private readonly Config _config;
    

    public DashboardJob(SendDashboardService dashboardService, Config config)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            Logger.Info("Dashboard job started.");

            // Получаем данные задачи из JobDataMap
            long chatId = -311068690;
            var dashboardUrl = _config.SuperSet.DashboardUrl;
            var caption = $"[Dashboard]({dashboardUrl})\n#supportmetric";
            var loginUrl = _config.SuperSet.loginUrl;
            var username = _config.SuperSet.Login;
            var password = _config.SuperSet.Password;

            Logger.Info($"Sending dashboard screenshot to chat ID: {chatId}. Dashboard URL: {dashboardUrl}");

            // Вызываем ваш метод
            await _dashboardService.SendDashboardScreenshotAsync(chatId, dashboardUrl, caption, loginUrl, username, password);

            Logger.Info("Dashboard screenshot sent successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while sending dashboard screenshot.");
            throw; // Для обработки повторов Quartz
        }
    }
}