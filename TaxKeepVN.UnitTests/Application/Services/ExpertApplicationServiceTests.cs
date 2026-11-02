using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.ExpertApplications;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ExpertApplicationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IFileStorageService> _mockFileStorage;
        private readonly Mock<ICertificateOcrProducerService> _mockCertProducer;
        private readonly Mock<ILogger<ExpertApplicationService>> _mockLogger;
        private readonly Mock<IGenericRepository<User>> _mockUserRepo;
        private readonly Mock<IGenericRepository<ExpertApplication>> _mockAppRepo;
        private readonly Mock<IGenericRepository<Specialization>> _mockSpecRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationSpecialization>> _mockAppSpecRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationFeeProposal>> _mockProposalRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationCertificate>> _mockCertRepo;
        private readonly Mock<IGenericRepository<ExpertCertificateConfiguration>> _mockCertConfigRepo;
        private readonly Mock<IGenericRepository<ConsultationFeeConfiguration>> _mockFeeConfigRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationAudit>> _mockAuditRepo;
        private readonly Mock<IGenericRepository<ExpertProfile>> _mockProfileRepo;
        private readonly Mock<IGenericRepository<UserWallet>> _mockWalletRepo;
        private readonly Mock<IGenericRepository<SystemNotification>> _mockNotifRepo;
        private readonly ExpertApplicationService _service;

        public ExpertApplicationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockFileStorage = new Mock<IFileStorageService>();
            _mockCertProducer = new Mock<ICertificateOcrProducerService>();
            _mockLogger = new Mock<ILogger<ExpertApplicationService>>();

            _mockUserRepo = new Mock<IGenericRepository<User>>();
            _mockAppRepo = new Mock<IGenericRepository<ExpertApplication>>();
            _mockSpecRepo = new Mock<IGenericRepository<Specialization>>();
            _mockAppSpecRepo = new Mock<IGenericRepository<ExpertApplicationSpecialization>>();
            _mockProposalRepo = new Mock<IGenericRepository<ExpertApplicationFeeProposal>>();
            _mockCertRepo = new Mock<IGenericRepository<ExpertApplicationCertificate>>();
            _mockCertConfigRepo = new Mock<IGenericRepository<ExpertCertificateConfiguration>>();
            _mockFeeConfigRepo = new Mock<IGenericRepository<ConsultationFeeConfiguration>>();
            _mockAuditRepo = new Mock<IGenericRepository<ExpertApplicationAudit>>();
            _mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            _mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            _mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();

            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplication>()).Returns(_mockAppRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(_mockSpecRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationSpecialization>()).Returns(_mockAppSpecRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationFeeProposal>()).Returns(_mockProposalRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationCertificate>()).Returns(_mockCertRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertCertificateConfiguration>()).Returns(_mockCertConfigRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationFeeConfiguration>()).Returns(_mockFeeConfigRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationAudit>()).Returns(_mockAuditRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(_mockProfileRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(_mockWalletRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(_mockNotifRepo.Object);

            // Default empty setups
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockAuditRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationAudit, bool>>>()))
                          .ReturnsAsync(new List<ExpertApplicationAudit>());
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet>());
            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile>());

            _service = new ExpertApplicationService(
                _mockUnitOfWork.Object,
                _mockFileStorage.Object,
                _mockCertProducer.Object,
                _mockLogger.Object);
        }

        #region SaveDraftAsync

        [Fact]
        public async Task UTCID01_SaveDraftAsync_UserNotFound_ThrowsNotFoundException()
        {
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.SaveDraftAsync(Guid.NewGuid(), new SaveExpertApplicationDraftRequest()));
        }

        [Fact]
        public async Task UTCID02_SaveDraftAsync_PendingAppExists_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyễn Văn Chuyên Gia" };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var pendingApp = new ExpertApplication { Id = Guid.NewGuid(), UserId = userId, Status = ExpertApplicationStatus.PendingReview };
            var appsList = new List<ExpertApplication> { pendingApp };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList.Where(expr.Compile()).ToList());

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SaveDraftAsync(userId, new SaveExpertApplicationDraftRequest()));
            ex.ErrorCode.Should().Be("ACTIVE_APPLICATION_EXISTS");
        }

        [Fact]
        public async Task UTCID04_SaveDraftAsync_NameMismatch_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyễn Văn A" };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication>());

            var request = new SaveExpertApplicationDraftRequest { FullName = "Trần Thị B" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SaveDraftAsync(userId, request));
            ex.ErrorCode.Should().Be("FULLNAME_MISMATCH");
        }

        [Fact]
        public async Task UTCID05_SaveDraftAsync_ValidNewDraft_CreatesDraftSuccessfully()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyễn Văn A" };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication>());

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization> { spec });

            var request = new SaveExpertApplicationDraftRequest
            {
                FullName = "Nguyễn Văn A",
                JobTitle = "Chuyên viên tư vấn thuế",
                CompanyName = "Công ty TNHH Thuế",
                YearsOfExperience = 5,
                ExperienceDescription = "5 năm kinh nghiệm hoàn thuế",
                SpecializationIds = new List<int> { 1 },
                FeeProposals = new List<FeeProposalRequestDto>
                {
                    new FeeProposalRequestDto { SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, ProposedFee = 500000m }
                }
            };

            var result = await _service.SaveDraftAsync(userId, request);

            result.Should().NotBeNull();
            result.FullName.Should().Be("Nguyễn Văn A");
            result.Status.Should().Be(ExpertApplicationStatus.Draft);
            _mockAppRepo.Verify(r => r.AddAsync(It.IsAny<ExpertApplication>()), Times.Once);
            _mockAppSpecRepo.Verify(r => r.AddAsync(It.IsAny<ExpertApplicationSpecialization>()), Times.Once);
            _mockProposalRepo.Verify(r => r.AddAsync(It.IsAny<ExpertApplicationFeeProposal>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UploadAvatarAsync

        [Fact]
        public async Task UTCID06_UploadAvatarAsync_InvalidExtension_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("avatar.exe");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadAvatarAsync(userId, new UploadAvatarRequest { File = mockFile.Object }));
            ex.ErrorCode.Should().Be("INVALID_FILE_TYPE");
        }

        [Fact]
        public async Task UTCID07_UploadAvatarAsync_Valid_UploadsAndReturnsUrl()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.FileName).Returns("avatar.png");
            _mockFileStorage.Setup(s => s.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                            .ReturnsAsync("https://taxkeep.vn/avatars/user.png");

            var app = new ExpertApplication { Id = Guid.NewGuid(), UserId = userId, Status = ExpertApplicationStatus.Draft };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var result = await _service.UploadAvatarAsync(userId, new UploadAvatarRequest { File = mockFile.Object });

            result.Should().NotBeNull();
            result.AvatarUrl.Should().Be("https://taxkeep.vn/avatars/user.png");
            app.AvatarUrl.Should().Be("https://taxkeep.vn/avatars/user.png");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UploadCertificateAsync & DeleteCertificateAsync

        [Fact]
        public async Task UTCID08_UploadCertificateAsync_InvalidCertType_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            _mockCertConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                               .ReturnsAsync(new List<ExpertCertificateConfiguration>());

            var request = new UploadCertificateRequest { CertificateType = "UNKNOWN_CERT" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadCertificateAsync(userId, request));
            ex.ErrorCode.Should().Be("INVALID_CERTIFICATE_TYPE");
        }

        [Fact]
        public async Task UTCID09_UploadCertificateAsync_Valid_SavesAndPublishesOcr()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var certConfig = new ExpertCertificateConfiguration
            {
                Code = "CPA",
                Name = "Chứng chỉ CPA",
                IsActive = true,
                RequiresExpiryDate = false
            };
            _mockCertConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                               .ReturnsAsync(new List<ExpertCertificateConfiguration> { certConfig });

            var app = new ExpertApplication { Id = Guid.NewGuid(), UserId = userId, Status = ExpertApplicationStatus.Draft };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var mockFile = new Mock<IFormFile>();
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));
            mockFile.Setup(f => f.Length).Returns(stream.Length);
            mockFile.Setup(f => f.FileName).Returns("cpa.pdf");
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
                    .Returns((Stream target, System.Threading.CancellationToken token) => stream.CopyToAsync(target, token));

            _mockFileStorage.Setup(s => s.SaveFileAsync(mockFile.Object, It.IsAny<string>()))
                            .ReturnsAsync("https://taxkeep.vn/certs/cpa.pdf");

            var request = new UploadCertificateRequest
            {
                CertificateType = "CPA",
                CertificateName = "Chứng chỉ CPA Việt Nam",
                CertificateNumber = "CPA-12345",
                IssueDate = new DateOnly(2022, 1, 1),
                HasExpiry = false,
                File = mockFile.Object
            };

            var result = await _service.UploadCertificateAsync(userId, request);

            result.Should().NotBeNull();
            result.CertificateNumber.Should().Be("CPA-12345");
            _mockCertRepo.Verify(r => r.AddAsync(It.IsAny<ExpertApplicationCertificate>()), Times.Once);
            _mockCertProducer.Verify(p => p.PublishCertificateOcrTask(It.IsAny<CertificateOcrRequestMessage>()), Times.Once);
        }

        [Fact]
        public async Task UTCID10_DeleteCertificateAsync_Valid_DeletesCertificate()
        {
            var userId = Guid.NewGuid();
            var certId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var cert = new ExpertApplicationCertificate { Id = certId, ApplicationId = appId, FileUrl = "https://taxkeep.vn/cert.pdf" };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var app = new ExpertApplication { Id = appId, UserId = userId, Status = ExpertApplicationStatus.Draft };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            await _service.DeleteCertificateAsync(userId, certId);

            _mockFileStorage.Verify(s => s.DeleteFileAsync(cert.FileUrl), Times.Once);
            _mockCertRepo.Verify(r => r.Remove(cert), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region SubmitApplicationAsync & GetMyApplicationAsync

        [Fact]
        public async Task UTCID11_SubmitApplicationAsync_MissingFields_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var app = new ExpertApplication { Id = Guid.NewGuid(), UserId = userId, Status = ExpertApplicationStatus.Draft, FullName = "" };
            var appsList11 = new List<ExpertApplication> { app };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList11.Where(expr.Compile()).ToList());

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitApplicationAsync(userId));
            ex.ErrorCode.Should().Be("MISSING_REQUIRED_FIELDS");
        }

        [Fact]
        public async Task UTCID12_SubmitApplicationAsync_Valid_SubmitsAndCreatesAudit()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var appId = Guid.NewGuid();
            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                FullName = "Nguyen Van A",
                JobTitle = "Chuyên viên thuế",
                ExperienceDescription = "Kinh nghiệm 5 năm tư vấn thuế TNCN",
                Status = ExpertApplicationStatus.Draft
            };
            var appsList12 = new List<ExpertApplication> { app };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList12.Where(expr.Compile()).ToList());

            var appSpec = new ExpertApplicationSpecialization { ApplicationId = appId, SpecializationId = 1 };
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization> { appSpec });

            var cert = new ExpertApplicationCertificate { Id = Guid.NewGuid(), ApplicationId = appId, HasExpiry = false };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var prop = new ExpertApplicationFeeProposal { Id = Guid.NewGuid(), ApplicationId = appId, ProposedFee = 500000m, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60 };
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal> { prop });

            var feeConfig = new ConsultationFeeConfiguration { SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, MinFee = 100000m, MaxFee = 1000000m, IsActive = true };
            _mockFeeConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                              .ReturnsAsync(new List<ConsultationFeeConfiguration> { feeConfig });

            var result = await _service.SubmitApplicationAsync(userId);

            result.Should().NotBeNull();
            app.Status.Should().Be(ExpertApplicationStatus.PendingReview);
            _mockAuditRepo.Verify(r => r.AddAsync(It.Is<ExpertApplicationAudit>(a => a.Action == ApplicationAuditAction.Submitted)), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID13_GetMyApplicationAsync_Valid_ReturnsDetail()
        {
            var userId = Guid.NewGuid();
            var app = new ExpertApplication { Id = Guid.NewGuid(), UserId = userId, FullName = "Nguyen Van A" };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var result = await _service.GetMyApplicationAsync(userId);

            result.Should().NotBeNull();
            result!.FullName.Should().Be("Nguyen Van A");
        }

        #endregion

        #region Review & Admin Methods

        [Fact]
        public async Task UTCID03_GetApplicationByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockAppRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                        .ReturnsAsync((ExpertApplication?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetApplicationByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID14_GetApplicationsAsync_ReturnsPagedApplications()
        {
            var app = new ExpertApplication { Id = Guid.NewGuid(), FullName = "Nguyen Van A", ApplicationNumber = "EXP-202501-1234", Status = ExpertApplicationStatus.PendingReview };
            _mockAppRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertApplication> { app });

            var query = new AdminExpertApplicationQueryParameters { Page = 1, Size = 10 };
            var result = await _service.GetApplicationsAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Pagination.TotalItems.Should().Be(1);
        }

        [Fact]
        public async Task UTCID15_VerifyCertificateAsync_Valid_UpdatesCertificateStatus()
        {
            var adminId = Guid.NewGuid();
            var appId = Guid.NewGuid();
            var certId = Guid.NewGuid();

            var app = new ExpertApplication { Id = appId };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var cert = new ExpertApplicationCertificate { Id = certId, ApplicationId = appId, VerificationStatus = CertificateVerificationStatus.PendingVerification };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var request = new VerifyCertificateRequest
            {
                VerificationStatus = CertificateVerificationStatus.Verified,
                VerificationNote = "Đã đối chiếu cổng Bộ Tài Chính",
                VerificationSource = "mof.gov.vn"
            };

            var result = await _service.VerifyCertificateAsync(adminId, appId, certId, request);

            result.Should().NotBeNull();
            cert.VerificationStatus.Should().Be(CertificateVerificationStatus.Verified);
            cert.VerifiedBy.Should().Be(adminId);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID16_ApproveApplicationAsync_Valid_ApprovesAndCreatesProfile()
        {
            var adminId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                FullName = "Nguyen Van A",
                JobTitle = "Chuyên gia thuế",
                Status = ExpertApplicationStatus.PendingReview,
                ApplicationNumber = "EXP-202501-9999"
            };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var user = new User { UserId = userId, UserRole = "taxpayer", RoleId = 2 };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var cert = new ExpertApplicationCertificate { Id = Guid.NewGuid(), ApplicationId = appId, HasExpiry = false, VerificationStatus = CertificateVerificationStatus.PendingVerification };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var result = await _service.ApproveApplicationAsync(adminId, appId, new ApproveExpertApplicationRequest { Note = "Hồ sơ xuất sắc" });

            result.Should().NotBeNull();
            app.Status.Should().Be(ExpertApplicationStatus.Approved);
            user.UserRole.Should().Be("expert");
            user.RoleId.Should().Be(3);
            cert.VerificationStatus.Should().Be(CertificateVerificationStatus.Verified);
            _mockProfileRepo.Verify(r => r.AddAsync(It.Is<ExpertProfile>(p => p.UserId == userId)), Times.Once);
            _mockWalletRepo.Verify(r => r.AddAsync(It.Is<UserWallet>(w => w.UserId == userId && w.WalletType == "EXPERT")), Times.Once);
            _mockNotifRepo.Verify(r => r.AddAsync(It.IsAny<SystemNotification>()), Times.Once);
            _mockAuditRepo.Verify(r => r.AddAsync(It.Is<ExpertApplicationAudit>(a => a.Action == ApplicationAuditAction.Approved)), Times.Once);
        }

        [Fact]
        public async Task UTCID17_RejectApplicationAsync_Valid_RejectsApplication()
        {
            var adminId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                Status = ExpertApplicationStatus.PendingReview,
                ApplicationNumber = "EXP-202501-REJ"
            };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var request = new RejectExpertApplicationRequest { Reason = "Kinh nghiệm thực tế chưa đủ 3 năm theo tiêu chuẩn." };
            var result = await _service.RejectApplicationAsync(adminId, appId, request);

            result.Should().NotBeNull();
            app.Status.Should().Be(ExpertApplicationStatus.Rejected);
            app.RejectionReason.Should().Be("Kinh nghiệm thực tế chưa đủ 3 năm theo tiêu chuẩn.");
            _mockAuditRepo.Verify(r => r.AddAsync(It.Is<ExpertApplicationAudit>(a => a.Action == ApplicationAuditAction.Rejected)), Times.Once);
            _mockNotifRepo.Verify(r => r.AddAsync(It.IsAny<SystemNotification>()), Times.Once);
        }

        [Fact]
        public async Task UTCID18_RequestSupplementAsync_Valid_SetsNeedSupplement()
        {
            var adminId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                Status = ExpertApplicationStatus.PendingReview,
                ApplicationNumber = "EXP-202501-SUPP"
            };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var request = new RequestSupplementRequest { Reason = "Vui lòng chụp lại ảnh chứng chỉ CPA có đầy đủ dấu đỏ." };
            var result = await _service.RequestSupplementAsync(adminId, appId, request);

            result.Should().NotBeNull();
            app.Status.Should().Be(ExpertApplicationStatus.NeedSupplement);
            app.SupplementRequestReason.Should().Be("Vui lòng chụp lại ảnh chứng chỉ CPA có đầy đủ dấu đỏ.");
            _mockAuditRepo.Verify(r => r.AddAsync(It.Is<ExpertApplicationAudit>(a => a.Action == ApplicationAuditAction.SupplementRequested)), Times.Once);
            _mockNotifRepo.Verify(r => r.AddAsync(It.IsAny<SystemNotification>()), Times.Once);
        }

        #endregion

        #region Extra Edge Cases for ExpertApplicationService

        [Fact]
        public async Task UTCID19_SaveDraftAsync_UpdateExistingDraft_Success()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyễn Văn A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var existingApp = new ExpertApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = ExpertApplicationStatus.Draft,
                FullName = "Nguyễn Văn A"
            };
            var appsList = new List<ExpertApplication> { existingApp };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList.Where(expr.Compile()).ToList());

            var request = new SaveExpertApplicationDraftRequest
            {
                FullName = "Nguyễn Văn A",
                JobTitle = "Giám đốc tư vấn thuế"
            };

            var result = await _service.SaveDraftAsync(userId, request);

            result.Should().NotBeNull();
            existingApp.JobTitle.Should().Be("Giám đốc tư vấn thuế");
            _mockAppRepo.Verify(r => r.Update(existingApp), Times.Once);
        }

        [Fact]
        public async Task UTCID20_SaveDraftAsync_InvalidSpecialization_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyễn Văn A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication>());

            // Only spec 1 is active, request asks for spec 99
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, IsActive = true } });

            var request = new SaveExpertApplicationDraftRequest
            {
                SpecializationIds = new List<int> { 99 }
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SaveDraftAsync(userId, request));
            ex.ErrorCode.Should().Be("INVALID_SPECIALIZATION");
        }

        [Fact]
        public async Task UTCID21_UploadCertificateAsync_ExpiryValidationFailures_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var certConfig = new ExpertCertificateConfiguration
            {
                Code = "EXP_REQUIRED",
                Name = "Cert",
                IsActive = true,
                RequiresExpiryDate = true
            };
            _mockCertConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                               .ReturnsAsync(new List<ExpertCertificateConfiguration> { certConfig });

            // 1. RequiresExpiryDate but HasExpiry = false
            var req1 = new UploadCertificateRequest { CertificateType = "EXP_REQUIRED", HasExpiry = false };
            var ex1 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadCertificateAsync(userId, req1));
            ex1.ErrorCode.Should().Be("EXPIRY_REQUIRED_FOR_TYPE");

            // 2. IssueDate in future
            var req2 = new UploadCertificateRequest
            {
                CertificateType = "EXP_REQUIRED",
                HasExpiry = true,
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
            };
            var ex2 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadCertificateAsync(userId, req2));
            ex2.ErrorCode.Should().Be("INVALID_ISSUE_DATE");

            // 3. ExpiryDate missing
            var req3 = new UploadCertificateRequest
            {
                CertificateType = "EXP_REQUIRED",
                HasExpiry = true,
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                ExpiryDate = null
            };
            var ex3 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadCertificateAsync(userId, req3));
            ex3.ErrorCode.Should().Be("EXPIRY_DATE_REQUIRED");

            // 4. ExpiryDate expired
            var req4 = new UploadCertificateRequest
            {
                CertificateType = "EXP_REQUIRED",
                HasExpiry = true,
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100)),
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
            };
            var ex4 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UploadCertificateAsync(userId, req4));
            ex4.ErrorCode.Should().Be("CERTIFICATE_EXPIRED");
        }

        [Fact]
        public async Task UTCID22_DeleteCertificateAsync_NotFound_ThrowsNotFoundException()
        {
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.DeleteCertificateAsync(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID23_DeleteCertificateAsync_ForbiddenUser_ThrowsUnauthorizedException()
        {
            var userId = Guid.NewGuid();
            var strangerId = Guid.NewGuid();
            var certId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var cert = new ExpertApplicationCertificate { Id = certId, ApplicationId = appId };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var app = new ExpertApplication { Id = appId, UserId = userId, Status = ExpertApplicationStatus.Draft };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                _service.DeleteCertificateAsync(strangerId, certId));
        }

        [Fact]
        public async Task UTCID24_DeleteCertificateAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var certId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var cert = new ExpertApplicationCertificate { Id = certId, ApplicationId = appId };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var app = new ExpertApplication { Id = appId, UserId = userId, Status = ExpertApplicationStatus.PendingReview };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.DeleteCertificateAsync(userId, certId));
            ex.ErrorCode.Should().Be("CANNOT_MODIFY");
        }

        [Fact]
        public async Task UTCID25_SubmitApplicationAsync_NoSpecialization_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var app = new ExpertApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FullName = "Nguyen Van A",
                JobTitle = "Chuyên viên",
                ExperienceDescription = "5 năm",
                Status = ExpertApplicationStatus.Draft
            };
            var appsList = new List<ExpertApplication> { app };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList.Where(expr.Compile()).ToList());

            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitApplicationAsync(userId));
            ex.ErrorCode.Should().Be("SPECIALIZATION_REQUIRED");
        }

        [Fact]
        public async Task UTCID26_SubmitApplicationAsync_NoCertificate_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var appId = Guid.NewGuid();
            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                FullName = "Nguyen Van A",
                JobTitle = "Chuyên viên",
                ExperienceDescription = "5 năm",
                Status = ExpertApplicationStatus.Draft
            };
            var appsList = new List<ExpertApplication> { app };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList.Where(expr.Compile()).ToList());

            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization> { new ExpertApplicationSpecialization { ApplicationId = appId, SpecializationId = 1 } });

            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitApplicationAsync(userId));
            ex.ErrorCode.Should().Be("CERTIFICATE_REQUIRED");
        }

        [Fact]
        public async Task UTCID27_SubmitApplicationAsync_FeeOutOfRange_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var appId = Guid.NewGuid();
            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                FullName = "Nguyen Van A",
                JobTitle = "Chuyên viên",
                ExperienceDescription = "5 năm",
                Status = ExpertApplicationStatus.Draft
            };
            var appsList = new List<ExpertApplication> { app };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync((Expression<Func<ExpertApplication, bool>> expr) => appsList.Where(expr.Compile()).ToList());

            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization> { new ExpertApplicationSpecialization { ApplicationId = appId, SpecializationId = 1 } });

            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { new ExpertApplicationCertificate { Id = Guid.NewGuid(), ApplicationId = appId } });

            // Propose 10,000,000 when MaxFee is 1,000,000
            var prop = new ExpertApplicationFeeProposal { ApplicationId = appId, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, ProposedFee = 10000000m };
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal> { prop });

            var feeConfig = new ConsultationFeeConfiguration { SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, MinFee = 100000m, MaxFee = 1000000m, IsActive = true };
            _mockFeeConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                              .ReturnsAsync(new List<ConsultationFeeConfiguration> { feeConfig });

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitApplicationAsync(userId));
            ex.ErrorCode.Should().Be("FEE_OUT_OF_RANGE");
        }

        [Fact]
        public async Task UTCID28_GetMyApplicationAsync_Null_ReturnsNull()
        {
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication>());

            var result = await _service.GetMyApplicationAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        #endregion
    }
}
