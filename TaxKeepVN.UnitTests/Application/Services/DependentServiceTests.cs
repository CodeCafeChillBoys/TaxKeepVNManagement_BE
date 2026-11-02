using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly DependentService _dependentService;

        public DependentServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCache = new Mock<IMemoryCache>();
            _mockConfig = new Mock<IConfiguration>();

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Setup Mock Configuration cho DependentSettings:GroupOrders:CHILD
            var mockChildSection = new Mock<IConfigurationSection>();
            var childUnder18 = new Mock<IConfigurationSection>();
            childUnder18.Setup(c => c.Key).Returns("CHILD_UNDER_18");
            var childOver18Studying = new Mock<IConfigurationSection>();
            childOver18Studying.Setup(c => c.Key).Returns("CHILD_OVER_18_STUDYING");
            var childOver18Disabled = new Mock<IConfigurationSection>();
            childOver18Disabled.Setup(c => c.Key).Returns("CHILD_OVER_18_DISABLED");

            mockChildSection.Setup(s => s.GetChildren()).Returns(new[] { childUnder18.Object, childOver18Studying.Object, childOver18Disabled.Object });
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD")).Returns(mockChildSection.Object);

            var secUnder18 = new Mock<IConfigurationSection>();
            secUnder18.Setup(s => s.Value).Returns("1");
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD:CHILD_UNDER_18")).Returns(secUnder18.Object);

            var secStudying = new Mock<IConfigurationSection>();
            secStudying.Setup(s => s.Value).Returns("2");
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD:CHILD_OVER_18_STUDYING")).Returns(secStudying.Object);

            var secDisabled = new Mock<IConfigurationSection>();
            secDisabled.Setup(s => s.Value).Returns("3");
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD:CHILD_OVER_18_DISABLED")).Returns(secDisabled.Object);

            _dependentService = new DependentService(
                _mockUnitOfWork.Object,
                _mockCache.Object,
                _mockConfig.Object
            );
        }

        #region FN_DEP_01: CreateDependentAsync

        [Fact]
        public async Task UTCID01_CreateDependentAsync_InvalidRelationship_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "INVALID_TYPE",
                CurrentGroup = "CHILD_UNDER_18"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_RELATIONSHIP");
        }

        [Fact]
        public async Task UTCID02_CreateDependentAsync_InvalidCurrentGroup_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "INVALID_GROUP"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_CURRENT_GROUP");
        }

        [Fact]
        public async Task UTCID03_CreateDependentAsync_MissingCitizenIdAndBirthCert_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = null,
                BirthCertNumber = ""
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("IDENTIFIER_REQUIRED");
        }

        [Fact]
        public async Task UTCID04_CreateDependentAsync_EffectiveFromGreaterThanTo_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-12",
                EffectiveToMonth = "2024-01"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_PERIOD");
        }

        [Fact]
        public async Task UTCID05_CreateDependentAsync_GroupRelationshipMismatch_ThrowsBadRequestException()
        {
            var emptySection = new Mock<IConfigurationSection>();
            emptySection.Setup(s => s.GetChildren()).Returns(new List<IConfigurationSection>());
            _mockConfig.Setup(c => c.GetSection(It.Is<string>(s => s.Contains("SPOUSE")))).Returns(emptySection.Object);

            var request = new CreateDependentRequest
            {
                Relationship = "SPOUSE",
                CurrentGroup = "SPOUSE_RETIRED"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("GROUP_RELATIONSHIP_MISMATCH");
        }

        [Fact]
        public async Task UTCID06_CreateDependentAsync_CitizenIdOverlap_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-06",
                EffectiveToMonth = "2024-12"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>
                {
                    new Dependent
                    {
                        CitizenId = "079201009999",
                        EffectiveFromMonth = "2024-01",
                        EffectiveToMonth = "2024-08"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(taxpayerId, request));
            ex.ErrorCode.Should().Be("DEPENDENT_PERIOD_OVERLAP");
        }

        [Fact]
        public async Task UTCID07_CreateDependentAsync_BirthCertOverlap_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Em",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthCertNumber = "KS-999999",
                EffectiveFromMonth = "2024-05",
                EffectiveToMonth = "2024-10"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>
                {
                    new Dependent
                    {
                        BirthCertNumber = "KS-999999",
                        EffectiveFromMonth = "2024-01",
                        EffectiveToMonth = "2024-06"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(taxpayerId, request));
            ex.ErrorCode.Should().Be("DEPENDENT_PERIOD_OVERLAP");
        }

        [Fact]
        public async Task UTCID08_CreateDependentAsync_ValidData_Success()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                BirthDate = new DateTime(2015, 5, 20),
                EffectiveFromMonth = "2024-01",
                EffectiveToMonth = "2024-12",
                Note = "Ghi chú hợp lệ"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>
                {
                    new DependentDocumentRule { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", IsMandatory = true, IsActive = true }
                });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _dependentService.CreateDependentAsync(taxpayerId, request);

            result.Should().NotBeNull();
            result.FullName.Should().Be(request.FullName);
            result.Relationship.Should().Be("CHILD");
            result.CurrentGroup.Should().Be("CHILD_UNDER_18");
            result.RequiredDocuments.Should().Contain("BIRTH_CERTIFICATE");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region FN_DEP_02: GetDependentsAsync

        [Fact]
        public async Task UTCID09_GetDependentsAsync_FilterAndSort_ReturnsPagedResult()
        {
            var taxpayerId = Guid.NewGuid();
            var query = new DependentQueryParameters
            {
                Status = "PENDING_DOCUMENTS",
                Relationship = "CHILD",
                Search = "Nguyen",
                Sort = "-fullname",
                Page = 1,
                Size = 10
            };

            var dependents = new List<Dependent>
            {
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = taxpayerId,
                    FullName = "Nguyen Van B",
                    Relationship = DependentRelationship.CHILD,
                    CurrentGroup = DependentGroup.CHILD_UNDER_18,
                    Status = DependentStatus.PENDING_DOCUMENTS,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = taxpayerId,
                    FullName = "Nguyen Van A",
                    Relationship = DependentRelationship.CHILD,
                    CurrentGroup = DependentGroup.CHILD_UNDER_18,
                    Status = DependentStatus.PENDING_DOCUMENTS,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(dependents);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var result = await _dependentService.GetDependentsAsync(taxpayerId, query);

            result.Should().NotBeNull();
            result.Pagination.TotalItems.Should().Be(2);
            result.Items.First().FullName.Should().Be("Nguyen Van B"); // Descending sort
        }

        #endregion

        #region FN_DEP_03: GetDependentByIdAsync

        [Fact]
        public async Task UTCID10_GetDependentByIdAsync_NotFoundOrDeleted_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.GetDependentByIdAsync(taxpayerId, depId));

            // Case deleted
            var deletedDep = new Dependent { Id = depId, TaxpayerId = taxpayerId, IsDeleted = true };
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(deletedDep);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.GetDependentByIdAsync(taxpayerId, depId));
        }

        [Fact]
        public async Task UTCID11_GetDependentByIdAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.GetDependentByIdAsync(taxpayerId, depId));
        }

        [Fact]
        public async Task UTCID12_GetDependentByIdAsync_Success_ReturnsDetail()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Status = DependentStatus.PENDING_DOCUMENTS,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>
                {
                    new DependentDocument
                    {
                        Id = Guid.NewGuid(),
                        DependentId = depId,
                        DocType = DocumentType.BIRTH_CERTIFICATE,
                        FileUrl = "/uploads/documents/doc.pdf",
                        FileMimeType = "application/pdf",
                        IsReadable = true,
                        UploadedAt = DateTime.UtcNow
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _dependentService.GetDependentByIdAsync(taxpayerId, depId);

            result.Should().NotBeNull();
            result.DependentId.Should().Be(depId);
            result.Documents.Should().HaveCount(1);
        }

        #endregion

        #region FN_DEP_04: UpdateGroupAsync

        [Fact]
        public async Task UTCID13_UpdateGroupAsync_NotFound_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_OVER_18_STUDYING" }));
        }

        [Fact]
        public async Task UTCID14_UpdateGroupAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_OVER_18_STUDYING" }));
        }

        [Fact]
        public async Task UTCID15_UpdateGroupAsync_InvalidOrSameGroup_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            // Invalid group
            var ex1 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "INVALID_GROUP" }));
            ex1.ErrorCode.Should().Be("INVALID_GROUP");

            // Same group
            var ex2 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_UNDER_18" }));
            ex2.ErrorCode.Should().Be("SAME_GROUP");
        }

        [Fact]
        public async Task UTCID16_UpdateGroupAsync_DowngradeNotAllowed_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_OVER_18_STUDYING, // Order = 2
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            // New group: CHILD_UNDER_18 (Order = 1) -> 1 < 2
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_UNDER_18" }));
            ex.ErrorCode.Should().Be("GROUP_DOWNGRADE_NOT_ALLOWED");
        }

        [Fact]
        public async Task UTCID17_UpdateGroupAsync_Success_UpdatesAndNotifies()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18, // Order = 1
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
            {
                NewGroup = "CHILD_OVER_18_STUDYING",
                Note = "Chuyen sang dai hoc"
            });

            result.Should().NotBeNull();
            result.PreviousGroup.Should().Be("CHILD_UNDER_18");
            result.CurrentGroup.Should().Be("CHILD_OVER_18_STUDYING");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupAsync_SoftDeleted_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = true
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_OVER_18_STUDYING" }));
        }

        [Fact]
        public async Task UpdateGroupAsync_RelationshipMismatch_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "SPOUSE_DISABLED" }));
            ex.ErrorCode.Should().Be("GROUP_RELATIONSHIP_MISMATCH");
        }

        [Fact]
        public async Task UpdateGroupAsync_NullNote_PreservesExistingNote()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Note = "Ghi chu ban dau",
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
            {
                NewGroup = "CHILD_OVER_18_DISABLED",
                Note = null
            });

            result.Note.Should().Be("Ghi chu ban dau");
            dependent.Note.Should().Be("Ghi chu ban dau");
        }

        [Fact]
        public async Task UpdateGroupAsync_WithExistingDocuments_MapsDocumentsInResponse()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var existingDoc = new DependentDocument
            {
                Id = Guid.NewGuid(),
                DependentId = depId,
                DocType = DocumentType.BIRTH_CERTIFICATE,
                FileUrl = "https://storage.taxkeep.vn/docs/bc.pdf",
                FileMimeType = "application/pdf",
                IsReadable = true,
                UploadedAt = DateTime.UtcNow
            };

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument> { existingDoc });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
            {
                NewGroup = "CHILD_OVER_18_STUDYING"
            });

            result.Documents.Should().HaveCount(1);
            result.Documents[0].DocType.Should().Be("BIRTH_CERTIFICATE");
        }

        #endregion

        #region FN_DEP_05: DeleteDependentAsync

        [Fact]
        public async Task UTCID18_DeleteDependentAsync_NotFound_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest()));
        }

        [Fact]
        public async Task UTCID19_DeleteDependentAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest()));
        }

        [Fact]
        public async Task UTCID20_DeleteDependentAsync_Success_SoftDeletes()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsDeleted = false,
                Status = DependentStatus.ACTIVE
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "Da co thu nhap" });

            result.Should().NotBeNull();
            dependent.IsDeleted.Should().BeTrue();
            dependent.Status.Should().Be(DependentStatus.INACTIVE);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteDependentAsync_AlreadyDeleted_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = true
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest()));
        }

        [Fact]
        public async Task DeleteDependentAsync_NullOrWhitespaceReason_PreservesExistingNote()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                Note = "Ghi chu cu",
                IsDeleted = false,
                Status = DependentStatus.ACTIVE
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "   " });

            result.Should().NotBeNull();
            dependent.Note.Should().Be("Ghi chu cu");
            dependent.IsDeleted.Should().BeTrue();
            dependent.Status.Should().Be(DependentStatus.INACTIVE);
        }

        [Fact]
        public async Task DeleteDependentAsync_PendingOrRejectedStatus_DeactivatesSuccessfully()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsDeleted = false,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "Huy ho so" });

            result.Should().NotBeNull();
            dependent.Status.Should().Be(DependentStatus.INACTIVE);
            dependent.IsDeleted.Should().BeTrue();
            dependent.Note.Should().Be("[Vô hiệu hóa] Huy ho so");
        }

        [Fact]
        public async Task DeleteDependentAsync_BoundaryReasonLength_Success()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = DependentRelationship.CHILD,
                CurrentGroup = DependentGroup.CHILD_UNDER_18,
                IsDeleted = false,
                Status = DependentStatus.ACTIVE
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            // Boundary min: 1 char
            var res1 = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "A" });
            dependent.Note.Should().Be("[Vô hiệu hóa] A");

            // Boundary max: 500 chars
            dependent.IsDeleted = false; // reset for test
            var longReason = new string('X', 500);
            var res2 = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = longReason });
            dependent.Note.Should().Be($"[Vô hiệu hóa] {longReason}");
        }

        #endregion
    }
}
