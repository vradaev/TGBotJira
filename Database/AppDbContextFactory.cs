using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JIRAbot;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        var connectionString = "Host=172.17.0.1;Port=5433;Database=TelegramJiraDB;Username=postgres;Password=mysecretpassword";
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(connectionString);
    }
}