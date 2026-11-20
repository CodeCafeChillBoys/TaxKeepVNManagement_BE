using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class TaxPeriodServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IFileStorageService> _mockFileStorage;
        private readonly Mock<IDocumentOcrProducerService> _mockOcrProducer;
        private readonly Mock<ILogger<TaxPeriodService>> _mockLogger;
        private readonly Mock<IGenericRepository<TaxPeriod>> _mockPeriodRepo;
        private readonly TaxPeriodService _service;

        public TaxPeriodServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockFileStorage = new Mock<IFileStorageService>();
            _mockOcrProducer = new Mock<IDocumentOcrProducerService>();
            _mockLogger = new Mock<ILogger<TaxPeriodService>>();
            _mockPeriodRepo = new Mock<IGenericRepository<TaxPeriod>>();

            _mockUnitOfWork.Setup(u => u.Repository<TaxPeriod>()).Returns(_mockPeriodRepo.Object);

            var defaultStatementRepo = new Mock<IGenericRepository<MedicalStatement>>();
            defaultStatementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatement, bool>>>()))
                .ReturnsAsync(new List<MedicalStatement>());
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatement>()).Returns(defaultStatementRepo.Object);

            var defaultLineRepo = new Mock<IGenericRepository<MedicalStatementLine>>();
            defaultLineRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatementLine, bool>>>()))
                .ReturnsAsync(new List<MedicalStatementLine>());
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatementLine>()).Returns(defaultLineRepo.Object);

            _service = new TaxPeriodService(
                _mockUnitOfWork.Object,
                _mockFileStorage.Object,
                _mockOcrProducer.Object,
                _mockLogger.Object);
        }

        #region InitOrGetPeriodAsync

        [Fact]
        public async Task UTCID01_InitOrGetPeriodAsync_InvalidYear_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.InitOrGetPeriodAsync(Guid.NewGuid(), 2010));
            ex.ErrorCode.Should().Be(ErrorCodes.InvalidTaxYear);
        }

        [Fact]
        public async Task UTCID02_InitOrGetPeriodAsync_AlreadySubmitted_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var existing = new TaxPeriod
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TaxYear = 2024,
                Status = "SUBMITTED"
            };

            _mockPeriodRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxPeriod, bool>>>()))
                           .ReturnsAsync(new List<TaxPeriod> { existing });

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.InitOrGetPeriodAsync(userId, 2024));
        }

        [Fact]
        public async Task UTCID03_InitOrGetPeriodAsync_NotExists_CreatesDraft()
        {
            var userId = Guid.NewGuid();
            _mockPeriodRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxPeriod, bool>>>()))
                           .ReturnsAsync(new List<TaxPeriod>());

            var result = await _service.InitOrGetPeriodAsync(userId, 2024);

            result.Should().NotBeNull();
            result.TaxYear.Should().Be(2024);
            result.Status.Should().Be("DRAFT");
            _mockPeriodRepo.Verify(r => r.AddAsync(It.IsAny<TaxPeriod>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region SubmitTaxPeriodAsync

        [Fact]
        public async Task UTCID04_SubmitTaxPeriodAsync_NotFound_ThrowsNotFoundException()
        {
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                           .ReturnsAsync((TaxPeriod?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.SubmitTaxPeriodAsync(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID05_SubmitTaxPeriodAsync_AlreadySubmitted_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var period = new TaxPeriod { Id = Guid.NewGuid(), UserId = userId, Status = "SUBMITTED" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitTaxPeriodAsync(userId, period.Id));
            ex.ErrorCode.Should().Be(ErrorCodes.InvalidDocumentStatus);
        }

        [Fact]
        public async Task UTCID06_SubmitTaxPeriodAsync_Valid_SubmitsSuccessfully()
        {
            var userId = Guid.NewGuid();
            var period = new TaxPeriod { Id = Guid.NewGuid(), UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);

            var result = await _service.SubmitTaxPeriodAsync(userId, period.Id);

            result.Status.Should().Be("SUBMITTED");
            _mockPeriodRepo.Verify(r => r.Update(period), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        [Theory]
        [InlineData(true, false, true, null)]
        [InlineData(false, false, true, "ERR_NOT_MEDICAL_RECEIPT")]
        [InlineData(true, true, true, ErrorCodes.InvalidDocumentStatus)]
        [InlineData(true, false, false, "ERR_MEDICAL_STATEMENT_TYPE_NOT_CONFIGURED")]
        public async Task UploadMedicalStatementAsync_ValidatesAndQueuesLinkedDocument(
            bool isMedicalReceipt, bool alreadyLinked, bool statementTypeExists, string? expectedError)
        {
            var period = new TaxPeriod { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), TaxYear = 2026, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PeriodId = period.Id,
                DocTypeCode = isMedicalReceipt ? DocumentTypeCodes.MedicalReceipt : "OTHER"
            };
            var documents = new Mock<IGenericRepository<Document>>();
            documents.Setup(r => r.GetByIdAsync(receipt.Id)).ReturnsAsync(receipt);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(documents.Object);

            var statementRepo = new Mock<IGenericRepository<MedicalStatement>>();
            var existingStmt = alreadyLinked ? new MedicalStatement { DocumentId = receipt.Id, Status = MedicalStatementStatus.CONFIRMED } : null;
            statementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatement, bool>>>()))
                .ReturnsAsync(existingStmt != null ? new List<MedicalStatement> { existingStmt } : new List<MedicalStatement>());
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatement>()).Returns(statementRepo.Object);

            var configuredTypes = new List<TaxDocumentType>
            {
                new() { Code = DocumentTypeCodes.MedicalReceipt, Name = "Medical receipt" }
            };
            if (statementTypeExists)
                configuredTypes.Add(new TaxDocumentType
                {
                    Code = DocumentTypeCodes.MedicalCostStatement, Name = "Medical statement", IsTaxEligible = false
                });
            var types = new Mock<IGenericRepository<TaxDocumentType>>();
            types.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                .ReturnsAsync((Expression<Func<TaxDocumentType, bool>> filter) => configuredTypes.Where(filter.Compile()).ToList());
            types.Setup(r => r.GetAllAsync()).ReturnsAsync(configuredTypes);
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(types.Object);

            var catRepo = new Mock<IGenericRepository<MedicalEligibleCategory>>();
            catRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalEligibleCategory, bool>>>()))
                .ReturnsAsync(new List<MedicalEligibleCategory>());
            _mockUnitOfWork.Setup(u => u.Repository<MedicalEligibleCategory>()).Returns(catRepo.Object);

            using var stream = new MemoryStream(new byte[256]);
            var file = new FormFile(stream, 0, stream.Length, "file", "statement.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };
            _mockFileStorage.Setup(s => s.SaveFileAsync(file, "tax-periods/2026/medical-statements"))
                .ReturnsAsync("https://storage/statement.pdf");

            if (expectedError != null)
            {
                var error = await Assert.ThrowsAsync<BadRequestException>(() =>
                    _service.UploadMedicalStatementAsync(period.UserId, period.Id, receipt.Id, file));
                error.ErrorCode.Should().Be(expectedError);
                _mockFileStorage.VerifyNoOtherCalls();
                _mockOcrProducer.VerifyNoOtherCalls();
                return;
            }

            var result = await _service.UploadMedicalStatementAsync(period.UserId, period.Id, receipt.Id, file);

            result.Id.Should().NotBe(receipt.Id);
            result.DocumentId.Should().Be(receipt.Id);
            result.Status.Should().Be("UPLOADED");
            result.FileUrl.Should().Be("https://storage/statement.pdf");
            statementRepo.Verify(r => r.AddAsync(It.Is<MedicalStatement>(s =>
                s.Id == result.Id && s.DocumentId == receipt.Id)), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
            _mockOcrProducer.Verify(p => p.PublishBatchOcrTasks(It.Is<IEnumerable<DocumentOcrExtractRequestMessage>>(
                jobs => jobs.Count() == 1 && jobs.Single().TaskId == result.Id &&
                    jobs.Single().Categories.Any(c => c.Code == DocumentTypeCodes.MedicalCostStatement))), Times.Once);
        }

        [Fact]
        public async Task UploadMedicalStatementAsync_WhenAlreadyExistsAndNotConfirmed_OverwritesExistingStatement()
        {
            var period = new TaxPeriod { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), TaxYear = 2026, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PeriodId = period.Id,
                DocTypeCode = DocumentTypeCodes.MedicalReceipt
            };
            var documents = new Mock<IGenericRepository<Document>>();
            documents.Setup(r => r.GetByIdAsync(receipt.Id)).ReturnsAsync(receipt);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(documents.Object);

            var existingStmt = new MedicalStatement
            {
                Id = Guid.NewGuid(),
                DocumentId = receipt.Id,
                Status = MedicalStatementStatus.EXTRACTED,
                FileUrl = "https://storage/old.pdf"
            };
            var statementRepo = new Mock<IGenericRepository<MedicalStatement>>();
            statementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatement, bool>>>()))
                .ReturnsAsync(new List<MedicalStatement> { existingStmt });
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatement>()).Returns(statementRepo.Object);

            var configuredTypes = new List<TaxDocumentType>
            {
                new() { Code = DocumentTypeCodes.MedicalReceipt, Name = "Medical receipt" },
                new() { Code = DocumentTypeCodes.MedicalCostStatement, Name = "Medical statement", IsTaxEligible = false }
            };
            var types = new Mock<IGenericRepository<TaxDocumentType>>();
            types.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                .ReturnsAsync((Expression<Func<TaxDocumentType, bool>> filter) => configuredTypes.Where(filter.Compile()).ToList());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(types.Object);

            var catRepo = new Mock<IGenericRepository<MedicalEligibleCategory>>();
            catRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalEligibleCategory, bool>>>()))
                .ReturnsAsync(new List<MedicalEligibleCategory>());
            _mockUnitOfWork.Setup(u => u.Repository<MedicalEligibleCategory>()).Returns(catRepo.Object);

            using var stream = new MemoryStream(new byte[256]);
            var file = new FormFile(stream, 0, stream.Length, "file", "new_statement.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };
            _mockFileStorage.Setup(s => s.SaveFileAsync(file, "tax-periods/2026/medical-statements"))
                .ReturnsAsync("https://storage/new_statement.pdf");

            var result = await _service.UploadMedicalStatementAsync(period.UserId, period.Id, receipt.Id, file);

            result.Should().NotBeNull();
            result.Id.Should().Be(existingStmt.Id);
            result.FileUrl.Should().Be("https://storage/new_statement.pdf");
            result.Status.Should().Be("UPLOADED");
            statementRepo.Verify(r => r.Update(existingStmt), Times.Once);
            _mockFileStorage.Verify(f => f.DeleteFileAsync("https://storage/old.pdf"), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
            _mockOcrProducer.Verify(p => p.PublishBatchOcrTasks(It.IsAny<IEnumerable<DocumentOcrExtractRequestMessage>>()), Times.Once);
        }

        #region BatchUploadDocumentsAsync

        [Fact]
        public async Task UTCID01_BatchUploadDocumentsAsync_SingleValidPdf_UploadsAndReturnsResponse()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024 * 1024); // 1MB
            mockFile.Setup(f => f.FileName).Returns("invoice.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            _mockFileStorage.Setup(f => f.SaveFileAsync(mockFile.Object, "tax-periods/2025"))
                            .ReturnsAsync("https://storage/tax-periods/2025/inv.pdf");

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var result = await _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object });

            result.Should().NotBeNull();
            result.PeriodId.Should().Be(periodId);
            result.TotalUploaded.Should().Be(1);
            result.Documents.Should().HaveCount(1);
            result.Documents[0].Status.Should().Be("UPLOADED");
            result.Documents[0].OriginalFilename.Should().Be("invoice.pdf");
            mockDocRepo.Verify(r => r.AddAsync(It.Is<Document>(d => d.PeriodId == periodId && d.IncomeYear == 2025 && d.Status == "UPLOADED")), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
            _mockOcrProducer.Verify(o => o.PublishBatchOcrTasks(It.IsAny<IEnumerable<DocumentOcrExtractRequestMessage>>()), Times.Once);
        }

        [Fact]
        public async Task UTCID02_BatchUploadDocumentsAsync_MultipleValidImages_UploadsAllAndDispatchesOcr()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile1 = new Mock<IFormFile>();
            mockFile1.Setup(f => f.Length).Returns(500 * 1024);
            mockFile1.Setup(f => f.FileName).Returns("receipt.jpg");
            mockFile1.Setup(f => f.ContentType).Returns("image/jpeg");

            var mockFile2 = new Mock<IFormFile>();
            mockFile2.Setup(f => f.Length).Returns(800 * 1024);
            mockFile2.Setup(f => f.FileName).Returns("stamp.png");
            mockFile2.Setup(f => f.ContentType).Returns("image/png");

            _mockFileStorage.Setup(f => f.SaveFileAsync(It.IsAny<IFormFile>(), "tax-periods/2025"))
                            .ReturnsAsync("https://storage/tax-periods/2025/file.png");

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var result = await _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile1.Object, mockFile2.Object });

            result.Should().NotBeNull();
            result.TotalUploaded.Should().Be(2);
            result.Documents.Should().HaveCount(2);
            mockDocRepo.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Exactly(2));
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID03_BatchUploadDocumentsAsync_MaxAllowedFileSize10MB_UploadsSuccessfully()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(10 * 1024 * 1024); // Exactly 10MB
            mockFile.Setup(f => f.FileName).Returns("max_size.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            _mockFileStorage.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                            .ReturnsAsync("https://storage/tax-periods/2025/max.pdf");

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var result = await _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object });

            result.Should().NotBeNull();
            result.TotalUploaded.Should().Be(1);
        }

        [Fact]
        public async Task UTCID04_BatchUploadDocumentsAsync_MinAllowedFileSize1Byte_UploadsSuccessfully()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1); // Exactly 1 byte
            mockFile.Setup(f => f.FileName).Returns("tiny.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            _mockFileStorage.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                            .ReturnsAsync("https://storage/tax-periods/2025/tiny.pdf");

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var result = await _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object });

            result.Should().NotBeNull();
            result.TotalUploaded.Should().Be(1);
        }

        [Fact]
        public async Task UTCID05_BatchUploadDocumentsAsync_PeriodNotFound_ThrowsNotFoundException()
        {
            var periodId = Guid.NewGuid();
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync((TaxPeriod?)null);

            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.BatchUploadDocumentsAsync(Guid.NewGuid(), periodId, new List<IFormFile>()));
            ex.Message.Should().Be(ErrorMessages.TaxPeriodNotFound);
        }

        [Fact]
        public async Task UTCID06_BatchUploadDocumentsAsync_PeriodBelongsToAnotherUser_ThrowsNotFoundException()
        {
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = ownerId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.BatchUploadDocumentsAsync(otherUserId, periodId, new List<IFormFile>()));
            ex.Message.Should().Be(ErrorMessages.TaxPeriodNotFound);
        }

        [Fact]
        public async Task UTCID07_BatchUploadDocumentsAsync_PeriodAlreadySubmitted_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "SUBMITTED" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile>()));
            ex.Message.Should().Be(ErrorMessages.TaxPeriodSubmitted);
        }

        [Fact]
        public async Task UTCID08_BatchUploadDocumentsAsync_FilesEmptyOrNull_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile>()));
            ex.ErrorCode.Should().Be(ErrorCodes.FilesRequired);
        }

        [Fact]
        public async Task UTCID09_BatchUploadDocumentsAsync_FileLengthZero_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(0); // Zero byte
            mockFile.Setup(f => f.FileName).Returns("empty.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object }));
            ex.ErrorCode.Should().Be(ErrorCodes.FilesRequired);
        }

        [Fact]
        public async Task UTCID10_BatchUploadDocumentsAsync_UnsupportedExtension_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.FileName).Returns("script.exe");
            mockFile.Setup(f => f.ContentType).Returns("application/octet-stream");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object }));
            ex.ErrorCode.Should().Be(ErrorCodes.UnsupportedFormat);
        }

        [Fact]
        public async Task UTCID11_BatchUploadDocumentsAsync_FileSizeExceeds10MB_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(10 * 1024 * 1024 + 1); // 10MB + 1 byte
            mockFile.Setup(f => f.FileName).Returns("large.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object }));
            ex.ErrorCode.Should().Be(ErrorCodes.FileSizeExceeded);
        }

        [Fact]
        public async Task UTCID12_BatchUploadDocumentsAsync_OcrProducerFails_GracefulDegradationSuccess()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("invoice.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            _mockFileStorage.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                            .ReturnsAsync("https://storage/tax-periods/2025/inv.pdf");

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            // RabbitMQ throws exception
            _mockOcrProducer.Setup(o => o.PublishBatchOcrTasks(It.IsAny<IEnumerable<DocumentOcrExtractRequestMessage>>()))
                            .Throws(new Exception("RabbitMQ cluster unreachable"));

            var result = await _service.BatchUploadDocumentsAsync(userId, periodId, new List<IFormFile> { mockFile.Object });

            result.Should().NotBeNull();
            result.TotalUploaded.Should().Be(1);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        }

        #endregion

        #region ConfirmDocumentReviewAsync

        [Fact]
        public async Task UTCID10_ConfirmDocumentReviewAsync_NotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.ConfirmDocumentReviewAsync(userId, periodId, Guid.NewGuid(), new ConfirmDocumentReviewRequestDto()));
        }

        #endregion

        #region DeleteDocumentAsync

        [Fact]
        public async Task UTCID11_DeleteDocumentAsync_NotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.DeleteDocumentAsync(userId, periodId, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID12_DeleteDocumentAsync_Valid_DeletesDocument()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "UPLOADED", FileUrl = "https://storage/doc.pdf" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockItemRepo = new Mock<IGenericRepository<DocumentItem>>();
            mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>()))
                        .ReturnsAsync(new List<DocumentItem>());
            _mockUnitOfWork.Setup(u => u.Repository<DocumentItem>()).Returns(mockItemRepo.Object);

            await _service.DeleteDocumentAsync(userId, periodId, docId);

            mockDocRepo.Verify(r => r.Remove(document), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetDocumentsAsync

        [Fact]
        public async Task UTCID13_GetDocumentsAsync_Valid_ReturnsDocuments()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var doc = new Document { Id = Guid.NewGuid(), PeriodId = periodId, DocTypeCode = "INV" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>()))
                       .ReturnsAsync(new List<Document> { doc });
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var mockItemRepo = new Mock<IGenericRepository<DocumentItem>>();
            mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>()))
                        .ReturnsAsync(new List<DocumentItem>());
            _mockUnitOfWork.Setup(u => u.Repository<DocumentItem>()).Returns(mockItemRepo.Object);

            var result = await _service.GetDocumentsAsync(userId, periodId);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
        }

        #endregion

        #region ConfirmDocumentReviewAsync - Extended

        [Fact]
        public async Task UTCID14_ConfirmDocumentReviewAsync_PeriodSubmitted_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "SUBMITTED" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.ConfirmDocumentReviewAsync(userId, periodId, Guid.NewGuid(), new ConfirmDocumentReviewRequestDto()));
        }

        [Fact]
        public async Task UTCID15_ConfirmDocumentReviewAsync_AlreadyConfirmed_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "CONFIRMED" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ConfirmDocumentReviewAsync(userId, periodId, docId, new ConfirmDocumentReviewRequestDto()));
            ex.ErrorCode.Should().Be(ErrorCodes.InvalidDocumentStatus);
        }

        [Fact]
        public async Task UTCID16_ConfirmDocumentReviewAsync_DuplicateInvoiceNumber_ThrowsConflictException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var otherDocId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "UPLOADED" };
            var otherDoc = new Document
            {
                Id = otherDocId,
                PeriodId = periodId,
                Status = "CONFIRMED",
                SellerTaxCode = "0101234567",
                InvoiceNumber = "HD001"
            };

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>()))
                       .ReturnsAsync(new List<Document> { otherDoc });
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var dto = new ConfirmDocumentReviewRequestDto
            {
                SellerTaxCode = "0101234567",
                InvoiceNumber = "HD001"
            };

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.ConfirmDocumentReviewAsync(userId, periodId, docId, dto));
            ex.ErrorCode.Should().Be(ErrorCodes.DuplicateDocument);
        }

        [Fact]
        public async Task UTCID17_ConfirmDocumentReviewAsync_DuplicateWithoutInvoiceNumber_ThrowsConflictException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var otherDocId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "UPLOADED", DocTypeCode = "RECEIPT" };
            var otherDoc = new Document
            {
                Id = otherDocId,
                PeriodId = periodId,
                Status = "CONFIRMED",
                DocTypeCode = "RECEIPT",
                SellerTaxCode = "0101234567",
                InvoiceNumber = null,
                TotalIncome = 1000000m,
                TotalAmount = 1000000m
            };

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>()))
                       .ReturnsAsync(new List<Document> { otherDoc });
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var dto = new ConfirmDocumentReviewRequestDto
            {
                SellerTaxCode = "0101234567",
                InvoiceNumber = null,
                TotalIncome = 1000000m,
                TotalAmount = 1000000m
            };

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.ConfirmDocumentReviewAsync(userId, periodId, docId, dto));
            ex.ErrorCode.Should().Be(ErrorCodes.DuplicateDocument);
        }

        [Fact]
        public async Task UTCID18_ConfirmDocumentReviewAsync_ValidWithholdingVoucher_SyncsIncomeSource()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document
            {
                Id = docId,
                PeriodId = periodId,
                DocTypeCode = "WITHHOLDING_VOUCHER",
                Status = "UPLOADED",
                SellerTaxCode = "0109999999"
            };

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            // In duplicate check (other docs):
            mockDocRepo.Setup(r => r.FindAsync(It.Is<Expression<Func<Document, bool>>>(e => !e.ToString().Contains("WITHHOLDING_VOUCHER"))))
                       .ReturnsAsync(new List<Document>());
            // In sync withholding vouchers:
            mockDocRepo.Setup(r => r.FindAsync(It.Is<Expression<Func<Document, bool>>>(e => e.ToString().Contains("WITHHOLDING_VOUCHER"))))
                       .ReturnsAsync(new List<Document> { document });

            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockIncomeRepo = new Mock<IGenericRepository<IncomeSource>>();
            mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                          .ReturnsAsync(new List<IncomeSource>());
            _mockUnitOfWork.Setup(u => u.Repository<IncomeSource>()).Returns(mockIncomeRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                        .ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var mockItemRepo = new Mock<IGenericRepository<DocumentItem>>();
            _mockUnitOfWork.Setup(u => u.Repository<DocumentItem>()).Returns(mockItemRepo.Object);

            var dto = new ConfirmDocumentReviewRequestDto
            {
                SellerTaxCode = "0109999999",
                SellerName = "Cong Ty ABC",
                TotalAmount = 50000000m,
                TotalIncome = 50000000m,
                TaxWithheld = 5000000m
            };

            var result = await _service.ConfirmDocumentReviewAsync(userId, periodId, docId, dto);

            result.Should().NotBeNull();
            document.Status.Should().Be("CONFIRMED");
            mockIncomeRepo.Verify(r => r.AddAsync(It.Is<IncomeSource>(s => s.CompanyTaxCode == "0109999999" && s.TotalIncome == 50000000m)), Times.Once);
        }

        [Fact]
        public async Task UTCID19_ConfirmDocumentReviewAsync_ValidWithItems_UpdatesItemsAndConfirms()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "UPLOADED" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>())).ReturnsAsync(new List<Document>());
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var oldItem = new DocumentItem { Id = 1L, DocumentId = docId };
            var mockItemRepo = new Mock<IGenericRepository<DocumentItem>>();
            mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>()))
                        .ReturnsAsync(new List<DocumentItem> { oldItem });
            _mockUnitOfWork.Setup(u => u.Repository<DocumentItem>()).Returns(mockItemRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>())).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var dto = new ConfirmDocumentReviewRequestDto
            {
                SellerTaxCode = "0109876543",
                SellerName = "Benh Vien XYZ",
                TotalAmount = 2000000m,
                Items = new List<TaxKeepVN.Application.DTOs.TaxAI.InvoiceLineItemDto>
                {
                    new TaxKeepVN.Application.DTOs.TaxAI.InvoiceLineItemDto { ItemName = "Thuoc khang sinh", TotalPrice = 500000m },
                    new TaxKeepVN.Application.DTOs.TaxAI.InvoiceLineItemDto { ItemName = "Vien phi", TotalPrice = 1500000m }
                }
            };

            var result = await _service.ConfirmDocumentReviewAsync(userId, periodId, docId, dto);

            result.Should().NotBeNull();
            mockItemRepo.Verify(r => r.Remove(oldItem), Times.Once);
            mockItemRepo.Verify(r => r.AddAsync(It.IsAny<DocumentItem>()), Times.Exactly(2));
            document.Status.Should().Be("CONFIRMED");
        }

        [Fact]
        public async Task UTCID19B_ConfirmDocumentReviewAsync_WithExtractedMedicalStatement_ConfirmsStatementTogether()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2026 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, DocTypeCode = DocumentTypeCodes.MedicalReceipt, Status = "EXTRACTED" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>())).ReturnsAsync(new List<Document>());
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var statement = new MedicalStatement
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                Status = MedicalStatementStatus.EXTRACTED,
                TotalEligibleAmount = 2500000m
            };
            var mockStmtRepo = new Mock<IGenericRepository<MedicalStatement>>();
            mockStmtRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatement, bool>>>()))
                .ReturnsAsync(new List<MedicalStatement> { statement });
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatement>()).Returns(mockStmtRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>())).ReturnsAsync(new List<TaxDocumentType>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var dto = new ConfirmDocumentReviewRequestDto
            {
                SellerTaxCode = "0109876543",
                SellerName = "Benh Vien Bach Mai",
                TotalAmount = 5000000m
            };

            var result = await _service.ConfirmDocumentReviewAsync(userId, periodId, docId, dto);

            result.Should().NotBeNull();
            document.Status.Should().Be("CONFIRMED");
            statement.Status.Should().Be(MedicalStatementStatus.CONFIRMED);
            mockStmtRepo.Verify(r => r.Update(statement), Times.Once);
            result.MedicalStatement.Should().NotBeNull();
            result.MedicalStatement!.Status.Should().Be(MedicalStatementStatus.CONFIRMED);
        }

        [Fact]
        public async Task UTCID19C_DeleteMedicalStatementAsync_Valid_DeletesStatementAndLines()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var stmtId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2026 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, DocTypeCode = DocumentTypeCodes.MedicalReceipt, Status = "EXTRACTED" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var statement = new MedicalStatement
            {
                Id = stmtId,
                DocumentId = docId,
                Status = MedicalStatementStatus.EXTRACTED,
                FileUrl = "https://storage/stmt.pdf"
            };
            var mockStmtRepo = new Mock<IGenericRepository<MedicalStatement>>();
            mockStmtRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatement, bool>>>()))
                .ReturnsAsync(new List<MedicalStatement> { statement });
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatement>()).Returns(mockStmtRepo.Object);

            var line = new MedicalStatementLine { Id = 1, MedicalStatementId = stmtId };
            var mockLineRepo = new Mock<IGenericRepository<MedicalStatementLine>>();
            mockLineRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatementLine, bool>>>()))
                .ReturnsAsync(new List<MedicalStatementLine> { line });
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatementLine>()).Returns(mockLineRepo.Object);

            await _service.DeleteMedicalStatementAsync(userId, periodId, docId);

            mockLineRepo.Verify(r => r.Remove(line), Times.Once);
            mockStmtRepo.Verify(r => r.Remove(statement), Times.Once);
            _mockFileStorage.Verify(f => f.DeleteFileAsync(statement.FileUrl), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region TriggerDocumentOcrAsync

        [Fact]
        public async Task UTCID20_TriggerDocumentOcrAsync_PeriodNotFound_ThrowsNotFoundException()
        {
            var periodId = Guid.NewGuid();
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync((TaxPeriod?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.TriggerDocumentOcrAsync(Guid.NewGuid(), periodId, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID21_TriggerDocumentOcrAsync_PeriodSubmitted_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "SUBMITTED" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.TriggerDocumentOcrAsync(userId, periodId, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID22_TriggerDocumentOcrAsync_DocumentNotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.TriggerDocumentOcrAsync(userId, periodId, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID23_TriggerDocumentOcrAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "CONFIRMED" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.TriggerDocumentOcrAsync(userId, periodId, docId));
            ex.ErrorCode.Should().Be(ErrorCodes.InvalidDocumentStatus);
        }

        [Fact]
        public async Task UTCID24_TriggerDocumentOcrAsync_Valid_PublishesOcrMessage()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT", TaxYear = 2025 };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "UPLOADED", FileUrl = "https://taxkeep.vn/files/doc.pdf" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>
            {
                new TaxDocumentType { Code = "INV", Name = "Hoa don", IsTaxEligible = true }
            });
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            await _service.TriggerDocumentOcrAsync(userId, periodId, docId);

            _mockOcrProducer.Verify(p => p.PublishBatchOcrTasks(It.IsAny<IEnumerable<TaxKeepVN.Application.DTOs.TaxAI.DocumentOcrExtractRequestMessage>>()), Times.Once);
        }

        #endregion

        #region DeleteDocumentAsync & GetDocumentByIdAsync - Extended

        [Fact]
        public async Task UTCID25_DeleteDocumentAsync_PeriodSubmitted_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "SUBMITTED" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.DeleteDocumentAsync(userId, periodId, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID26_DeleteDocumentAsync_AlreadyConfirmed_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, Status = "CONFIRMED" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.DeleteDocumentAsync(userId, periodId, docId));
            ex.ErrorCode.Should().Be(ErrorCodes.InvalidDocumentStatus);
        }

        [Fact]
        public async Task UTCID27_GetDocumentByIdAsync_NotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetDocumentByIdAsync(userId, periodId, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID28_GetDocumentByIdAsync_Valid_ReturnsDocumentReviewDto()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, DocTypeCode = "INV" };
            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>
            {
                new TaxDocumentType { Code = "INV", Name = "Hoa don gia tri gia tang" }
            });
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var item = new DocumentItem { Id = 1L, DocumentId = docId, ItemName = "Hang hoa" };
            var mockItemRepo = new Mock<IGenericRepository<DocumentItem>>();
            mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>()))
                        .ReturnsAsync(new List<DocumentItem> { item });
            _mockUnitOfWork.Setup(u => u.Repository<DocumentItem>()).Returns(mockItemRepo.Object);

            var result = await _service.GetDocumentByIdAsync(userId, periodId, docId);

            result.Should().NotBeNull();
            result.Id.Should().Be(docId);
            result.DocTypeName.Should().Be("Hoa don gia tri gia tang");
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID29_GetDocumentByIdAsync_WithChildMedicalStatement_MapsErrorsAndCrossCheckIssues()
        {
            var userId = Guid.NewGuid();
            var periodId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var childDocId = Guid.NewGuid();

            var period = new TaxPeriod { Id = periodId, UserId = userId, Status = "DRAFT" };
            _mockPeriodRepo.Setup(r => r.GetByIdAsync(periodId)).ReturnsAsync(period);

            var document = new Document { Id = docId, PeriodId = periodId, DocTypeCode = "MEDICAL_RECEIPT" };
            var statement = new MedicalStatement
            {
                Id = childDocId,
                DocumentId = docId,
                PatientName = "LÊ THỊ THU HƯƠNG",
                ValidationErrors = "[\"Thông tin bệnh nhân trên bảng kê không trùng khớp với hóa đơn viện phí.\"]"
            };

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            mockDocRepo.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(document);
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            var mockStatementRepo = new Mock<IGenericRepository<MedicalStatement>>();
            mockStatementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatement, bool>>>()))
                             .ReturnsAsync(new List<MedicalStatement> { statement });
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatement>()).Returns(mockStatementRepo.Object);

            var mockLineRepo = new Mock<IGenericRepository<MedicalStatementLine>>();
            mockLineRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalStatementLine, bool>>>()))
                        .ReturnsAsync(new List<MedicalStatementLine>());
            _mockUnitOfWork.Setup(u => u.Repository<MedicalStatementLine>()).Returns(mockLineRepo.Object);

            var mockTypeRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            mockTypeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<TaxDocumentType>
            {
                new TaxDocumentType { Code = "MEDICAL_RECEIPT", Name = "Hóa đơn viện phí" }
            });
            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(mockTypeRepo.Object);

            var mockItemRepo = new Mock<IGenericRepository<DocumentItem>>();
            mockItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DocumentItem, bool>>>()))
                        .ReturnsAsync(new List<DocumentItem>());
            _mockUnitOfWork.Setup(u => u.Repository<DocumentItem>()).Returns(mockItemRepo.Object);

            var result = await _service.GetDocumentByIdAsync(userId, periodId, docId);

            result.Should().NotBeNull();
            result.MedicalStatement.Should().NotBeNull();
            result.ValidationErrors.Should().Contain("Thông tin bệnh nhân trên bảng kê không trùng khớp với hóa đơn viện phí.");
        }

        #endregion
    }
}
