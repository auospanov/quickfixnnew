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
        public DbSet<NewOrders> newOrders { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            
            //optionsBuilder.UseSqlServer("Data Source=WIN-DUS0A072PNF\\SQLEXPRESS;Initial Catalog=fixdb;Persist Security Info=True;User ID=platformAdm;Password=Admin$12345; Encrypt=False;Trust Server Certificate=True");
            optionsBuilder.UseSqlServer(Program.GetValueByKey(Program.cfg, "ConnectionString"));
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
        }
    }
}
