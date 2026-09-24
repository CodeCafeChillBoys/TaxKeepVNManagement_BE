using Microsoft.EntityFrameworkCore;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Entities.Law;

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

        // ── Law System DbSets ────────────────────────────────────────────────
        public DbSet<LegalDocument> LegalDocuments { get; set; }
        public DbSet<LegalDocumentRelation> LegalDocumentRelations { get; set; }
        public DbSet<LawRuleDefinition> LawRuleDefinitions { get; set; }
        public DbSet<LawRevision> LawRevisions { get; set; }
        public DbSet<LawRuleVersion> LawRuleVersions { get; set; }
        public DbSet<LawChangeset> LawChangesets { get; set; }
        public DbSet<LawChangeOp> LawChangeOps { get; set; }
        public DbSet<LawChangesetRelation> LawChangesetRelations { get; set; }
        public DbSet<LawOrphanResolution> LawOrphanResolutions { get; set; }

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

            // ── LegalDocument ────────────────────────────────────────────────────
            modelBuilder.Entity<LegalDocument>(entity =>
            {
                entity.ToTable("legal_documents");
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Id).HasColumnName("id");
                entity.Property(d => d.DocumentNumber).HasColumnName("document_number").HasMaxLength(50);
                entity.Property(d => d.NumberNormalized).HasColumnName("number_normalized").HasMaxLength(50);
                entity.Property(d => d.DocumentType).HasColumnName("document_type").HasMaxLength(20);
                entity.Property(d => d.Title).HasColumnName("title");
                entity.Property(d => d.Issuer).HasColumnName("issuer").HasMaxLength(100);
                entity.Property(d => d.IssuedDate).HasColumnName("issued_date");
                entity.Property(d => d.EffectiveDate).HasColumnName("effective_date");
                entity.Property(d => d.FileUrl).HasColumnName("file_url");
                entity.Property(d => d.OriginalFilename).HasColumnName("original_filename");
                entity.Property(d => d.SourceUrl).HasColumnName("source_url");
                entity.Property(d => d.TotalPages).HasColumnName("total_pages");
                entity.Property(d => d.LegalStatus).HasColumnName("legal_status").HasMaxLength(30).HasDefaultValue("CHUA_RO");
                entity.Property(d => d.LegalStatusNote).HasColumnName("legal_status_note");
                entity.Property(d => d.IsPlaceholder).HasColumnName("is_placeholder").HasDefaultValue(false);
                entity.Property(d => d.CreatedBy).HasColumnName("created_by");
                entity.Property(d => d.CreatedAt).HasColumnName("created_at");
                entity.Property(d => d.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(d => d.NumberNormalized)
                    .IsUnique()
                    .HasFilter("number_normalized IS NOT NULL")
                    .HasDatabaseName("uq_legal_documents_normalized");
            });

            // ── LegalDocumentRelation ────────────────────────────────────────────
            modelBuilder.Entity<LegalDocumentRelation>(entity =>
            {
                entity.ToTable("legal_document_relations");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Id).HasColumnName("id");
                entity.Property(r => r.SourceDocumentId).HasColumnName("source_document_id");
                entity.Property(r => r.TargetDocumentId).HasColumnName("target_document_id");
                entity.Property(r => r.RelationType).HasColumnName("relation_type").HasMaxLength(30).IsRequired();
                entity.Property(r => r.TargetArticle).HasColumnName("target_article").HasMaxLength(50);
                entity.Property(r => r.TargetClause).HasColumnName("target_clause").HasMaxLength(50);
                entity.Property(r => r.TargetPoint).HasColumnName("target_point").HasMaxLength(50);
                entity.Property(r => r.SourceArticle).HasColumnName("source_article").HasMaxLength(50);
                entity.Property(r => r.SourceClause).HasColumnName("source_clause").HasMaxLength(50);
                entity.Property(r => r.SourcePoint).HasColumnName("source_point").HasMaxLength(50);
                entity.Property(r => r.SourcePage).HasColumnName("source_page");
                entity.Property(r => r.EffectiveDate).HasColumnName("effective_date");
                entity.Property(r => r.EvidenceText).HasColumnName("evidence_text");
                entity.Property(r => r.Note).HasColumnName("note");
                entity.Property(r => r.CreatedInRevision).HasColumnName("created_in_revision");

                entity.HasOne(r => r.SourceDocument)
                    .WithMany(d => d.SourceRelations)
                    .HasForeignKey(r => r.SourceDocumentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.TargetDocument)
                    .WithMany(d => d.TargetRelations)
                    .HasForeignKey(r => r.TargetDocumentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Revision)
                    .WithMany(rev => rev.DocumentRelationsCreated)
                    .HasForeignKey(r => r.CreatedInRevision)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.TargetDocumentId).HasDatabaseName("idx_legal_doc_rel_target");
            });

            // ── LawRuleDefinition ────────────────────────────────────────────────
            modelBuilder.Entity<LawRuleDefinition>(entity =>
            {
                entity.ToTable("law_rule_definitions");
                entity.HasKey(d => d.RuleCode);

                entity.Property(d => d.RuleCode).HasColumnName("rule_code").HasMaxLength(50);
                entity.Property(d => d.RuleGroup).HasColumnName("rule_group").HasMaxLength(30).IsRequired();
                entity.Property(d => d.ValueKind).HasColumnName("value_kind").HasMaxLength(20).IsRequired();
                entity.Property(d => d.DefaultUnit).HasColumnName("default_unit").HasMaxLength(20);
                entity.Property(d => d.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
                entity.Property(d => d.Description).HasColumnName("description").IsRequired();
                entity.Property(d => d.RequiredForFlow3).HasColumnName("required_for_flow3").HasDefaultValue(false);
                entity.Property(d => d.RequiredFromTaxYear).HasColumnName("required_from_tax_year");
                entity.Property(d => d.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(d => d.CreatedAt).HasColumnName("created_at");
                entity.Property(d => d.UpdatedAt).HasColumnName("updated_at");

                entity.HasData(
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_TAX_SCHEDULE,
                        RuleGroup = LawConstants.RuleGroup.SCHEDULE,
                        ValueKind = LawConstants.ValueKind.SCHEDULE,
                        DefaultUnit = "VND/year",
                        DisplayName = "Biểu thuế lũy tiến từng phần (tiền lương, tiền công)",
                        Description = "Bậc thuế, ngưỡng thu nhập tính thuế và thuế suất từng bậc",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/month",
                        DisplayName = "Giảm trừ bản thân",
                        Description = "Mức giảm trừ gia cảnh cho chính người nộp thuế mỗi tháng",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/person/month",
                        DisplayName = "Giảm trừ mỗi người phụ thuộc",
                        Description = "Mức giảm trừ cho mỗi người phụ thuộc hợp lệ mỗi tháng",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS,
                        RuleGroup = LawConstants.RuleGroup.DEPENDENT,
                        ValueKind = LawConstants.ValueKind.JSON,
                        DefaultUnit = null,
                        DisplayName = "Nhóm người phụ thuộc và điều kiện",
                        Description = "Nhóm người phụ thuộc hợp lệ, độ tuổi, điều kiện khuyết tật, học tập và thu nhập",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME,
                        RuleGroup = LawConstants.RuleGroup.DEPENDENT,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/month",
                        DisplayName = "Thu nhập bình quân tháng tối đa của người phụ thuộc",
                        Description = "Ngưỡng thu nhập bình quân tháng tối đa để đủ điều kiện làm người phụ thuộc",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/year",
                        DisplayName = "Giảm trừ chi phí y tế",
                        Description = "Mức giảm trừ chi phí khám chữa bệnh hiểm nghèo tối đa trong năm",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = 2026,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_EDUCATION,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/year",
                        DisplayName = "Giảm trừ chi phí giáo dục, đào tạo",
                        Description = "Mức giảm trừ chi phí học tập, đào tạo cho bản thân và người phụ thuộc tối đa trong năm",
                        RequiredForFlow3 = true,
                        RequiredFromTaxYear = 2026,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_MANDATORY_INSURANCE,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.FLAG,
                        DefaultUnit = null,
                        DisplayName = "Trừ bảo hiểm bắt buộc theo thực đóng",
                        Description = "Cho phép trừ các khoản đóng bảo hiểm bắt buộc (BHXH, BHYT, BHTN) theo thực tế phát sinh",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/month",
                        DisplayName = "Trần trừ bảo hiểm hưu trí bổ sung, tự nguyện, nhân thọ",
                        Description = "Mức đóng vào quỹ hưu trí tự nguyện, bảo hiểm bổ sung được trừ tối đa mỗi tháng",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_CHARITY,
                        RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                        ValueKind = LawConstants.ValueKind.FLAG,
                        DefaultUnit = null,
                        DisplayName = "Trừ đóng góp từ thiện, nhân đạo, khuyến học",
                        Description = "Cho phép trừ các khoản đóng góp từ thiện, nhân đạo, khuyến học theo thực tế phát sinh",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_SETTLEMENT_SELF_REQUIRED_IF_MED_EDU,
                        RuleGroup = LawConstants.RuleGroup.SETTLEMENT,
                        ValueKind = LawConstants.ValueKind.FLAG,
                        DefaultUnit = null,
                        DisplayName = "Có giảm trừ y tế, giáo dục thì phải tự quyết toán",
                        Description = "Cờ xác định cá nhân có áp dụng giảm trừ y tế hoặc giáo dục thì bắt buộc phải tự quyết toán trực tiếp",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE,
                        RuleGroup = LawConstants.RuleGroup.WITHHOLDING,
                        ValueKind = LawConstants.ValueKind.RATE,
                        DefaultUnit = "%",
                        DisplayName = "Tỷ lệ khấu trừ lao động vãng lai",
                        Description = "Tỷ lệ khấu trừ thuế TNCN tại nguồn đối với cá nhân không ký HĐLĐ hoặc HĐLĐ dưới 3 tháng",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT,
                        RuleGroup = LawConstants.RuleGroup.WITHHOLDING,
                        ValueKind = LawConstants.ValueKind.AMOUNT,
                        DefaultUnit = "VND/payment",
                        DisplayName = "Ngưỡng mỗi lần chi trả phải khấu trừ",
                        Description = "Mức chi trả từ ngưỡng này trở lên mỗi lần thì phải khấu trừ thuế vãng lai",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_RATE_NON_RESIDENT_SALARY,
                        RuleGroup = LawConstants.RuleGroup.RATE,
                        ValueKind = LawConstants.ValueKind.RATE,
                        DefaultUnit = "%",
                        DisplayName = "Thuế suất tiền lương cá nhân không cư trú",
                        Description = "Thuế suất thuế TNCN đối với thu nhập từ tiền lương, tiền công của cá nhân không cư trú",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_EXEMPTION_OVERTIME,
                        RuleGroup = LawConstants.RuleGroup.EXEMPTION,
                        ValueKind = LawConstants.ValueKind.TEXT,
                        DefaultUnit = null,
                        DisplayName = "Miễn thuế phần làm đêm, thêm giờ",
                        Description = "Phần tiền lương, tiền công trả cao hơn do làm việc ban đêm, làm thêm giờ được miễn thuế",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_EXEMPTION_RETIREMENT_PENSION,
                        RuleGroup = LawConstants.RuleGroup.EXEMPTION,
                        ValueKind = LawConstants.ValueKind.TEXT,
                        DefaultUnit = null,
                        DisplayName = "Miễn thuế lương hưu từ Quỹ BHXH",
                        Description = "Tiền lương hưu do Quỹ bảo hiểm xã hội chi trả được miễn thuế TNCN",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new LawRuleDefinition
                    {
                        RuleCode = LawConstants.RuleCodes.PIT_EXEMPTION_INSURANCE_COMPENSATION,
                        RuleGroup = LawConstants.RuleGroup.EXEMPTION,
                        ValueKind = LawConstants.ValueKind.TEXT,
                        DefaultUnit = null,
                        DisplayName = "Miễn thuế bồi thường bảo hiểm, trợ cấp tai nạn lao động",
                        Description = "Tiền bồi thường bảo hiểm con người, tài sản, trợ cấp tai nạn lao động được miễn thuế TNCN",
                        RequiredForFlow3 = false,
                        RequiredFromTaxYear = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                );
            });

            // ── LawRevision ──────────────────────────────────────────────────────
            modelBuilder.Entity<LawRevision>(entity =>
            {
                entity.ToTable("law_revisions");
                entity.HasKey(r => r.RevisionNo);

                entity.Property(r => r.RevisionNo).HasColumnName("revision_no").ValueGeneratedNever();
                entity.Property(r => r.ChangesetId).HasColumnName("changeset_id");
                entity.Property(r => r.DocumentId).HasColumnName("document_id");
                entity.Property(r => r.Description).HasColumnName("description").IsRequired();
                entity.Property(r => r.CommittedBy).HasColumnName("committed_by").HasMaxLength(100).IsRequired();
                entity.Property(r => r.CommittedAt).HasColumnName("committed_at");
                entity.Property(r => r.MetaVersion).HasColumnName("meta_version").HasDefaultValue(1L);

                entity.HasOne(r => r.Changeset)
                    .WithOne(c => c.Revision)
                    .HasForeignKey<LawRevision>(r => r.ChangesetId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Document)
                    .WithMany()
                    .HasForeignKey(r => r.DocumentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ── LawRuleVersion ───────────────────────────────────────────────────
            modelBuilder.Entity<LawRuleVersion>(entity =>
            {
                entity.ToTable("law_rule_versions", t =>
                {
                    t.HasCheckConstraint("chk_law_rule_versions_apply_range", "apply_to IS NULL OR apply_to > apply_from");
                });
                entity.HasKey(v => v.Id);

                entity.Property(v => v.Id).HasColumnName("id");
                entity.Property(v => v.CreatedInRevision).HasColumnName("created_in_revision");
                entity.Property(v => v.RuleCode).HasColumnName("rule_code").HasMaxLength(50).IsRequired();
                entity.Property(v => v.DocumentId).HasColumnName("document_id");
                entity.Property(v => v.Article).HasColumnName("article").HasMaxLength(50);
                entity.Property(v => v.Clause).HasColumnName("clause").HasMaxLength(50);
                entity.Property(v => v.Point).HasColumnName("point").HasMaxLength(50);
                entity.Property(v => v.Page).HasColumnName("page");
                entity.Property(v => v.EvidenceText).HasColumnName("evidence_text");
                entity.Property(v => v.ApplyFrom).HasColumnName("apply_from").IsRequired();
                entity.Property(v => v.ApplyTo).HasColumnName("apply_to");
                entity.Property(v => v.RuleValue).HasColumnName("rule_value").HasColumnType("jsonb").IsRequired();
                entity.Property(v => v.SupersededInRevision).HasColumnName("superseded_in_revision");
                entity.Property(v => v.SupersededAt).HasColumnName("superseded_at");
                entity.Property(v => v.DerivedFromVersionId).HasColumnName("derived_from_version_id");
                entity.Property(v => v.SourceOpId).HasColumnName("source_op_id");

                entity.HasOne(v => v.Revision)
                    .WithMany(r => r.RuleVersionsCreated)
                    .HasForeignKey(v => v.CreatedInRevision)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.SupersededRevision)
                    .WithMany(r => r.RuleVersionsSuperseded)
                    .HasForeignKey(v => v.SupersededInRevision)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.RuleDefinition)
                    .WithMany(d => d.Versions)
                    .HasForeignKey(v => v.RuleCode)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.Document)
                    .WithMany(d => d.RuleVersions)
                    .HasForeignKey(v => v.DocumentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.HasOne(v => v.SourceOp)
                    .WithMany()
                    .HasForeignKey(v => v.SourceOpId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(v => new { v.RuleCode, v.ApplyFrom })
                    .HasFilter("superseded_in_revision IS NULL")
                    .HasDatabaseName("idx_law_rule_versions_active");
            });

            // ── LawChangeset ─────────────────────────────────────────────────────
            modelBuilder.Entity<LawChangeset>(entity =>
            {
                entity.ToTable("law_changesets");
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Id).HasColumnName("id");
                entity.Property(c => c.DocumentId).HasColumnName("document_id");
                entity.Property(c => c.BaseRevisionNo).HasColumnName("base_revision_no");
                entity.Property(c => c.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("EXTRACTING");
                entity.Property(c => c.Origin).HasColumnName("origin").HasMaxLength(20).HasDefaultValue("AI");
                entity.Property(c => c.AiTaskId).HasColumnName("ai_task_id");
                entity.Property(c => c.AiModel).HasColumnName("ai_model").HasMaxLength(50);
                entity.Property(c => c.AiRawResponse).HasColumnName("ai_raw_response").HasColumnType("jsonb");
                entity.Property(c => c.AiWarnings).HasColumnName("ai_warnings").HasColumnType("jsonb");
                entity.Property(c => c.AiErrorCode).HasColumnName("ai_error_code").HasMaxLength(50);
                entity.Property(c => c.AiErrorMessage).HasColumnName("ai_error_message");
                entity.Property(c => c.PagesRead).HasColumnName("pages_read");
                entity.Property(c => c.TotalPages).HasColumnName("total_pages");
                entity.Property(c => c.Reason).HasColumnName("reason");
                entity.Property(c => c.CreatedBy).HasColumnName("created_by");
                entity.Property(c => c.CreatedAt).HasColumnName("created_at");
                entity.Property(c => c.UpdatedAt).HasColumnName("updated_at");
                entity.Property(c => c.MergedRevisionNo).HasColumnName("merged_revision_no");
                entity.Property(c => c.MergedBy).HasColumnName("merged_by");
                entity.Property(c => c.MergedAt).HasColumnName("merged_at");
                entity.Property(c => c.RejectedReason).HasColumnName("rejected_reason");
                entity.Property(c => c.RejectedBy).HasColumnName("rejected_by");
                entity.Property(c => c.RejectedAt).HasColumnName("rejected_at");

                entity.HasOne(c => c.Document)
                    .WithMany(d => d.Changesets)
                    .HasForeignKey(c => c.DocumentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.HasMany(c => c.Ops)
                    .WithOne(o => o.Changeset)
                    .HasForeignKey(o => o.ChangesetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Relations)
                    .WithOne(r => r.Changeset)
                    .HasForeignKey(r => r.ChangesetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.OrphanResolutions)
                    .WithOne(o => o.Changeset)
                    .HasForeignKey(o => o.ChangesetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(c => c.AiTaskId)
                    .IsUnique()
                    .HasFilter("ai_task_id IS NOT NULL")
                    .HasDatabaseName("uq_law_changesets_ai_task_id");
            });

            // ── LawChangeOp ──────────────────────────────────────────────────────
            modelBuilder.Entity<LawChangeOp>(entity =>
            {
                entity.ToTable("law_change_ops");
                entity.HasKey(o => o.Id);

                entity.Property(o => o.Id).HasColumnName("id");
                entity.Property(o => o.ChangesetId).HasColumnName("changeset_id");
                entity.Property(o => o.Seq).HasColumnName("seq");
                entity.Property(o => o.OpKey).HasColumnName("op_key").HasMaxLength(50);
                entity.Property(o => o.OpType).HasColumnName("op_type").HasMaxLength(20).IsRequired();
                entity.Property(o => o.RuleCode).HasColumnName("rule_code").HasMaxLength(50).IsRequired();
                entity.Property(o => o.NewCode).HasColumnName("new_code").HasDefaultValue(false);
                entity.Property(o => o.ProposedDefinition).HasColumnName("proposed_definition").HasColumnType("jsonb");
                entity.Property(o => o.Before).HasColumnName("before").HasColumnType("jsonb");
                entity.Property(o => o.After).HasColumnName("after").HasColumnType("jsonb");
                entity.Property(o => o.ApplyFrom).HasColumnName("apply_from");
                entity.Property(o => o.ApplyTo).HasColumnName("apply_to");
                entity.Property(o => o.ApplyBasis).HasColumnName("apply_basis").HasColumnType("jsonb");
                entity.Property(o => o.Article).HasColumnName("article").HasMaxLength(50);
                entity.Property(o => o.Clause).HasColumnName("clause").HasMaxLength(50);
                entity.Property(o => o.Point).HasColumnName("point").HasMaxLength(50);
                entity.Property(o => o.Page).HasColumnName("page");
                entity.Property(o => o.EvidenceText).HasColumnName("evidence_text");
                entity.Property(o => o.Confidence).HasColumnName("confidence").HasColumnType("numeric(4,3)");
                entity.Property(o => o.Rationale).HasColumnName("rationale");
                entity.Property(o => o.Origin).HasColumnName("origin").HasMaxLength(20).HasDefaultValue("AI");
                entity.Property(o => o.Decision).HasColumnName("decision").HasMaxLength(20).HasDefaultValue("PENDING");
                entity.Property(o => o.EditedByAdmin).HasColumnName("edited_by_admin").HasDefaultValue(false);
                entity.Property(o => o.AdminNote).HasColumnName("admin_note");
                entity.Property(o => o.ConflictState).HasColumnName("conflict_state").HasMaxLength(20).HasDefaultValue("NONE");
                entity.Property(o => o.Flags).HasColumnName("flags").HasColumnType("jsonb").HasDefaultValue("[]");
                entity.Property(o => o.DecidedBy).HasColumnName("decided_by");
                entity.Property(o => o.DecidedAt).HasColumnName("decided_at");
                entity.Property(o => o.CreatedAt).HasColumnName("created_at");
                entity.Property(o => o.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(o => new { o.ChangesetId, o.Decision })
                    .HasDatabaseName("idx_law_change_ops_changeset");
            });

            // ── LawChangesetRelation ─────────────────────────────────────────────
            modelBuilder.Entity<LawChangesetRelation>(entity =>
            {
                entity.ToTable("law_changeset_relations");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Id).HasColumnName("id");
                entity.Property(r => r.ChangesetId).HasColumnName("changeset_id");
                entity.Property(r => r.RelationKey).HasColumnName("relation_key").HasMaxLength(50);
                entity.Property(r => r.RelationType).HasColumnName("relation_type").HasMaxLength(30).IsRequired();
                entity.Property(r => r.TargetDocumentNumber).HasColumnName("target_document_number").HasMaxLength(50).IsRequired();
                entity.Property(r => r.TargetArticle).HasColumnName("target_article").HasMaxLength(50);
                entity.Property(r => r.TargetClause).HasColumnName("target_clause").HasMaxLength(50);
                entity.Property(r => r.TargetPoint).HasColumnName("target_point").HasMaxLength(50);
                entity.Property(r => r.SourceArticle).HasColumnName("source_article").HasMaxLength(50);
                entity.Property(r => r.SourceClause).HasColumnName("source_clause").HasMaxLength(50);
                entity.Property(r => r.SourcePoint).HasColumnName("source_point").HasMaxLength(50);
                entity.Property(r => r.SourcePage).HasColumnName("source_page");
                entity.Property(r => r.EffectiveDate).HasColumnName("effective_date");
                entity.Property(r => r.EvidenceText).HasColumnName("evidence_text");
                entity.Property(r => r.Note).HasColumnName("note");
                entity.Property(r => r.Origin).HasColumnName("origin").HasMaxLength(20).HasDefaultValue("AI");
                entity.Property(r => r.Decision).HasColumnName("decision").HasMaxLength(20).HasDefaultValue("PENDING");
                entity.Property(r => r.ConflictState).HasColumnName("conflict_state").HasMaxLength(20).HasDefaultValue("NONE");
                entity.Property(r => r.ConflictDetail).HasColumnName("conflict_detail").HasColumnType("jsonb");
                entity.Property(r => r.TargetDocId).HasColumnName("target_doc_id");

                entity.HasOne(r => r.TargetDocument)
                    .WithMany()
                    .HasForeignKey(r => r.TargetDocId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ── LawOrphanResolution ──────────────────────────────────────────────
            modelBuilder.Entity<LawOrphanResolution>(entity =>
            {
                entity.ToTable("law_orphan_resolutions");
                entity.HasKey(o => o.Id);

                entity.Property(o => o.Id).HasColumnName("id");
                entity.Property(o => o.ChangesetId).HasColumnName("changeset_id");
                entity.Property(o => o.VersionId).HasColumnName("version_id");
                entity.Property(o => o.Action).HasColumnName("action").HasMaxLength(20).IsRequired();
                entity.Property(o => o.GeneratedOpId).HasColumnName("generated_op_id");
                entity.Property(o => o.Note).HasColumnName("note");
                entity.Property(o => o.ResolvedBy).HasColumnName("resolved_by");
                entity.Property(o => o.ResolvedAt).HasColumnName("resolved_at");

                entity.HasOne(o => o.RuleVersion)
                    .WithMany()
                    .HasForeignKey(o => o.VersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(o => o.GeneratedOp)
                    .WithMany()
                    .HasForeignKey(o => o.GeneratedOpId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(o => new { o.ChangesetId, o.VersionId })
                    .IsUnique()
                    .HasDatabaseName("uq_orphan_resolution_changeset_version");
            });
        }
    }
}
