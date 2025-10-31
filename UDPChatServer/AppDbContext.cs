using Microsoft.EntityFrameworkCore;
using UdpChatServer.Models;

namespace UdpChatServer
{
    public class AppDbContext : DbContext
    {
        // Таблицы базы данных
        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<LogEntry> LogEntries { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<BlacklistEntry> BlacklistEntries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Строка подключения к локальной базе данных SQL Server
            optionsBuilder.UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=UdpChatDb;Trusted_Connection=True;");
        }
    }
}