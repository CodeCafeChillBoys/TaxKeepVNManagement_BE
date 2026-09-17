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
        public DbSet<DependentDocumentRule> DependentDocumentRules { get; set; }

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

                // Standardized snake_case column names
                entity.Property(d => d.CurrentGroup).HasColumnName("current_group")
                    .HasConversion<string>();
                entity.Property(d => d.BirthDate).HasColumnName("birth_date");
                entity.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(d => d.IsProfileComplete).HasColumnName("is_profile_complete").HasDefaultValue(false);

                // Foreign key relationship to User
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(d => d.TaxpayerId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Index để query overlap nhanh theo CitizenId và BirthCertNumber
                entity.HasIndex(d => d.CitizenId).HasDatabaseName("idx_dependents_citizen_id");
                entity.HasIndex(d => d.BirthCertNumber).HasDatabaseName("idx_dependents_birth_cert");
                entity.HasIndex(d => d.TaxpayerId).HasDatabaseName("idx_dependents_taxpayer_id");
            });

            // ── DependentDocument ────────────────────────────────────────────────
            modelBuilder.Entity<DependentDocument>(entity =>
            {
                entity.ToTable("dependent_documents");
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Id).HasColumnName("id");
                entity.Property(d => d.DependentId).HasColumnName("dependent_id");
                entity.Property(d => d.DocType).HasColumnName("doc_type").HasConversion<string>().IsRequired();
                entity.Property(d => d.FileUrl).HasColumnName("file_url").IsRequired();
                entity.Property(d => d.FileMimeType).HasColumnName("file_mime_type").IsRequired();
                entity.Property(d => d.IsReadable).HasColumnName("is_readable");
                entity.Property(d => d.UploadedAt).HasColumnName("uploaded_at");

                entity.HasOne(d => d.Dependent)
                    .WithMany(d => d.Documents)
                    .HasForeignKey(d => d.DependentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── IncomeSource ─────────────────────────────────────────────────────
            modelBuilder.Entity<IncomeSource>(entity =>
            {
                entity.ToTable("income_sources");
                entity.HasKey(i => i.Id);

                entity.Property(i => i.Id).HasColumnName("id");
                entity.Property(i => i.TaxpayerId).HasColumnName("taxpayer_id");
                entity.Property(i => i.CompanyName).HasColumnName("company_name").HasMaxLength(255).IsRequired();
                entity.Property(i => i.CompanyTaxCode).HasColumnName("company_tax_code").HasMaxLength(20).IsRequired();
                entity.Property(i => i.TaxYear).HasColumnName("tax_year");
                entity.Property(i => i.TotalIncome).HasColumnName("total_income").HasColumnType("decimal(18,2)");
                entity.Property(i => i.TaxWithheld).HasColumnName("tax_withheld").HasColumnType("decimal(18,2)");
                entity.Property(i => i.IsActive).HasColumnName("is_active");
                entity.Property(i => i.CreatedAt).HasColumnName("created_at");
                entity.Property(i => i.UpdatedAt).HasColumnName("updated_at");

                // Foreign key relationship to User
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(i => i.TaxpayerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── SystemNotification ───────────────────────────────────────────────
            modelBuilder.Entity<SystemNotification>(entity =>
            {
                entity.ToTable("system_notifications");
                entity.HasKey(n => n.NotificationId);

                entity.Property(n => n.NotificationId).HasColumnName("notification_id");
                entity.Property(n => n.UserId).HasColumnName("user_id");
                entity.Property(n => n.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
                entity.Property(n => n.Message).HasColumnName("message").IsRequired();
                entity.Property(n => n.NotificationType).HasColumnName("notification_type").HasMaxLength(100).IsRequired();
                entity.Property(n => n.IsRead).HasColumnName("is_read");
                entity.Property(n => n.TargetActionUrl).HasColumnName("target_action_url");
                entity.Property(n => n.CreatedAt).HasColumnName("created_at");

                // Foreign key relationship to User
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── DependentDocumentRule ────────────────────────────────────────────
            modelBuilder.Entity<DependentDocumentRule>(entity =>
            {
                entity.ToTable("dependent_document_rules");
                entity.HasKey(r => r.RuleId);

                entity.Property(r => r.RuleId).HasColumnName("rule_id");
                entity.Property(r => r.TargetGroup).HasColumnName("target_group").HasMaxLength(50).IsRequired();
                entity.Property(r => r.DocType).HasColumnName("doc_type").HasMaxLength(50).IsRequired();
                entity.Property(r => r.IsMandatory).HasColumnName("is_mandatory").HasDefaultValue(true);
                entity.Property(r => r.Description).HasColumnName("description");
                entity.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(r => r.CreatedAt).HasColumnName("created_at");
                entity.Property(r => r.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(r => new { r.TargetGroup, r.DocType })
                    .IsUnique()
                    .HasDatabaseName("uq_group_doc_rule");
            });
        }
    }
}
