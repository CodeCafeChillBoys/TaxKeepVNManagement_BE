using Microsoft.EntityFrameworkCore;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;

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
        public DbSet<Income> Incomes { get; set; }

        // Tax Settlement
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        public DbSet<TaxSettlementDossier> TaxSettlementDossiers { get; set; }
        public DbSet<TaxSettlementIncomeItem> TaxSettlementIncomeItems { get; set; }

        // RBAC Roles
        public DbSet<Role> Roles { get; set; }

        // Expert Registration & Management
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<ConsultationFeeConfiguration> ConsultationFeeConfigurations { get; set; }
        public DbSet<ExpertApplication> ExpertApplications { get; set; }
        public DbSet<ExpertApplicationSpecialization> ExpertApplicationSpecializations { get; set; }
        public DbSet<ExpertApplicationCertificate> ExpertApplicationCertificates { get; set; }
        public DbSet<ExpertApplicationFeeProposal> ExpertApplicationFeeProposals { get; set; }
        public DbSet<ExpertApplicationAudit> ExpertApplicationAudits { get; set; }
        public DbSet<ExpertProfile> ExpertProfiles { get; set; }
        public DbSet<ExpertSlot> ExpertSlots { get; set; }
        public DbSet<ExpertReview> ExpertReviews { get; set; }

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
                entity.Property(u => u.RoleId).HasColumnName("role_id").HasDefaultValue(1);
                entity.Property(u => u.UserRole).HasColumnName("user_role").HasDefaultValue("taxpayer");
                entity.Property(u => u.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
                entity.Property(u => u.CreatedAt).HasColumnName("created_at");
                entity.Property(u => u.UpdatedAt).HasColumnName("updated_at");
                entity.Property(u => u.DateOfBirth).HasColumnName("date_of_birth");
                entity.Property(u => u.Address).HasColumnName("address");
                entity.Property(u => u.Status).HasColumnName("status").HasDefaultValue("active");

                entity.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(u => u.RoleId).HasDatabaseName("idx_users_role_id");

                // Unique indexes
                entity.HasIndex(u => u.CitizenId).IsUnique().HasDatabaseName("idx_users_citizen_id");
                entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("idx_users_email");
            });

            // ── Role ────────────────────────────────────────────────────────────
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Id).HasColumnName("id").UseIdentityByDefaultColumn();
                entity.Property(r => r.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
                entity.Property(r => r.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(r => r.Description).HasColumnName("description");
                entity.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(r => r.CreatedAt).HasColumnName("created_at");

                entity.HasIndex(r => r.Code).IsUnique().HasDatabaseName("idx_roles_code");

                entity.HasData(
                    new Role { Id = 1, Code = "taxpayer", Name = "Người nộp thuế", Description = "Người dùng cá nhân thực hiện quyết toán, kê khai thuế", IsActive = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                    new Role { Id = 2, Code = "admin", Name = "Quản trị viên hệ thống", Description = "Quản trị viên quản lý toàn bộ hệ thống, thẩm định hồ sơ", IsActive = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                    new Role { Id = 3, Code = "expert", Name = "Chuyên gia tư vấn thuế", Description = "Chuyên gia đã được phê duyệt, cung cấp dịch vụ tư vấn thuế", IsActive = true, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }
                );
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
                entity.Property(t => t.Description).HasColumnName("description");

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

            // ── Income ───────────────────────────────────────────────────────────
            modelBuilder.Entity<Income>(entity =>
            {
                entity.ToTable("incomes");
                entity.HasKey(i => i.Id);
                entity.HasIndex(i => i.UserId).HasDatabaseName("idx_incomes_user_id");
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

            // ── SystemConfig ─────────────────────────────────────────────────────
            modelBuilder.Entity<SystemConfig>(entity =>
            {
                entity.ToTable("system_configs");
                entity.HasKey(c => c.ConfigId);
                entity.Property(c => c.ConfigId).HasColumnName("config_id");
                entity.Property(c => c.ConfigKey).HasColumnName("config_key").HasMaxLength(100).IsRequired();
                entity.Property(c => c.ConfigValue).HasColumnName("config_value").IsRequired();
                entity.Property(c => c.Description).HasColumnName("description");
                entity.Property(c => c.AppliesFromYear).HasColumnName("applies_from_year").IsRequired(false);
                entity.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(c => c.CreatedAt).HasColumnName("created_at");
                entity.Property(c => c.UpdatedAt).HasColumnName("updated_at");
                // Unique: (config_key, applies_from_year) — cho phép cùng key tồn tại nhiều năm khác nhau
                entity.HasIndex(c => new { c.ConfigKey, c.AppliesFromYear })
                      .IsUnique()
                      .HasDatabaseName("uq_system_configs_key_year");
            });

            // ── TaxSettlementDossier ─────────────────────────────────────────────
            modelBuilder.Entity<TaxSettlementDossier>(entity =>
            {
                entity.ToTable("tax_settlement_dossiers");
                entity.HasKey(d => d.Id);
                entity.HasMany(d => d.IncomeItems)
                    .WithOne(i => i.Dossier)
                    .HasForeignKey(i => i.DossierId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(d => new { d.TaxpayerId, d.TaxYear })
                    .HasDatabaseName("idx_settlement_taxpayer_year");
            });

            // ── TaxSettlementIncomeItem ──────────────────────────────────────────
            modelBuilder.Entity<TaxSettlementIncomeItem>(entity =>
            {
                entity.ToTable("tax_settlement_income_items");
                entity.HasKey(i => i.Id);
                entity.Property(i => i.IsSelected).HasDefaultValue(true);
            });

            // ── Specialization ──────────────────────────────────────────────────
            modelBuilder.Entity<Specialization>(entity =>
            {
                entity.ToTable("specializations");
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Id).HasColumnName("id").UseIdentityByDefaultColumn();
                entity.Property(s => s.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
                entity.Property(s => s.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
                entity.Property(s => s.Description).HasColumnName("description");
                entity.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true);

                entity.HasIndex(s => s.Code).IsUnique().HasDatabaseName("idx_specializations_code");

                entity.HasData(
                    new Specialization { Id = 1, Code = "PIT", Name = "Thuế thu nhập cá nhân (TNCN)", IsActive = true },
                    new Specialization { Id = 2, Code = "CIT", Name = "Thuế thu nhập doanh nghiệp (TNDN)", IsActive = true },
                    new Specialization { Id = 3, Code = "FINALIZATION", Name = "Quyết toán thuế", IsActive = true },
                    new Specialization { Id = 4, Code = "TAX_REFUND", Name = "Hoàn thuế", IsActive = true },
                    new Specialization { Id = 5, Code = "INTERNAL_ACCOUNTING", Name = "Kế toán nội bộ", IsActive = true },
                    new Specialization { Id = 6, Code = "TRANSFER_PRICING", Name = "Chuyển giá", IsActive = true },
                    new Specialization { Id = 7, Code = "TAX_AGENT", Name = "Đại lý thuế", IsActive = true },
                    new Specialization { Id = 8, Code = "CORPORATE_TAX_LEGAL", Name = "Pháp lý thuế doanh nghiệp", IsActive = true }
                );
            });

            // ── ConsultationFeeConfiguration ────────────────────────────────────
            modelBuilder.Entity<ConsultationFeeConfiguration>(entity =>
            {
                entity.ToTable("consultation_fee_configurations");
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Id).HasColumnName("id").UseIdentityByDefaultColumn();
                entity.Property(c => c.SessionType)
                    .HasColumnName("session_type")
                    .HasMaxLength(50)
                    .HasConversion<string>()
                    .IsRequired();
                entity.Property(c => c.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
                entity.Property(c => c.MinFee).HasColumnName("min_fee").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(c => c.MaxFee).HasColumnName("max_fee").HasColumnType("numeric(18,2)").IsRequired();
                entity.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);

                entity.HasIndex(c => new { c.SessionType, c.DurationMinutes })
                    .IsUnique()
                    .HasDatabaseName("uq_consultation_fee_type_duration");

                entity.HasData(
                    new ConsultationFeeConfiguration { Id = 1, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 30, MinFee = 100000m, MaxFee = 1000000m, IsActive = true },
                    new ConsultationFeeConfiguration { Id = 2, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, MinFee = 200000m, MaxFee = 2000000m, IsActive = true }
                );
            });

            // ── ExpertApplication ───────────────────────────────────────────────
            modelBuilder.Entity<ExpertApplication>(entity =>
            {
                entity.ToTable("expert_applications");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Id).HasColumnName("id");
                entity.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(a => a.ApplicationNumber).HasColumnName("application_number").HasMaxLength(30).IsRequired();
                entity.Property(a => a.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
                entity.Property(a => a.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
                entity.Property(a => a.JobTitle).HasColumnName("job_title").HasMaxLength(255).IsRequired();
                entity.Property(a => a.CompanyName).HasColumnName("company_name").HasMaxLength(255);
                entity.Property(a => a.Bio).HasColumnName("bio");
                entity.Property(a => a.YearsOfExperience).HasColumnName("years_of_experience").HasDefaultValue(0);
                entity.Property(a => a.CurrentPosition).HasColumnName("current_position").HasMaxLength(255);
                entity.Property(a => a.ExperienceDescription).HasColumnName("experience_description").IsRequired();
                entity.Property(a => a.Status).HasColumnName("status").HasMaxLength(30)
                    .HasConversion<string>()
                    .HasDefaultValue(ExpertApplicationStatus.Draft)
                    .IsRequired();
                entity.Property(a => a.SubmittedAt).HasColumnName("submitted_at");
                entity.Property(a => a.ReviewedBy).HasColumnName("reviewed_by");
                entity.Property(a => a.ReviewedAt).HasColumnName("reviewed_at");
                entity.Property(a => a.RejectionReason).HasColumnName("rejection_reason");
                entity.Property(a => a.SupplementRequestReason).HasColumnName("supplement_request_reason");
                entity.Property(a => a.CreatedAt).HasColumnName("created_at");
                entity.Property(a => a.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(a => a.User)
                    .WithMany(u => u.ExpertApplications)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Reviewer)
                    .WithMany()
                    .HasForeignKey(a => a.ReviewedBy)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(a => a.ApplicationNumber).IsUnique().HasDatabaseName("uq_expert_applications_number");
                entity.HasIndex(a => a.UserId).HasDatabaseName("idx_expert_applications_user_id");
                entity.HasIndex(a => a.Status).HasDatabaseName("idx_expert_applications_status");

                // BR-02: Partial unique index đảm bảo mỗi user chỉ có tối đa 1 hồ sơ PendingReview hoặc NeedSupplement
                entity.HasIndex(a => a.UserId)
                    .HasFilter("status IN ('PendingReview', 'NeedSupplement')")
                    .IsUnique()
                    .HasDatabaseName("uq_user_active_pending_application");
            });

            // ── ExpertApplicationSpecialization ─────────────────────────────────
            modelBuilder.Entity<ExpertApplicationSpecialization>(entity =>
            {
                entity.ToTable("expert_application_specializations");
                entity.HasKey(s => new { s.ApplicationId, s.SpecializationId });

                entity.Property(s => s.ApplicationId).HasColumnName("application_id");
                entity.Property(s => s.SpecializationId).HasColumnName("specialization_id");

                entity.HasOne(s => s.Application)
                    .WithMany(a => a.ApplicationSpecializations)
                    .HasForeignKey(s => s.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Specialization)
                    .WithMany(sp => sp.ApplicationSpecializations)
                    .HasForeignKey(s => s.SpecializationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ExpertApplicationCertificate ────────────────────────────────────
            modelBuilder.Entity<ExpertApplicationCertificate>(entity =>
            {
                entity.ToTable("expert_application_certificates");
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Id).HasColumnName("id");
                entity.Property(c => c.ApplicationId).HasColumnName("application_id").IsRequired();
                entity.Property(c => c.CertificateType).HasColumnName("certificate_type").HasMaxLength(50).IsRequired();
                entity.Property(c => c.CertificateName).HasColumnName("certificate_name").HasMaxLength(255).IsRequired();
                entity.Property(c => c.CertificateNumber).HasColumnName("certificate_number").HasMaxLength(100).IsRequired();
                entity.Property(c => c.IssuingAuthority).HasColumnName("issuing_authority").HasMaxLength(255).IsRequired();
                entity.Property(c => c.IssueDate).HasColumnName("issue_date").IsRequired();
                entity.Property(c => c.ExpiryDate).HasColumnName("expiry_date");
                entity.Property(c => c.HasExpiry).HasColumnName("has_expiry").HasDefaultValue(false);
                entity.Property(c => c.FileUrl).HasColumnName("file_url").HasMaxLength(500).IsRequired();
                entity.Property(c => c.FileName).HasColumnName("file_name").HasMaxLength(255);
                entity.Property(c => c.FileMimeType).HasColumnName("file_mime_type").HasMaxLength(100);
                entity.Property(c => c.VerificationStatus).HasColumnName("verification_status").HasMaxLength(30)
                    .HasConversion<string>()
                    .HasDefaultValue(CertificateVerificationStatus.PendingVerification)
                    .IsRequired();
                entity.Property(c => c.VerificationSource).HasColumnName("verification_source").HasMaxLength(255);
                entity.Property(c => c.VerificationNote).HasColumnName("verification_note");
                entity.Property(c => c.VerifiedBy).HasColumnName("verified_by");
                entity.Property(c => c.VerifiedAt).HasColumnName("verified_at");
                entity.Property(c => c.CreatedAt).HasColumnName("created_at");

                entity.HasOne(c => c.Application)
                    .WithMany(a => a.Certificates)
                    .HasForeignKey(c => c.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Verifier)
                    .WithMany()
                    .HasForeignKey(c => c.VerifiedBy)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(c => c.ApplicationId).HasDatabaseName("idx_expert_certs_application_id");
            });

            // ── ExpertApplicationFeeProposal ────────────────────────────────────
            modelBuilder.Entity<ExpertApplicationFeeProposal>(entity =>
            {
                entity.ToTable("expert_application_fee_proposals");
                entity.HasKey(f => f.Id);

                entity.Property(f => f.Id).HasColumnName("id");
                entity.Property(f => f.ApplicationId).HasColumnName("application_id").IsRequired();
                entity.Property(f => f.SessionType)
                    .HasColumnName("session_type")
                    .HasMaxLength(50)
                    .HasConversion<string>()
                    .IsRequired();
                entity.Property(f => f.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
                entity.Property(f => f.ProposedFee).HasColumnName("proposed_fee").HasColumnType("numeric(18,2)").IsRequired();

                entity.HasOne(f => f.Application)
                    .WithMany(a => a.FeeProposals)
                    .HasForeignKey(f => f.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(f => new { f.ApplicationId, f.SessionType, f.DurationMinutes })
                    .IsUnique()
                    .HasDatabaseName("uq_application_fee_proposal");
            });

            // ── ExpertApplicationAudit ──────────────────────────────────────────
            modelBuilder.Entity<ExpertApplicationAudit>(entity =>
            {
                entity.ToTable("expert_application_audits");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Id).HasColumnName("id");
                entity.Property(a => a.ApplicationId).HasColumnName("application_id").IsRequired();
                entity.Property(a => a.ActorId).HasColumnName("actor_id").IsRequired();
                entity.Property(a => a.Action).HasColumnName("action").HasMaxLength(50)
                    .HasConversion<string>()
                    .IsRequired();
                entity.Property(a => a.FromStatus).HasColumnName("from_status").HasMaxLength(30)
                    .HasConversion<string>();
                entity.Property(a => a.ToStatus).HasColumnName("to_status").HasMaxLength(30)
                    .HasConversion<string>()
                    .IsRequired();
                entity.Property(a => a.Notes).HasColumnName("notes");
                entity.Property(a => a.CreatedAt).HasColumnName("created_at");

                entity.HasOne(a => a.Application)
                    .WithMany(app => app.Audits)
                    .HasForeignKey(a => a.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Actor)
                    .WithMany()
                    .HasForeignKey(a => a.ActorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => a.ApplicationId).HasDatabaseName("idx_expert_audits_application_id");
            });

            // ── ExpertProfile ───────────────────────────────────────────────────
            modelBuilder.Entity<ExpertProfile>(entity =>
            {
                entity.ToTable("expert_profiles");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Id).HasColumnName("id");
                entity.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(p => p.LatestApplicationId).HasColumnName("latest_application_id").IsRequired();
                entity.Property(p => p.JobTitle).HasColumnName("job_title").HasMaxLength(255).IsRequired();
                entity.Property(p => p.CompanyName).HasColumnName("company_name").HasMaxLength(255);
                entity.Property(p => p.Bio).HasColumnName("bio");
                entity.Property(p => p.YearsOfExperience).HasColumnName("years_of_experience").HasDefaultValue(0);
                entity.Property(p => p.Rating).HasColumnName("rating").HasColumnType("numeric(3,2)").HasDefaultValue(0m);
                entity.Property(p => p.TotalReviews).HasColumnName("total_reviews").HasDefaultValue(0);
                entity.Property(p => p.CompletedConsultationsCount).HasColumnName("completed_consultations_count").HasDefaultValue(0);
                entity.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(p => p.ApprovedAt).HasColumnName("approved_at");
                entity.Property(p => p.CreatedAt).HasColumnName("created_at");
                entity.Property(p => p.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(p => p.User)
                    .WithOne(u => u.ExpertProfile)
                    .HasForeignKey<ExpertProfile>(p => p.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.LatestApplication)
                    .WithMany()
                    .HasForeignKey(p => p.LatestApplicationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("uq_expert_profiles_user_id");
            });

            // ── ExpertSlot ──────────────────────────────────────────────────────
            modelBuilder.Entity<ExpertSlot>(entity =>
            {
                entity.ToTable("expert_slots");
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Id).HasColumnName("id");
                entity.Property(s => s.ExpertProfileId).HasColumnName("expert_profile_id").IsRequired();
                entity.Property(s => s.SlotDate).HasColumnName("slot_date").IsRequired();
                entity.Property(s => s.StartTime).HasColumnName("start_time").IsRequired();
                entity.Property(s => s.EndTime).HasColumnName("end_time").IsRequired();
                entity.Property(s => s.SessionType)
                    .HasColumnName("session_type")
                    .HasMaxLength(50)
                    .HasConversion<string>()
                    .HasDefaultValue(SessionType.ONLINE_MEETING)
                    .IsRequired();
                entity.Property(s => s.IsBooked).HasColumnName("is_booked").HasDefaultValue(false);
                entity.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(s => s.CreatedAt).HasColumnName("created_at");
                entity.Property(s => s.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(s => s.ExpertProfile)
                    .WithMany(p => p.Slots)
                    .HasForeignKey(s => s.ExpertProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(s => s.ExpertProfileId).HasDatabaseName("idx_expert_slots_profile_id");
                entity.HasIndex(s => s.SlotDate).HasDatabaseName("idx_expert_slots_slot_date");
                entity.HasIndex(s => new { s.ExpertProfileId, s.SlotDate, s.StartTime }).IsUnique().HasDatabaseName("uq_expert_slots_profile_datetime");
            });

            // ── ExpertReview ────────────────────────────────────────────────────
            modelBuilder.Entity<ExpertReview>(entity =>
            {
                entity.ToTable("expert_reviews");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Id).HasColumnName("id");
                entity.Property(r => r.ExpertProfileId).HasColumnName("expert_profile_id").IsRequired();
                entity.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(r => r.BookingId).HasColumnName("booking_id");
                entity.Property(r => r.Rating).HasColumnName("rating").IsRequired();
                entity.Property(r => r.Comment).HasColumnName("comment");
                entity.Property(r => r.IsAnonymous).HasColumnName("is_anonymous").HasDefaultValue(false);
                entity.Property(r => r.IsPublished).HasColumnName("is_published").HasDefaultValue(true);
                entity.Property(r => r.CreatedAt).HasColumnName("created_at");
                entity.Property(r => r.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(r => r.ExpertProfile)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(r => r.ExpertProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                    .WithMany(u => u.ExpertReviews)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.ExpertProfileId).HasDatabaseName("idx_expert_reviews_profile_id");
                entity.HasIndex(r => r.UserId).HasDatabaseName("idx_expert_reviews_user_id");
            });
        }
    }
}