using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JIRAbot
{
    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;

        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public DbSet<Client> Clients { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<RequestType> RequestTypes { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<JiraTask> JiraTasks { get; set; }
        public DbSet<RequestStatusHistory> RequestStatusHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<DutyOfficer> DutyOfficers { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Client)
                .WithMany(c => c.Groups)
                .HasForeignKey(g => g.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Group)
                .WithMany(g => g.Requests)
                .HasForeignKey(r => r.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Type)
                .WithMany()
                .HasForeignKey(r => r.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<JiraTask>()
                .HasOne(jt => jt.Request)
                .WithMany(r => r.JiraTasks)
                .HasForeignKey(jt => jt.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RequestStatusHistory>()
                .HasOne(rsh => rsh.Request)
                .WithMany(r => r.StatusHistories)
                .HasForeignKey(rsh => rsh.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Request)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DutyOfficer>(entity =>
            {
                entity.ToTable("DutyOfficers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(15);
                entity.Property(e => e.DutyType).IsRequired();
            });
            modelBuilder.Entity<Setting>(entity =>
            {
                // Указание имени таблицы
                entity.ToTable("Settings");

                // Настройка ключа
                entity.HasKey(s => s.Id);

                // Настройка колонок
                entity.Property(s => s.Id)
                    .HasColumnName("id")
                    .IsRequired()
                    .ValueGeneratedOnAdd(); // Для автогенерации ID

                entity.Property(s => s.KeyName)
                    .HasColumnName("key_name")
                    .IsRequired()
                    .HasMaxLength(255); // Ограничение длины строки

                entity.Property(s => s.Value)
                    .HasColumnName("value")
                    .IsRequired();

                entity.Property(s => s.Description)
                    .HasColumnName("description")
                    .HasMaxLength(500); // Описание опционально

                entity.Property(s => s.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("NOW()") // Установка значения по умолчанию
                    .ValueGeneratedOnAdd();

                entity.Property(s => s.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("NOW()")
                    .ValueGeneratedOnAddOrUpdate();

                // Индекс для ускорения поиска по ключу
                entity.HasIndex(s => s.KeyName)
                    .IsUnique();
            });
            
        }
    }
}