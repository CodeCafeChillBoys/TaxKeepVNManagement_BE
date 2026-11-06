using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentDocumentServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IFileStorageService> _mockFileStorageService;
        private readonly IMemoryCache _cache;
        private readonly Mock<IOcrService> _mockOcrService;
        private readonly Mock<IGenericRepository<Dependent>> _mockDependentRepo;
        private readonly Mock<IGenericRepository<DependentDocument>> _mockDocRepo;
        private readonly DependentDocumentService _service;

        public DependentDocumentServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockFileStorageService = new Mock<IFileStorageService>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockOcrService = new Mock<IOcrService>();
            _mockDependentRepo = new Mock<IGenericRepository<Dependent>>();
            _mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();

            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(_mockDependentRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(_mockDocRepo.Object);

            _service = new DependentDocumentService(
                _mockUnitOfWork.Object,
                _mockFileStorageService.Object,
                _cache,
                _mockOcrService.Object);
        }

        #region UploadDocumentAsync

        [Fact]
        public async Task UTCID01_UploadDocumentAsync_FileNull_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), "BIRTH_CERTIFICATE", null!));
            ex.ErrorCode.Should().Be("FILE_MISSING");
        }

        [Fact]
        public async Task UTCID02_UploadDocumentAsync_FileExceedsLimit_ThrowsBadRequestException()
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), "BIRTH_CERTIFICATE", mockFile.Object));
            ex.ErrorCode.Should().Be("FILE_SIZE_EXCEEDED");
        }

        [Fact]
        public async Task UTCID03_UploadDocumentAsync_InvalidExtension_ThrowsBadRequestException()
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("document.exe");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), "BIRTH_CERTIFICATE", mockFile.Object));
            ex.ErrorCode.Should().Be("INVALID_FILE_TYPE");
        }

        [Fact]
        public async Task UTCID04_UploadDocumentAsync_InvalidDocType_ThrowsBadRequestException()
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("document.pdf");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), "INVALID_TYPE", mockFile.Object));
            ex.ErrorCode.Should().Be("INVALID_DOC_TYPE");
        }

        [Fact]
        public async Task UTCID05_UploadDocumentAsync_DependentNotFound_ThrowsNotFoundException()
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("document.pdf");

            _mockDependentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Dependent?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.UploadDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), "BIRTH_CERTIFICATE", mockFile.Object));
        }

        [Fact]
        public async Task UTCID06_UploadDocumentAsync_ForbiddenUser_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var anotherUserId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("document.pdf");

            var dependent = new Dependent { Id = depId, TaxpayerId = anotherUserId };
            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.UploadDocumentAsync(userId, depId, "BIRTH_CERTIFICATE", mockFile.Object));
        }

        [Fact]
        public async Task UTCID07_UploadDocumentAsync_Valid_SavesDocumentAndReturnsDto()
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("birth_cert.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/dependents/cert.pdf");

            var docsInDb = new List<DependentDocument>();
            _mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                        .ReturnsAsync(() => docsInDb);
            _mockDocRepo.Setup(r => r.AddAsync(It.IsAny<DependentDocument>()))
                        .Callback<DependentDocument>(d => docsInDb.Add(d))
                        .Returns(Task.CompletedTask);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                        .ReturnsAsync(new List<DependentDocumentRule>
                        {
                            new DependentDocumentRule { DocType = "BIRTH_CERTIFICATE", IsMandatory = true, IsActive = true, TargetGroup = "CHILD_UNDER_18" }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _service.UploadDocumentAsync(userId, depId, "BIRTH_CERTIFICATE", mockFile.Object);

            result.Should().NotBeNull();
            result.Document.FileUrl.Should().Be("https://storage/dependents/cert.pdf");
            result.IsProfileComplete.Should().BeTrue();
            _mockDocRepo.Verify(r => r.AddAsync(It.IsAny<DependentDocument>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID07b_UploadDocumentAsync_ExistingDocument_UpdatesExistingAndCleansDuplicates()
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("cccd_new.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            var existingDoc1 = new DependentDocument
            {
                Id = Guid.NewGuid(),
                DependentId = depId,
                DocType = DocumentType.CITIZEN_ID,
                FileUrl = "https://storage/old1.pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-2)
            };
            var existingDoc2 = new DependentDocument
            {
                Id = Guid.NewGuid(),
                DependentId = depId,
                DocType = DocumentType.CITIZEN_ID,
                FileUrl = "https://storage/old2.pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-1)
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/new_cccd.pdf");

            _mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                        .ReturnsAsync(new List<DependentDocument> { existingDoc1, existingDoc2 });

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                        .ReturnsAsync(new List<DependentDocumentRule>
                        {
                            new DependentDocumentRule { DocType = "CITIZEN_ID", IsMandatory = true, IsActive = true, TargetGroup = "CHILD_UNDER_18" }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _service.UploadDocumentAsync(userId, depId, "CITIZEN_ID", mockFile.Object);

            result.Should().NotBeNull();
            result.Document.FileUrl.Should().Be("https://storage/new_cccd.pdf");
            result.Document.Id.Should().Be(existingDoc1.Id);
            _mockDocRepo.Verify(r => r.Update(existingDoc1), Times.Once);
            _mockDocRepo.Verify(r => r.Remove(existingDoc2), Times.Once);
            _mockDocRepo.Verify(r => r.AddAsync(It.IsAny<DependentDocument>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID08_UploadDocumentAsync_DatabaseException_RollsBackTransaction()
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("birth_cert.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/dependents/cert.pdf");

            _mockDocRepo.Setup(r => r.AddAsync(It.IsAny<DependentDocument>()))
                        .ThrowsAsync(new InvalidOperationException("DB Failure"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UploadDocumentAsync(userId, depId, "BIRTH_CERTIFICATE", mockFile.Object));

            _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID09_UploadDocumentAsync_ChildUnder18_MissingMandatory_StatusPendingDocuments()
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("doc.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsProfileComplete = true,
                Status = DependentStatus.ACTIVE
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/dependents/doc.pdf");

            _mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                        .ReturnsAsync(new List<DependentDocument>
                        {
                            new DependentDocument { DocType = DocumentType.OTHER }
                        });

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                        .ReturnsAsync(new List<DependentDocumentRule>
                        {
                            new DependentDocumentRule { DocType = "BIRTH_CERTIFICATE", IsMandatory = true, IsActive = true, TargetGroup = "CHILD_UNDER_18" },
                            new DependentDocumentRule { DocType = "STUDENT_CARD", IsMandatory = true, IsActive = true, TargetGroup = "CHILD_UNDER_18" }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _service.UploadDocumentAsync(userId, depId, "OTHER", mockFile.Object);

            result.Should().NotBeNull();
            result.IsProfileComplete.Should().BeFalse();
            result.MissingDocuments.Should().Contain("BIRTH_CERTIFICATE_OR_CITIZEN_ID");
            result.MissingDocuments.Should().Contain("STUDENT_CARD");
            dependent.Status.Should().Be(DependentStatus.PENDING_DOCUMENTS);
        }

        [Fact]
        public async Task UTCID10_UploadDocumentAsync_NonChildGroup_WithMandatoryRules()
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("cccd.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = DependentGroup.SPOUSE_DISABLED,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/dependents/cccd.pdf");

            _mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                        .ReturnsAsync(new List<DependentDocument>
                        {
                            new DependentDocument { DocType = DocumentType.CITIZEN_ID }
                        });

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                        .ReturnsAsync(new List<DependentDocumentRule>
                        {
                            new DependentDocumentRule { DocType = "CITIZEN_ID", IsMandatory = true, IsActive = true, TargetGroup = "SPOUSE_DISABLED" },
                            new DependentDocumentRule { DocType = "DISABILITY_CERTIFICATE", IsMandatory = true, IsActive = true, TargetGroup = "SPOUSE_DISABLED" }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _service.UploadDocumentAsync(userId, depId, "CITIZEN_ID", mockFile.Object);

            result.Should().NotBeNull();
            result.IsProfileComplete.Should().BeFalse();
            result.MissingDocuments.Should().Contain("DISABILITY_CERTIFICATE");
        }

        [Theory]
        [InlineData(DependentGroup.CHILD_OVER_18_DISABLED, DocumentType.CITIZEN_ID, "DISABILITY_CERTIFICATE")]
        [InlineData(DependentGroup.CHILD_OVER_18_STUDYING, DocumentType.CITIZEN_ID, "STUDENT_CARD")]
        [InlineData(DependentGroup.SPOUSE_DISABLED, DocumentType.CITIZEN_ID, "MARRIAGE_CERTIFICATE")]
        [InlineData(DependentGroup.SPOUSE_RETIRED, DocumentType.CITIZEN_ID, "MARRIAGE_CERTIFICATE")]
        [InlineData(DependentGroup.PARENT_DISABLED, DocumentType.CITIZEN_ID, "RELATIONSHIP_CERTIFICATE")]
        [InlineData(DependentGroup.PARENT_RETIRED, DocumentType.CITIZEN_ID, "RELATIONSHIP_CERTIFICATE")]
        [InlineData(DependentGroup.OTHER_HELPLESS, DocumentType.CITIZEN_ID, "SUPPORT_COMMITMENT_FORM")]
        public async Task UTCID11_UploadDocumentAsync_FallbackRules_CoversAllFallbackGroups(DependentGroup group, DocumentType uploadedDoc, string expectedMissing)
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("file.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = group,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/dependents/file.pdf");

            _mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                        .ReturnsAsync(new List<DependentDocument>
                        {
                            new DependentDocument { DocType = uploadedDoc }
                        });

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                        .ReturnsAsync(new List<DependentDocumentRule>()); // Empty to trigger fallback
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _service.UploadDocumentAsync(userId, depId, uploadedDoc.ToString(), mockFile.Object);

            result.Should().NotBeNull();
            result.IsProfileComplete.Should().BeFalse();
            result.MissingDocuments.Should().Contain(expectedMissing);
        }

        [Fact]
        public async Task UTCID12_UploadDocumentAsync_ImagePng_Succeeds()
        {
            var userId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(500 * 1024);
            mockFile.Setup(f => f.FileName).Returns("certificate.png");
            mockFile.Setup(f => f.ContentType).Returns("image/png");

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = userId,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            _mockDependentRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockFileStorageService.Setup(f => f.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                                   .ReturnsAsync("https://storage/dependents/certificate.png");

            _mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                        .ReturnsAsync(new List<DependentDocument>
                        {
                            new DependentDocument { DocType = DocumentType.BIRTH_CERTIFICATE }
                        });

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                        .ReturnsAsync(new List<DependentDocumentRule>
                        {
                            new DependentDocumentRule { DocType = "BIRTH_CERTIFICATE", IsMandatory = true, IsActive = true, TargetGroup = "CHILD_UNDER_18" }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _service.UploadDocumentAsync(userId, depId, "BIRTH_CERTIFICATE", mockFile.Object);

            result.Should().NotBeNull();
            result.Document.FileMimeType.Should().Be("image/png");
            result.IsProfileComplete.Should().BeTrue();
            dependent.Status.Should().Be(DependentStatus.ACTIVE);
        }

        #endregion
    }
}
