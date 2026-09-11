using Microsoft.EntityFrameworkCore;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Infrastructure.Contexts
{
    public class TaxKeepDbContext : DbContext
    {
        public TaxKeepDbContext(DbContextOptions<TaxKeepDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RevokedToken> RevokedTokens { get; set; }
        public DbSet<Dependent> Dependents { get; set; }
        public DbSet<DependentDocument> DependentDocuments { get; set; }
        public DbSet<SystemNotification> SystemNotifications { get; set; }
        public DbSet<IncomeSource> IncomeSources { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ────────────────────────────────────────────────────────────
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(u => u.UserId);

                entity.Property(u => u.UserId).HasColumnName("user_id");
                entity.Property(u => u.TaxIdNumber).HasColumnName("tax_id_number");
                entity.Property(u => u.CitizenId).HasColumnName("citizen_id").IsRequired();
                entity.Property(u => u.FullName).HasColumnName("full_name").IsRequired();
                entity.Property(u => u.Email).HasColumnName("email").IsRequired();
                entity.Property(u => u.PhoneNumber).HasColumnName("phone_number");
                entity.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
                entity.Property(u => u.UserRole).HasColumnName("user_role").HasDefaultValue("taxpayer");
                entity.Property(u => u.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
                entity.Property(u => u.CreatedAt).HasColumnName("created_at");
                entity.Property(u => u.UpdatedAt).HasColumnName("updated_at");
                entity.Property(u => u.DateOfBirth).HasColumnName("date_of_birth");
                entity.Property(u => u.Address).HasColumnName("address");
                entity.Property(u => u.Status).HasColumnName("status").HasDefaultValue("active");

                // Unique indexes
                entity.HasIndex(u => u.CitizenId).IsUnique().HasDatabaseName("idx_users_citizen_id");
                entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("idx_users_email");
            });

            // ── RevokedToken ─────────────────────────────────────────────────────
            modelBuilder.Entity<RevokedToken>(entity =>
            {
                entity.ToTable("revoked_tokens");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Jti).HasColumnName("jti").IsRequired();
                entity.Property(t => t.ExpiresAt).HasColumnName("expires_at");
                entity.Property(t => t.RevokedAt).HasColumnName("revoked_at");

                // Index để truy vấn nhanh theo JTI
                entity.HasIndex(t => t.Jti).HasDatabaseName("idx_revoked_tokens_jti");
            });

            // ── Dependent ────────────────────────────────────────────────────────
            modelBuilder.Entity<Dependent>(entity =>
            {
                entity.ToTable("dependents");
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Id).HasColumnName("id");
                entity.Property(d => d.TaxpayerId).HasColumnName("taxpayer_id");
                entity.Property(d => d.FullName).HasColumnName("full_name").IsRequired();
                entity.Property(d => d.Relationship).HasColumnName("relationship")
                    .HasConversion<string>().IsRequired();
                entity.Property(d => d.DateOfBirth).HasColumnName("date_of_birth");
                entity.Property(d => d.CitizenId).HasColumnName("citizen_id");
                entity.Property(d => d.BirthCertNumber).HasColumnName("birth_cert_number");
                entity.Property(d => d.TaxIdNumber).HasColumnName("tax_id_number");
                entity.Property(d => d.EffectiveFromMonth).HasColumnName("effective_from_month").IsRequired();
                entity.Property(d => d.EffectiveToMonth).HasColumnName("effective_to_month").IsRequired();
                entity.Property(d => d.Note).HasColumnName("note");
                entity.Property(d => d.Status).HasColumnName("status")
                    .HasConversion<string>().HasDefaultValue(TaxKeepVN.Domain.Enums.DependentStatus.PENDING_DOCUMENTS);
                entity.Property(d => d.CreatedAt).HasColumnName("created_at");
                entity.Property(d => d.UpdatedAt).HasColumnName("updated_at");

                // Our fields
                entity.Property(d => d.CurrentGroup).HasColumnName("current_group")
                    .HasConversion<string>();
                entity.Property(d => d.BirthDate).HasColumnName("birth_date");
                entity.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(d => d.IsProfileComplete).HasColumnName("is_profile_complete").HasDefaultValue(false);

                // Index để query overlap nhanh theo CitizenId và BirthCertNumber
                entity.HasIndex(d => d.CitizenId).HasDatabaseName("idx_dependents_citizen_id");
                entity.HasIndex(d => d.BirthCertNumber).HasDatabaseName("idx_dependents_birth_cert");
                entity.HasIndex(d => d.TaxpayerId).HasDatabaseName("idx_dependents_taxpayer_id");
            });

            // ── DependentDocument ────────────────────────────────────────────────
            modelBuilder.Entity<DependentDocument>(entity =>
            {
                entity.Property(d => d.DocType).HasConversion<string>();

                entity.HasOne(d => d.Dependent)
                    .WithMany(d => d.Documents)
                    .HasForeignKey(d => d.DependentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
