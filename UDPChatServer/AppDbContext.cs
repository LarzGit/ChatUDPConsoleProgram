using Microsoft.EntityFrameworkCore;
using UdpChatServer.Models;

namespace UdpChatServer
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<LogEntry> LogEntries { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<BlacklistEntry> BlacklistEntries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(@"Server=localhost;User=root;Password=root123;Database=udp_chat;",
                new MySqlServerVersion(new Version(8, 0, 34)));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(u => u.Login).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<Contact>(e =>
            {
                e.HasIndex(c => c.OwnerId);
                e.HasIndex(c => c.ContactUserId);
                e.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerId);
                e.HasOne<User>().WithMany().HasForeignKey(c => c.ContactUserId);
            });

            modelBuilder.Entity<BlacklistEntry>(e =>
            {
                e.HasIndex(b => b.OwnerId);
                e.HasIndex(b => b.BlockedUserId);
                e.HasOne<User>().WithMany().HasForeignKey(b => b.OwnerId);
                e.HasOne<User>().WithMany().HasForeignKey(b => b.BlockedUserId);
            });

            modelBuilder.Entity<Message>(e =>
            {
                e.HasIndex(m => m.Content).HasDatabaseName("idx_content_fulltext");
                e.HasOne<User>().WithMany().HasForeignKey(m => m.SenderId);
                e.HasOne<User>().WithMany().HasForeignKey(m => m.ReceiverId);
            });

            modelBuilder.Entity<LogEntry>(e =>
            {
                e.HasIndex(l => l.UserId);
                e.HasOne<User>().WithMany().HasForeignKey(l => l.UserId);
            });
        }
    }
}