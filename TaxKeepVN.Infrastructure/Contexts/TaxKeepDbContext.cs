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
        public DbSet<TaxPeriod> TaxPeriods { get; set; }
        public DbSet<TaxDocumentType> DocumentTypes { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentItem> DocumentItems { get; set; }

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

                // Cột trong database PostgreSQL sử dụng quy ước snake_case
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
                entity.ToTable("dependent_documents");
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Id).HasColumnName("id");
                entity.Property(d => d.DependentId).HasColumnName("dependent_id").IsRequired();
                entity.Property(d => d.DocType).HasColumnName("doc_type").HasConversion<string>().IsRequired();
                entity.Property(d => d.FileMimeType).HasColumnName("file_mime_type").IsRequired();
                entity.Property(d => d.FileUrl).HasColumnName("file_url").IsRequired();
                entity.Property(d => d.IsReadable).HasColumnName("is_readable");
                entity.Property(d => d.UploadedAt).HasColumnName("uploaded_at");

                entity.HasOne(d => d.Dependent)
                    .WithMany(d => d.Documents)
                    .HasForeignKey(d => d.DependentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(d => d.DependentId).HasDatabaseName("idx_dependent_documents_dependent_id");
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

            // ── TaxPeriod ────────────────────────────────────────────────────────
            modelBuilder.Entity<TaxPeriod>(entity =>
            {
                entity.ToTable("tax_periods");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Id).HasColumnName("id");
                entity.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(p => p.TaxYear).HasColumnName("tax_year").IsRequired();
                entity.Property(p => p.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("DRAFT").IsRequired();
                entity.Property(p => p.CreatedAt).HasColumnName("created_at");
                entity.Property(p => p.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(p => p.User)
                    .WithMany(u => u.TaxPeriods)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.UserId).HasDatabaseName("idx_tax_periods_user_id");
                entity.HasIndex(p => new { p.UserId, p.TaxYear })
                    .IsUnique()
                    .HasDatabaseName("uq_tax_periods_user_year");
            });

            // ── TaxDocumentType ──────────────────────────────────────────────────
            modelBuilder.Entity<TaxDocumentType>(entity =>
            {
                entity.ToTable("document_types");
                entity.HasKey(t => t.Code);

                entity.Property(t => t.Code).HasColumnName("code").HasMaxLength(50);
                entity.Property(t => t.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
                entity.Property(t => t.IsTaxEligible).HasColumnName("is_tax_eligible").HasDefaultValue(true);

                // Seed data theo sơ đồ ERD
                entity.HasData(
                    new TaxDocumentType { Code = "SALES_INVOICE", Name = "Hóa đơn bán hàng", IsTaxEligible = true },
                    new TaxDocumentType { Code = "VAT_INVOICE", Name = "Hóa đơn GTGT", IsTaxEligible = true },
                    new TaxDocumentType { Code = "WITHHOLDING_VOUCHER", Name = "Chứng từ khấu trừ thuế TNCN", IsTaxEligible = true }
                );
            });

            // ── Document ─────────────────────────────────────────────────────────
            // Trong method OnModelCreating:
            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("documents");
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Id).HasColumnName("id");
                entity.Property(d => d.PeriodId).HasColumnName("period_id").IsRequired();
                entity.Property(d => d.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(50).IsRequired(false);
                entity.Property(d => d.FileUrl).HasColumnName("file_url").IsRequired();
                entity.Property(d => d.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255);
                entity.Property(d => d.InvoiceSeries).HasColumnName("invoice_series").HasMaxLength(50);
                entity.Property(d => d.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(50);
                entity.Property(d => d.InvoiceDate).HasColumnName("invoice_date");
                entity.Property(d => d.SellerName).HasColumnName("seller_name").HasMaxLength(255);
                entity.Property(d => d.SellerTaxCode).HasColumnName("seller_tax_code").HasMaxLength(50);
                entity.Property(d => d.SellerAddress).HasColumnName("seller_address");
                entity.Property(d => d.SellerPhone).HasColumnName("seller_phone").HasMaxLength(50);
                entity.Property(d => d.BuyerName).HasColumnName("buyer_name").HasMaxLength(255);
                entity.Property(d => d.BuyerTaxCode).HasColumnName("buyer_tax_code").HasMaxLength(50);
                entity.Property(d => d.BuyerIdCard).HasColumnName("buyer_id_card").HasMaxLength(50);
                entity.Property(d => d.BuyerAddress).HasColumnName("buyer_address");
                entity.Property(d => d.PaymentMethod).HasColumnName("payment_method").HasMaxLength(100);
                entity.Property(d => d.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)");
                entity.Property(d => d.TotalAmountInWords).HasColumnName("total_amount_in_words");
                entity.Property(d => d.LookupUrl).HasColumnName("lookup_url");
                entity.Property(d => d.LookupCode).HasColumnName("lookup_code").HasMaxLength(100);
                entity.Property(d => d.ExtractedYear).HasColumnName("extracted_year");
                entity.Property(d => d.IsYearValid).HasColumnName("is_year_valid");
                entity.Property(d => d.IsIdentityValid).HasColumnName("is_identity_valid");
                entity.Property(d => d.IsNotReimbursed).HasColumnName("is_not_reimbursed").HasDefaultValue(false);
                entity.Property(d => d.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("UPLOADED").IsRequired();
                entity.Property(d => d.CreatedAt).HasColumnName("created_at");

                entity.HasOne(d => d.Period)
                    .WithMany(p => p.Documents)
                    .HasForeignKey(d => d.PeriodId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.DocType)
                    .WithMany(t => t.Documents)
                    .HasForeignKey(d => d.DocTypeCode)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => d.PeriodId).HasDatabaseName("idx_documents_period_id");
                entity.HasIndex(d => d.DocTypeCode).HasDatabaseName("idx_documents_doc_type_code");
            });

            // ── DocumentItem ─────────────────────────────────────────────────────
            modelBuilder.Entity<DocumentItem>(entity =>
            {
                entity.ToTable("document_items");
                entity.HasKey(i => i.Id);

                entity.Property(i => i.Id).HasColumnName("id").UseIdentityByDefaultColumn();
                entity.Property(i => i.DocumentId).HasColumnName("document_id").IsRequired();
                entity.Property(i => i.ItemOrder).HasColumnName("item_order").IsRequired();
                entity.Property(i => i.ItemName).HasColumnName("item_name").HasMaxLength(500).IsRequired();
                entity.Property(i => i.Unit).HasColumnName("unit").HasMaxLength(50);
                entity.Property(i => i.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)").HasDefaultValue(0m);
                entity.Property(i => i.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
                entity.Property(i => i.TotalPrice).HasColumnName("total_price").HasColumnType("numeric(18,2)").HasDefaultValue(0m);

                entity.HasOne(i => i.Document)
                    .WithMany(d => d.Items)
                    .HasForeignKey(i => i.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => i.DocumentId).HasDatabaseName("idx_document_items_document_id");
            });
        }
    }
}