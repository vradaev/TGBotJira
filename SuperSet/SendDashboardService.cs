using NLog;
using PuppeteerSharp;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace JIRAbot.SuperSet;

public class SendDashboardService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly TelegramBotClient _botClient;
    
    public SendDashboardService(TelegramBotClient botClient)
    {
        _botClient = botClient;
     
    }

    
     public async Task SendDashboardScreenshotAsync(long chatId, string dashboardUrl, string caption, string loginUrl, string username, string password)
    {
        // Логируем начало процесса
        Logger.Info($"Starting to capture and send screenshot for dashboard: {dashboardUrl}");

        var screenshotPath = Path.Combine(Path.GetTempPath(), "dashboard_screenshot.png");

        try
        {
            // Захватываем скриншот
            Logger.Info("Capturing screenshot...");
            await CaptureScreenshotAsync(dashboardUrl, screenshotPath, loginUrl, username, password);

            // Отправляем скриншот в Telegram
            Logger.Info("Sending screenshot to Telegram...");
            using (var stream = new FileStream(screenshotPath, FileMode.Open))
            {
                var fileToSend = new InputFileStream(stream, "dashboard_screenshot.png");
                await _botClient.SendPhotoAsync(chatId, fileToSend, caption: caption);
            }

            // Удаляем временный файл
            Logger.Info("Deleting temporary screenshot file...");
            System.IO.File.Delete(screenshotPath);

            Logger.Info("Screenshot sent and temporary file deleted successfully.");
        }
        catch (Exception ex)
        {
            // Логируем ошибку, если что-то пошло не так
            Logger.Error(ex, "An error occurred while sending the dashboard screenshot.");
        }
    }

    private async Task CaptureScreenshotAsync(string dashboardUrl, string filePath, string loginUrl, string username, string password)
    {
        try
        {
            Logger.Info("Downloading Puppeteer browser...");
            var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
            //    Path = "/app/.chrome" 
            });
            await browserFetcher.DownloadAsync();
            
            // Запуск браузера с указанием пути к Chromium
            var browserOptions = new LaunchOptions
            {
                Headless = true,
              //  ExecutablePath = "/app/.chrome/Chrome/Linux-130.0.6723.69/chrome-linux64/chrome",
                Args = new[] { "--no-sandbox" } 
            };
            
            Logger.Info("Launching browser...");
            using var browser = await Puppeteer.LaunchAsync(browserOptions);
            using var page = await browser.NewPageAsync();
            
            Logger.Info("Navigating to login page...");
            await page.GoToAsync(loginUrl);
            
            Logger.Info($"Entering credentials for user: {username}");
            await page.TypeAsync("input[name='username']", username);
            await page.TypeAsync("input[name='password']", password);
            
            await page.WaitForSelectorAsync("input.btn.btn-primary[type='submit'][value='Sign In']", new WaitForSelectorOptions { Timeout = 5000 });

            // Click to the button
            await page.ClickAsync("input.btn.btn-primary[type='submit'][value='Sign In']");

            // Waiting page after login
            Logger.Info("Waiting for navigation after login...");
            await page.WaitForNavigationAsync();
            
            // set screen size
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1280,    // Ширина
                Height = 1280,   // Высота
                IsMobile = false, // Не мобильная версия
                DeviceScaleFactor = 1  // Масштаб (1 — нормальный, 2 — высокая плотность пикселей)
            });

            // redirect to dashboard url
            Logger.Info("Navigating to dashboard page...");
            await page.GoToAsync(dashboardUrl);
            
            //delay to load dashboard page
            Logger.Info("Delay before open dashboard page...");
            await Task.Delay(3000);

            // Take screen
            Logger.Info($"Taking screenshot and saving to: {filePath}");
            await page.ScreenshotAsync(filePath);

            Logger.Info("Screenshot captured successfully.");
        }
        catch (Exception ex)
        {
            // Error during screen capturing
            Logger.Error(ex, "An error occurred while capturing the screenshot.");
            throw;
        }
    }
}