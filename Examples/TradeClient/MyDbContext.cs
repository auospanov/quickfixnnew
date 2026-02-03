using Microsoft.EntityFrameworkCore;
using QuickFix.Fields;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TradeClient
{
    public class MyDbContext : DbContext
    {
        public DbSet<heartbeat> heartbeat { get; set; }

        public DbSet<orders> orders { get; set; }
        public DbSet<quotesSimple> quotesSimple { get; set; }
        public DbSet<instrsView> instrsView { get; set; }
        public DbSet<settingsTP> settingsTP { get; set; }
        public DbSet<NewOrders> NewOrders { get; set; }
        public DbSet<tradeCapture> TradeCapture { get; set; }
        
        // Конструктор для использования с DbContextOptions (рекомендуемый подход)
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
        }

        // Конструктор без параметров для обратной совместимости (будет использоваться только если нет фабрики)
        public MyDbContext() : base()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Используется только если DbContext создается без параметров (для обратной совместимости)
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(Program.GetValueByKey(Program.cfg, "ConnectionString"));
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {            
            modelBuilder.Entity<heartbeat>().ToTable(nameof(heartbeat), t => t.ExcludeFromMigrations());
            modelBuilder.Entity<orders>().ToTable(nameof(orders), t => t.ExcludeFromMigrations());
            modelBuilder.Entity<quotesSimple>().ToTable(nameof(quotesSimple), t => t.ExcludeFromMigrations());
            modelBuilder
           .Entity<instrsView>(entity =>
           {
               entity.ToView("instrsView"); // имя вьюшки в БД
               //entity.HasKey(e => e.Id); // указываем ключ
               entity.HasNoKey();
           });
            modelBuilder.Entity<settingsTP>().ToTable(nameof(settingsTP), t => t.ExcludeFromMigrations());
            modelBuilder
           .Entity<settingsTP>(entity =>
           {
               entity.ToView("settingsTP"); // имя вьюшки в БД
               //entity.HasKey(e => e.Id); // указываем ключ
               entity.HasNoKey();
           });
            modelBuilder.Entity<NewOrders>().ToTable(nameof(NewOrders), t => t.ExcludeFromMigrations());
            modelBuilder.Entity<tradeCapture>().ToTable(nameof(tradeCapture), t => t.ExcludeFromMigrations());
        }
    }
}
