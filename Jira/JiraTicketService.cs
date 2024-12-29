using Microsoft.EntityFrameworkCore;
using NLog;

namespace JIRAbot;

public class JiraTicketService
{
    
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly AppDbContext _context;
    private readonly JiraClient _jiraClient;

    public JiraTicketService(AppDbContext context, JiraClient jiraClient)
    {
        _context = context;
        _jiraClient = jiraClient;
    }

    public async Task SaveJiraTicketsAsync()
    {
        try
        {
            Logger.Info("Starting Jira ticket synchronization.");

            // Retrieve the last synchronization date from the Settings table
            var lastSyncDate = await GetOrInitializeLastSyncDateAsync();
            Logger.Debug($"Last synchronization date: {lastSyncDate:yyyy-MM-dd HH:mm:ss}");

            // Fetch tickets updated after the last synchronization date
            var tickets = await _jiraClient.GetIssuesUpdatedAfterAsync(lastSyncDate);

            if (!tickets.Any())
            {
                Logger.Info("No new tickets to synchronize.");
                return;
            }

            Logger.Info($"Found {tickets.Count} tickets to process.");

            // Update or insert tickets
            var ticketKeys = tickets.Select(t => t.JiraKey).ToList();
            var existingTickets = await _context.JiraTickets
                                                .Where(t => ticketKeys.Contains(t.JiraKey))
                                                .ToListAsync();

            var newTickets = new List<JiraTicket>();

            foreach (var ticket in tickets)
            {
                var existingTicket = existingTickets.FirstOrDefault(t => t.JiraKey == ticket.JiraKey);
                if (existingTicket != null)
                {
                    // Update existing ticket
                    existingTicket.ClientName = ticket.ClientName;
                    existingTicket.Assignee = ticket.Assignee;
                    existingTicket.Status = ticket.Status;
                    existingTicket.Summary = ticket.Summary;
                    existingTicket.Description = ticket.Description;
                    existingTicket.UpdatedAt = DateTime.UtcNow;
                    existingTicket.ClosedAt = ticket.ClosedAt;
                    existingTicket.FirstRespondAt = ticket.FirstRespondAt;
                }
                else
                {
                    // Add new ticket
                    newTickets.Add(ticket);
                }
            }

            if (newTickets.Any())
            {
                Logger.Info($"Adding {newTickets.Count} new tickets to the database.");
                await _context.JiraTickets.AddRangeAsync(newTickets);
            }

            // Update the last synchronization date
            await UpdateLastSyncDateAsync(DateTime.UtcNow);

            // Save changes to the database
            await _context.SaveChangesAsync();
            Logger.Info("Jira ticket synchronization completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred during Jira ticket synchronization.");
            throw;
        }
    }

    public async Task<DateTime> GetOrInitializeLastSyncDateAsync()
    {
        try
        {
            Logger.Debug("Retrieving the last synchronization date.");

            var lastSyncSetting = await _context.Settings.FirstOrDefaultAsync(s => s.KeyName == "lastjirasync");

            if (lastSyncSetting == null)
            {
                Logger.Warn("Setting 'lastjirasync' not found. Initializing with the default date.");
                var initialDate = new DateTime(2024, 12, 01);
                await UpdateLastSyncDateAsync(initialDate);
                return initialDate;
            }

            if (!DateTime.TryParse(lastSyncSetting.Value, out var parsedDate))
            {
                Logger.Warn($"Invalid value for 'lastjirasync': {lastSyncSetting.Value}. Using the minimum date.");
                return DateTime.MinValue;
            }

            Logger.Debug($"Successfully retrieved the last synchronization date: {parsedDate:yyyy-MM-dd HH:mm:ss}");
            return parsedDate;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while retrieving the last synchronization date.");
            throw;
        }
    }
    private async Task UpdateLastSyncDateAsync(DateTime syncDate)
    {
        try
        {
            Logger.Debug($"Updating the last synchronization date to {syncDate:yyyy-MM-dd HH:mm:ss}.");
            var lastSyncSetting = await _context.Settings.FirstOrDefaultAsync(s => s.KeyName == "lastjirasync");

            if (lastSyncSetting != null)
            {
                // If the setting exists, update its value and timestamp
                lastSyncSetting.Value = syncDate.ToString("yyyy-MM-dd HH:mm:ss");
                lastSyncSetting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // If the setting does not exist, create a new one
                _context.Settings.Add(new Setting
                {
                    KeyName = "lastjirasync",
                    Value = syncDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    Description = "day of sync",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                Logger.Info("Adding new setting 'lastjirasync' to the database.");
            }

            // Save changes to the database
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while updating the last synchronization date.");
            throw;
        }
    }
   
}