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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DependentDocument>()
                .Property(d => d.DocType)
                .HasConversion<string>(); // Save Enum as string in DB
                
            modelBuilder.Entity<DependentDocument>()
                .HasOne(d => d.Dependent)
                .WithMany(d => d.Documents)
                .HasForeignKey(d => d.DependentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
