using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class MedicalCrossCheckHelperTests
    {
        private readonly Mock<IUnitOfWork> _mockUow = new();
        private readonly Mock<IGenericRepository<DocumentItem>> _mockItemRepo = new();

        public MedicalCrossCheckHelperTests()
        {
            _mockUow.Setup(u => u.Repository<DocumentItem>()).Returns(_mockItemRepo.Object);
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_AllMatch_NoError()
        {
            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                BuyerName = "LÊ THỊ THU HƯƠNG",
                BuyerIdCard = "079109001234",
                TotalAmount = 1000000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"Lê Thị Thu Hương\",\"idCard\":\"079109001234\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientPaid\":1000000,\"totalHospitalAmount\":5000000,\"insurancePaidAmount\":4000000}"),
                MedicalLines = JsonSerializer.Deserialize<JsonElement>(
                    "[{\"itemName\":\"Khám chuyên khoa Tiêu hóa\"},{\"itemName\":\"Nội soi thực quản dạ dày gây mê\"}]")
            };

            var items = new List<DocumentItem>
            {
                new DocumentItem { DocumentId = parentDoc.Id, ItemName = "Khám chuyên khoa tiêu hóa" },
                new DocumentItem { DocumentId = parentDoc.Id, ItemName = "Nội soi thực quản dạ dày" }
            };
            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(items);

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            data.CrossCheckIssues.Should().BeEmpty();
            data.ValidationErrors.Should().BeEmpty();
            data.Status.Should().Be("EXTRACTED");
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_PatientNameMismatch_FailsValidation()
        {
            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                BuyerName = "LÊ THỊ THU HƯƠNG",
                BuyerIdCard = "079109001234",
                TotalAmount = 1000000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"NGUYỄN VĂN AN\",\"idCard\":\"079109001234\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>("{\"patientPaid\":1000000}")
            };

            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(new List<DocumentItem>());

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            data.CrossCheckIssues.Should().Contain("PATIENT_NAME_MISMATCH");
            data.ValidationErrors.Should().Contain(e => e.Contains("không trùng khớp với người đứng tên"));
            data.Status.Should().Be("FAILED");
            statement.Status.Should().Be("FAILED");
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_PatientIdCardMismatch_FailsValidation()
        {
            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                BuyerName = "LÊ THỊ THU HƯƠNG",
                BuyerIdCard = "079109001234",
                TotalAmount = 1000000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"LÊ THỊ THU HƯƠNG\",\"idCard\":\"079109999999\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>("{\"patientPaid\":1000000}")
            };

            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(new List<DocumentItem>());

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            data.CrossCheckIssues.Should().Contain("PATIENT_IDCARD_MISMATCH");
            data.ValidationErrors.Should().Contain(e => e.Contains("Số định danh/CCCD trên bảng kê"));
            data.Status.Should().Be("FAILED");
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_TotalAmountMismatch_FailsValidation()
        {
            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                BuyerName = "LÊ THỊ THU HƯƠNG",
                BuyerIdCard = "079109001234",
                TotalAmount = 1000000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"LÊ THỊ THU HƯƠNG\",\"idCard\":\"079109001234\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>("{\"patientPaid\":2500000}")
            };

            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(new List<DocumentItem>());

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            data.CrossCheckIssues.Should().Contain("TOTAL_AMOUNT_MISMATCH");
            data.ValidationErrors.Should().Contain(e => e.Contains("không khớp với số tiền thực tế"));
            data.Status.Should().Be("FAILED");
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_ServiceItemMismatch_ReportsDiscrepancy()
        {
            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                BuyerName = "LÊ THỊ THU HƯƠNG",
                BuyerIdCard = "079109001234",
                TotalAmount = 1000000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"LÊ THỊ THU HƯƠNG\",\"idCard\":\"079109001234\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>("{\"patientPaid\":1000000}"),
                MedicalLines = JsonSerializer.Deserialize<JsonElement>(
                    "[{\"itemName\":\"Khám chuyên khoa tiêu hóa\"}]")
            };

            // Invoice has an item that is completely absent from the statement
            var items = new List<DocumentItem>
            {
                new DocumentItem { DocumentId = parentDoc.Id, ItemName = "Chụp cộng hưởng từ sọ não (MRI)" }
            };
            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(items);

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            data.CrossCheckIssues.Should().Contain("SERVICE_ITEM_MISMATCH");
            data.ValidationErrors.Should().Contain(e => e.Contains("Chụp cộng hưởng từ sọ não (MRI)"));
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_PatientIsDependent_Success()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();

            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                PeriodId = periodId,
                BuyerName = "NGUYỄN VĂN AN",
                BuyerIdCard = "079109001111",
                TotalAmount = 500000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            // Bệnh nhân trên bảng kê là con (Người phụ thuộc: NGUYỄN BẢO CHÂU)
            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"Nguyễn Bảo Châu\",\"idCard\":\"079109002222\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>("{\"patientPaid\":500000}")
            };

            var mockPeriodRepo = new Mock<IGenericRepository<TaxPeriod>>();
            mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(new TaxPeriod { Id = periodId, UserId = userId });
            _mockUow.Setup(u => u.Repository<TaxPeriod>()).Returns(mockPeriodRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Nguyễn Văn An", CitizenId = "079109001111" });
            _mockUow.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockDepRepo = new Mock<IGenericRepository<Dependent>>();
            var dependents = new List<Dependent>
            {
                new Dependent { TaxpayerId = userId, FullName = "Nguyễn Bảo Châu", Status = TaxKeepVN.Domain.Enums.DependentStatus.ACTIVE, CitizenId = "079109002222" }
            };
            mockDepRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>())).ReturnsAsync(dependents);
            _mockUow.Setup(u => u.Repository<Dependent>()).Returns(mockDepRepo.Object);

            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(new List<DocumentItem>());

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            // Bệnh nhân là người phụ thuộc hợp lệ nên không bị báo lỗi PATIENT_NOT_TAXPAYER_OR_DEPENDENT
            data.CrossCheckIssues.Should().NotContain("PATIENT_NOT_TAXPAYER_OR_DEPENDENT");
            data.CrossCheckIssues.Should().NotContain("PATIENT_NAME_MISMATCH");
            data.Status.Should().Be("EXTRACTED");
        }

        [Fact]
        public async Task PerformMedicalCrossCheck_PatientNotTaxpayerOrDependent_Fails()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();

            var parentDoc = new Document
            {
                Id = Guid.NewGuid(),
                PeriodId = periodId,
                BuyerName = "NGUYỄN VĂN AN",
                TotalAmount = 500000m
            };

            var statement = new MedicalStatement { Id = Guid.NewGuid(), DocumentId = parentDoc.Id };

            // Bệnh nhân là một người hoàn toàn xa lạ: "TRẦN VĂN LẠ"
            var data = new DocumentOcrData
            {
                Status = "EXTRACTED",
                MedicalHeader = JsonSerializer.Deserialize<JsonElement>(
                    "{\"patientName\":\"Trần Văn Lạ\"}"),
                MedicalSummary = JsonSerializer.Deserialize<JsonElement>("{\"patientPaid\":500000}")
            };

            var mockPeriodRepo = new Mock<IGenericRepository<TaxPeriod>>();
            mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(new TaxPeriod { Id = periodId, UserId = userId });
            _mockUow.Setup(u => u.Repository<TaxPeriod>()).Returns(mockPeriodRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Nguyễn Văn An", CitizenId = "079109001111" });
            _mockUow.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockDepRepo = new Mock<IGenericRepository<Dependent>>();
            mockDepRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>())).ReturnsAsync(new List<Dependent>());
            _mockUow.Setup(u => u.Repository<Dependent>()).Returns(mockDepRepo.Object);

            _mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>())).ReturnsAsync(new List<DocumentItem>());

            await MedicalCrossCheckHelper.PerformMedicalCrossCheckAsync(_mockUow.Object, parentDoc, statement, data);

            // Bắt buộc phải chặn theo Khoản 3b NĐ 253/2026
            data.CrossCheckIssues.Should().Contain("PATIENT_NOT_TAXPAYER_OR_DEPENDENT");
            data.ValidationErrors.Should().Contain(e => e.Contains("không phải Người nộp thuế") && e.Contains("Người phụ thuộc"));
            data.Status.Should().Be("FAILED");
            statement.Status.Should().Be("FAILED");
        }
    }
}
