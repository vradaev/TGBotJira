using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JIRAbot;

public class AppDbContext : DbContext
{
    private readonly string _connectionString;
    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }
    public DbSet<Clients> Clients { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<RequestType> RequestTypes { get; set; }
    public DbSet<Request> Requests { get; set; }
    public DbSet<JiraTask> JiraTasks { get; set; }
    public DbSet<RequestStatusHistory> RequestStatusHistories { get; set; }
    public DbSet<Comment> Comments { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connectionString);
    } 
}