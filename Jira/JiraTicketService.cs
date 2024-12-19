using Microsoft.EntityFrameworkCore;

namespace JIRAbot;

public class JiraTicketService
{
    private readonly AppDbContext _context;
    private readonly JiraClient _jiraClient;

    public JiraTicketService(AppDbContext context, JiraClient jiraClient)
    {
        _context = context;
        _jiraClient = jiraClient;
    }

    public async Task SaveJiraTicketsAsync()
    {
        // Получаем дату последней синхронизации из таблицы Settings
        var lastSyncSetting = await _context.Settings
                                             .FirstOrDefaultAsync(s => s.KeyName == "lastjirasync");
        DateTime lastSyncDate = lastSyncSetting != null 
            ? DateTime.Parse(lastSyncSetting.Value) 
            : DateTime.MinValue; // Если значения нет, ставим минимальную дату

        // Получаем задачи, измененные после последней синхронизации
        var tickets = await _jiraClient.GetIssuesUpdatedAfterAsync(lastSyncDate);

        foreach (var ticket in tickets)
        {
            var existingTicket = await _context.JiraTickets
                                               .FirstOrDefaultAsync(t => t.JiraKey == ticket.JiraKey);

            if (existingTicket == null)
            {
                // Если задачи нет, добавляем новую
                _context.JiraTickets.Add(ticket);
            }
            else
            {
                // Если задача уже существует, обновляем ее
                existingTicket.ClientName = ticket.ClientName;
                existingTicket.Assignee = ticket.Assignee;
                existingTicket.Status = ticket.Status;
                existingTicket.Summary = ticket.Summary;
                existingTicket.Description = ticket.Description;
                existingTicket.UpdatedAt = DateTime.UtcNow;
                existingTicket.ClosedAt = ticket.ClosedAt;
                existingTicket.FirstRespondAt = ticket.FirstRespondAt;
                
                _context.Entry(existingTicket).State = EntityState.Modified;
            }
        }

        // Обновляем дату последней синхронизации
        if (tickets.Any())
        {
            var latestTicketDate = DateTime.UtcNow;
            if (lastSyncSetting != null)
            {
                lastSyncSetting.Value = latestTicketDate.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                _context.Settings.Add(new Setting
                {
                    KeyName = "lastjirasync",
                    Value = latestTicketDate.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}