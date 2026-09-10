using Microsoft.EntityFrameworkCore;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Infrastructure.Contexts
{
    public class TaxKeepDbContext : DbContext
    {
        public TaxKeepDbContext(DbContextOptions<TaxKeepDbContext> options) : base(options)
        {
        }

        public DbSet<Dependent> Dependents { get; set; }
        public DbSet<DependentDocument> DependentDocuments { get; set; }
        public DbSet<SystemNotification> SystemNotifications { get; set; }
        public DbSet<IncomeSource> IncomeSources { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DependentDocument>()
                .Property(d => d.DocType)
                .HasConversion<string>();
                
            modelBuilder.Entity<Dependent>()
                .Property(d => d.CurrentGroup)
                .HasConversion<string>();

            modelBuilder.Entity<DependentDocument>()
                .HasOne(d => d.Dependent)
                .WithMany(d => d.Documents)
                .HasForeignKey(d => d.DependentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
