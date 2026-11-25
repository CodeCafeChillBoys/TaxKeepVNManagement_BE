using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class OcrServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<OcrService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly OcrService _service;

        public OcrServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<OcrService>>();
            _mockHandler = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("http://ai-service.local")
            };

            _mockHttpClientFactory.Setup(f => f.CreateClient("TaxAIService")).Returns(_httpClient);

            _service = new OcrService(_mockHttpClientFactory.Object, _mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region ExtractDocumentAsync

        [Fact]
        public async Task UTCID01_ExtractDocumentAsync_FileNull_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ExtractDocumentAsync(null!));
            ex.ErrorCode.Should().Be("FILE_MISSING");
        }

        #endregion

        #region ProcessUserCccdOcrAsync

        [Fact]
        public async Task UTCID02_ProcessUserCccdOcrAsync_Unreadable_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = false,
                    UnreadableReason = "Mờ nhòe"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ProcessUserCccdOcrAsync(mockFile.Object));
            ex.ErrorCode.Should().Be("UNREADABLE_IMAGE");
        }

        [Fact]
        public async Task UTCID03_ProcessUserCccdOcrAsync_InvalidCccdLength_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    CitizenId = "12345" // Not 12 digits
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ProcessUserCccdOcrAsync(mockFile.Object));
            ex.ErrorCode.Should().Be("INVALID_CITIZEN_ID");
        }

        #endregion

        #region ValidateDependentDocumentAsync

        [Fact]
        public async Task UTCID04_ValidateDependentDocumentAsync_DocTypeMismatch_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "MARRIAGE_CERTIFICATE",
                    FullName = "Nguyễn Văn Con"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("doc.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object));
            ex.ErrorCode.Should().Be("DOC_TYPE_MISMATCH");
        }

        [Fact]
        public async Task UTCID05_ProcessUserCccdOcrAsync_Valid_ReturnsDto()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    CitizenId = "012345678901",
                    FullName = "Nguyễn Văn A"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var result = await _service.ProcessUserCccdOcrAsync(mockFile.Object);

            result.Should().NotBeNull();
            result.CitizenId.Should().Be("012345678901");
            result.IsAvailable.Should().BeTrue();
        }

        [Fact]
        public async Task UTCID06_ProcessDependentOcrAsync_Valid_ReturnsDto()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    CitizenId = "012345678901",
                    FullName = "Nguyễn Văn Con",
                    BirthDate = "2015-05-10"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockDepRepo = new Mock<IGenericRepository<Dependent>>();
            mockDepRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                       .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockDepRepo.Object);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("dep.jpg");

            var result = await _service.ProcessDependentOcrAsync(mockFile.Object);

            result.Should().NotBeNull();
            result.CitizenId.Should().Be("012345678901");
            result.FullName.Should().Be("Nguyễn Văn Con");
        }

        [Fact]
        public async Task UTCID07_ValidateDependentDocumentAsync_Valid_CompletesSuccessfully()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var act = () => _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID08_ValidateDependentDocumentAsync_Unreadable_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = false,
                    UnreadableReason = "Quá tối"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object));
            ex.ErrorCode.Should().Be("UNREADABLE_DOCUMENT");
        }

        [Fact]
        public async Task UTCID09_ValidateDependentDocumentAsync_IdentityMismatch_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Trần Thị B"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object));
            ex.ErrorCode.Should().Be("IDENTITY_MISMATCH");
        }

        [Fact]
        public async Task UTCID10_ValidateDependentDocumentAsync_DocTypeMismatch_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CCCD",
                    FullName = "Nguyễn Văn Con"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object));
            ex.ErrorCode.Should().Be("DOC_TYPE_MISMATCH");
        }

        [Fact]
        public async Task UTCID11_ValidateDependentDocumentAsync_CitizenIdMismatch_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CCCD",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "001200000001"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con", CitizenId = "001200000002" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.CITIZEN_ID, mockFile.Object));
            ex.ErrorCode.Should().Be("IDENTITY_MISMATCH");
        }

        [Fact]
        public async Task UTCID12_ValidateDependentDocumentAsync_BirthYearMismatch_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CCCD",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "001200000001",
                    BirthDate = "2005-01-01"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con", CitizenId = "001200000001", BirthDate = new DateTime(2006, 1, 1) };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.CITIZEN_ID, mockFile.Object));
            ex.ErrorCode.Should().Be("IDENTITY_MISMATCH");
        }

        [Fact]
        public async Task UTCID13_ExtractDocumentAsync_WithBackFile_Success()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CCCD",
                    FullName = "Nguyễn Văn A"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFront = new Mock<IFormFile>();
            mockFront.Setup(f => f.Length).Returns(100);
            mockFront.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFront.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFront.Setup(f => f.FileName).Returns("front.jpg");

            var mockBack = new Mock<IFormFile>();
            mockBack.Setup(f => f.Length).Returns(100);
            mockBack.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockBack.Setup(f => f.ContentType).Returns("image/jpeg");
            mockBack.Setup(f => f.FileName).Returns("back.jpg");

            var result = await _service.ExtractDocumentAsync(mockFront.Object, mockBack.Object);
            result.Should().NotBeNull();
            result.FullName.Should().Be("Nguyễn Văn A");
        }

        [Fact]
        public async Task UTCID14_ExtractDocumentAsync_AiServiceError_ThrowsBadGatewayException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("OCR Failure")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("doc.jpg");

            var ex = await Assert.ThrowsAsync<BadGatewayException>(() =>
                _service.ExtractDocumentAsync(mockFile.Object));
            ex.ErrorCode.Should().Be("AI_SERVICE_UNAVAILABLE");
        }

        [Fact]
        public async Task UTCID15_ValidateDependentDocumentAsync_ThresholdNotPassed_ThrowsBadRequestException()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    ThresholdValidation = new DependentThresholdValidationResultDto
                    {
                        IsPassedThreshold = false,
                        WarningMessage = "Độ tin cậy trích xuất không đạt ngưỡng."
                    }
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object));

            ex.ErrorCode.Should().Be("LOW_CONFIDENCE_DOCUMENT");
            ex.Message.Should().Contain("không đạt ngưỡng");
        }

        [Fact]
        public async Task UTCID16_ValidateDependentDocumentAsync_BirthCert_FatherMatchesTaxpayer_Success()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Cha",
                CitizenId = "001200000001"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    FatherFullName = "Nguyễn Văn Cha",
                    FatherIdNumber = "001200000001"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var act = () => _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID17_ValidateDependentDocumentAsync_BirthCert_MotherMatchesTaxpayer_Success()
        {
            var taxpayer = new User
            {
                FullName = "Lê Thị Mẹ",
                CitizenId = "001200000002"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    MotherFullName = "Lê Thị Mẹ",
                    MotherIdNumber = "001200000002"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var act = () => _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID18_ValidateDependentDocumentAsync_BirthCert_ParentNameMismatch_ThrowsRelationshipMismatch()
        {
            var taxpayer = new User
            {
                FullName = "Trần Văn Lạ",
                CitizenId = "001200000999"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    FatherFullName = "Nguyễn Văn Cha",
                    MotherFullName = "Lê Thị Mẹ"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("không trùng khớp với thông tin cha");
        }

        [Fact]
        public async Task UTCID19_ValidateDependentDocumentAsync_BirthCert_ParentIdMismatch_ThrowsRelationshipMismatch()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Cha",
                CitizenId = "001200000999" // Lệch CCCD
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    FatherFullName = "Nguyễn Văn Cha",
                    FatherIdNumber = "001200000001"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("birth_cert.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("Số định danh của cha");
        }

        [Fact]
        public async Task UTCID20_ValidateDependentDocumentAsync_MarriageCert_SpouseMatchesTaxpayer_Success()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Chồng"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "MARRIAGE_CERTIFICATE",
                    FullName = "Trần Thị Vợ",
                    SpouseFullName = "Nguyễn Văn Chồng"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("marriage.jpg");

            var dependent = new Dependent { FullName = "Trần Thị Vợ", Relationship = "SPOUSE" };

            var act = () => _service.ValidateDependentDocumentAsync(dependent, DocumentType.MARRIAGE_CERTIFICATE, mockFile.Object, taxpayer);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID21_ValidateDependentDocumentAsync_MarriageCert_SpouseMismatch_ThrowsRelationshipMismatch()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Chồng"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "MARRIAGE_CERTIFICATE",
                    FullName = "Trần Thị Vợ",
                    SpouseFullName = "Lê Văn Khác"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("marriage.jpg");

            var dependent = new Dependent { FullName = "Trần Thị Vợ", Relationship = "SPOUSE" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.MARRIAGE_CERTIFICATE, mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("không khớp");
        }

        [Fact]
        public async Task UTCID22_ValidateDependentDocumentAsync_TaxpayerBirthCert_DependentIsParent_Success()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Con",
                CitizenId = "001200000099"
            };

            // Người phụ thuộc là Cha (sinh năm 1960)
            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Cha",
                BirthDate = new DateTime(1960, 5, 20),
                CitizenId = "001060000123",
                Relationship = "PARENT"
            };

            // Khai sinh của NNT (sinh năm 1995) — Năm sinh khác nhưng KHÔNG được throw IDENTITY_MISMATCH
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    BirthDate = "1995-10-15",
                    FatherFullName = "Nguyễn Văn Cha",
                    FatherIdNumber = "001060000123"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("taxpayer_birth_cert.jpg");

            var act = () => _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID23_ValidateDependentDocumentAsync_TaxpayerBirthCert_DependentIsParent_FatherMismatch_ThrowsRelationshipMismatch()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Con"
            };

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Cha",
                Relationship = "PARENT"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    FatherFullName = "Trần Văn Lạ",
                    MotherFullName = "Lê Thị Khác"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("taxpayer_birth_cert.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("không trùng khớp với tên cha");
        }

        [Fact]
        public async Task UTCID24_ValidateDependentDocumentAsync_TaxpayerBirthCert_DependentIsParent_FatherIdMismatch_ThrowsRelationshipMismatch()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Con"
            };

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Cha",
                CitizenId = "001060000123",
                Relationship = "PARENT"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Con",
                    FatherFullName = "Nguyễn Văn Cha",
                    FatherIdNumber = "001060000999" // Lệch CCCD người phụ thuộc
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("taxpayer_birth_cert.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.BIRTH_CERTIFICATE, mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("Số định danh của cha");
        }

        [Fact]
        public async Task UTCID25_ValidateDependentDocumentAsync_MarriageCert_ParentInLaw_TaxpayerPresent_Success()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Con Rể"
            };

            // Người phụ thuộc là Mẹ vợ (PARENT)
            var dependent = new Dependent
            {
                FullName = "Nguyễn Thị Mẹ Vợ",
                Relationship = "PARENT"
            };

            // Giấy kết hôn của NNT và Vợ -> Chỉ cần NNT có mặt trên giấy kết hôn
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "MARRIAGE_CERTIFICATE",
                    FullName = "Nguyễn Văn Con Rể",
                    SpouseFullName = "Lê Thị Vợ"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("marriage.jpg");

            var act = () => _service.ValidateDependentDocumentAsync(dependent, DocumentType.MARRIAGE_CERTIFICATE, mockFile.Object, taxpayer);
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID26_ValidateDependentDocumentAsync_MarriageCert_ParentInLaw_TaxpayerMissing_ThrowsRelationshipMismatch()
        {
            var taxpayer = new User
            {
                FullName = "Nguyễn Văn Con Rể"
            };

            var dependent = new Dependent
            {
                FullName = "Nguyễn Thị Mẹ Vợ",
                Relationship = "PARENT"
            };

            // Giấy kết hôn không có tên NNT
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "MARRIAGE_CERTIFICATE",
                    FullName = "Trần Văn Lạ",
                    SpouseFullName = "Lê Thị Vợ"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("marriage.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, DocumentType.MARRIAGE_CERTIFICATE, mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("phải có thông tin của người nộp thuế");
        }

        [Fact]
        public async Task UTCID27_ValidateDependentDocumentAsync_IsPassedThresholdFalse_ThrowsLowConfidenceDocument()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CITIZEN_ID",
                    FullName = "Nguyễn Văn Con",
                    IsPassedThreshold = false
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object));

            ex.ErrorCode.Should().Be("LOW_CONFIDENCE_DOCUMENT");
        }

        [Fact]
        public async Task UTCID28_ValidateDependentDocumentAsync_DirectDocMissingFullName_ThrowsIdentityUnreadable()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CITIZEN_ID",
                    FullName = "" // Rỗng
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object));

            ex.ErrorCode.Should().Be("IDENTITY_UNREADABLE");
        }

        [Fact]
        public async Task UTCID29_ValidateDependentDocumentAsync_IdentityMismatch_MasksCitizenIdInMessage()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CITIZEN_ID",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "079123456789"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Con",
                CitizenId = "079987654321"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object));

            ex.ErrorCode.Should().Be("IDENTITY_MISMATCH");
            ex.Message.Should().Contain("0791****6789");
            ex.Message.Should().Contain("0799****4321");
            ex.Message.Should().NotContain("079123456789");
        }

        [Fact]
        public async Task UTCID30_ValidateDependentDocumentAsync_AiDown_ThrowsBadGatewayException()
        {
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent { FullName = "Nguyễn Văn Con" };

            var ex = await Assert.ThrowsAsync<BadGatewayException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object));

            ex.ErrorCode.Should().Be("AI_SERVICE_UNAVAILABLE");
            ex.Message.Should().NotContain("Connection refused");
        }

        [Fact]
        public async Task UTCID31_ValidateDependentDocumentAsync_RuleValidationMatchedDocType_Matches()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "UNKNOWN",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "079123456789",
                    BirthDate = "2000-01-01",
                    RuleValidation = new RuleValidationResultDto
                    {
                        IsMatchedRule = true,
                        MatchedDocType = "CITIZEN_ID"
                    }
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent 
            { 
                FullName = "Nguyễn Văn Con",
                CitizenId = "079123456789",
                BirthDate = new DateTime(2000, 1, 1)
            };

            // Should succeed without throwing DOC_TYPE_MISMATCH
            await _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object);
        }

        [Fact]
        public async Task UTCID32_ValidateDependentDocumentAsync_CitizenId_ExpiredBeforeEffectiveFrom_ThrowsDocumentExpired()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CITIZEN_ID",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "079123456789",
                    BirthDate = "2010-05-15",
                    ExpiryDate = "2025-12-31" // Hết hạn trước 2026-01
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cccd.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Con",
                CitizenId = "079123456789",
                BirthDate = new DateTime(2010, 5, 15),
                EffectiveFromMonth = "2026-01"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object));

            ex.ErrorCode.Should().Be("DOCUMENT_EXPIRED");
            ex.Message.Should().Contain("đã hết hạn");
        }

        [Fact]
        public async Task UTCID33_ValidateDependentDocumentAsync_CitizenId_Legacy9Digits_DefaultDisallowed_ThrowsIdentityMismatch()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CITIZEN_ID",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "123456789", // 9 số CMND cũ
                    BirthDate = "2010-05-15"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cmnd.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Con",
                CitizenId = "123456789",
                BirthDate = new DateTime(2010, 5, 15)
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object));

            ex.ErrorCode.Should().Be("IDENTITY_MISMATCH");
            ex.Message.Should().Contain("12 số hợp lệ");
        }

        [Fact]
        public async Task UTCID34_ValidateDependentDocumentAsync_CitizenId_Legacy9Digits_AllowedByParam_Success()
        {
            var configRepo = new Mock<IGenericRepository<SystemConfig>>();
            configRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                .ReturnsAsync(new List<SystemConfig>
                {
                    new SystemConfig { ConfigKey = "allowLegacy9Digits", ConfigValue = "true", IsActive = true }
                });
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns(configRepo.Object);

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CITIZEN_ID",
                    FullName = "Nguyễn Văn Con",
                    CitizenId = "123456789",
                    BirthDate = "2010-05-15"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("cmnd.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Con",
                CitizenId = "123456789",
                BirthDate = new DateTime(2010, 5, 15)
            };

            await _service.ValidateDependentDocumentAsync(dependent, "CITIZEN_ID", mockFile.Object);
        }

        [Fact]
        public async Task UTCID35_ValidateDependentDocumentAsync_ChildBirthCert_BirthCertNumberMismatch_ThrowsIdentityMismatch()
        {
            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "CHILD_BIRTH_CERTIFICATE",
                    FullName = "Nguyễn Văn Bé",
                    BirthDate = "2020-01-01",
                    DocumentNumber = "GKS-1234/2020" // Khác với hồ sơ
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("gks.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Bé",
                BirthDate = new DateTime(2020, 1, 1),
                BirthCertNumber = "GKS-9999/2020",
                Relationship = "CHILD"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "CHILD_BIRTH_CERTIFICATE", mockFile.Object));

            ex.ErrorCode.Should().Be("IDENTITY_MISMATCH");
            ex.Message.Should().Contain("Số giấy khai sinh trên giấy tờ");
        }

        [Fact]
        public async Task UTCID36_ValidateDependentDocumentAsync_StudentCard_MissingSchoolName_ThrowsInvalidDocumentData()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "STUDENT_CARD",
                    FullName = "Nguyễn Văn Sinh Viên",
                    BirthDate = "2005-08-20",
                    SchoolName = null // Thiếu tên trường
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("thesv.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Sinh Viên",
                BirthDate = new DateTime(2005, 8, 20),
                CurrentGroup = "CHILD_OVER_18_STUDYING"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "STUDENT_CARD", mockFile.Object));

            ex.ErrorCode.Should().Be("INVALID_DOCUMENT_DATA");
            ex.Message.Should().Contain("tên trường học");
        }

        [Fact]
        public async Task UTCID37_ValidateDependentDocumentAsync_StudentCard_Expired_ThrowsDocumentExpired()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "STUDENT_CARD",
                    FullName = "Nguyễn Văn Sinh Viên",
                    BirthDate = "2005-08-20",
                    SchoolName = "Đại học Bách Khoa",
                    ValidTo = "2025-06-30" // Đã hết hạn trước 2026-01
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("thesv.jpg");

            var dependent = new Dependent
            {
                FullName = "Nguyễn Văn Sinh Viên",
                BirthDate = new DateTime(2005, 8, 20),
                EffectiveFromMonth = "2026-01",
                CurrentGroup = "CHILD_OVER_18_STUDYING"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "STUDENT_CARD", mockFile.Object));

            ex.ErrorCode.Should().Be("DOCUMENT_EXPIRED");
            ex.Message.Should().Contain("hết hạn");
        }

        [Fact]
        public async Task UTCID38_ValidateDependentDocumentAsync_DisabilityCert_Unqualified_ThrowsDisabilityNotQualified()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "DISABILITY_CERTIFICATE",
                    FullName = "Trần Thị Bệnh",
                    BirthDate = "1960-03-10",
                    DisabilityLevel = "Nhẹ", // Không thuộc Đặc biệt nặng hay Nặng
                    LaborCapacityLossPercent = 30 // < 61%
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("khuyettat.jpg");

            var dependent = new Dependent
            {
                FullName = "Trần Thị Bệnh",
                BirthDate = new DateTime(1960, 3, 10),
                CurrentGroup = "PARENT_DISABLED"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "DISABILITY_CERTIFICATE", mockFile.Object));

            ex.ErrorCode.Should().Be("DISABILITY_NOT_QUALIFIED");
            ex.Message.Should().Contain("không đủ điều kiện");
        }

        [Fact]
        public async Task UTCID39_ValidateDependentDocumentAsync_DisabilityCert_QualifiedByLevel_Success()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "DISABILITY_CERTIFICATE",
                    FullName = "Trần Thị Bệnh",
                    BirthDate = "1960-03-10",
                    DisabilityLevel = "Đặc biệt nặng"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("khuyettat.jpg");

            var dependent = new Dependent
            {
                FullName = "Trần Thị Bệnh",
                BirthDate = new DateTime(1960, 3, 10),
                CurrentGroup = "PARENT_DISABLED"
            };

            await _service.ValidateDependentDocumentAsync(dependent, "DISABILITY_CERTIFICATE", mockFile.Object);
        }

        [Fact]
        public async Task UTCID40_ValidateDependentDocumentAsync_MarriageCert_AfterEffectiveMonth_ThrowsMarriageAfterEffective()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var taxpayer = new User { FullName = "Nguyễn Văn Chồng" };
            var dependent = new Dependent
            {
                FullName = "Trần Thị Vợ",
                Relationship = "SPOUSE",
                EffectiveFromMonth = "2026-01"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "MARRIAGE_CERTIFICATE",
                    FullName = "Nguyễn Văn Chồng",
                    SpouseFullName = "Trần Thị Vợ",
                    IssueDate = "2026-02-15" // Kết hôn tháng 2 nhưng khai giảm trừ từ tháng 1
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("kethon.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "MARRIAGE_CERTIFICATE", mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("MARRIAGE_AFTER_EFFECTIVE");
            ex.Message.Should().Contain("sau thời điểm bắt đầu tính");
        }

        [Fact]
        public async Task UTCID41_ValidateDependentDocumentAsync_HouseholdCert_MissingMember_ThrowsRelationshipMismatch()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var taxpayer = new User { FullName = "Nguyễn Văn Chủ Hộ" };
            var dependent = new Dependent { FullName = "Lê Thị Con Dâu" };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "HOUSEHOLD_CERTIFICATE",
                    HouseholdMembers = new List<HouseholdMemberDto>
                    {
                        new HouseholdMemberDto { FullName = "Nguyễn Văn Chủ Hộ" },
                        new HouseholdMemberDto { FullName = "Nguyễn Văn Con Trai" }
                        // Không có Lê Thị Con Dâu
                    }
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("ct07.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "HOUSEHOLD_CERTIFICATE", mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("RELATIONSHIP_MISMATCH");
            ex.Message.Should().Contain("phải chứa thông tin của cả");
        }

        [Fact]
        public async Task UTCID42_ValidateDependentDocumentAsync_HouseholdCert_BothMembersPresent_Success()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var taxpayer = new User { FullName = "Nguyễn Văn Chủ Hộ" };
            var dependent = new Dependent { FullName = "Lê Thị Con Dâu" };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "HOUSEHOLD_CERTIFICATE",
                    IssueDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    HouseholdMembers = new List<HouseholdMemberDto>
                    {
                        new HouseholdMemberDto { FullName = "Nguyễn Văn Chủ Hộ" },
                        new HouseholdMemberDto { FullName = "Lê Thị Con Dâu" }
                    }
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("ct07.jpg");

            await _service.ValidateDependentDocumentAsync(dependent, "HOUSEHOLD_CERTIFICATE", mockFile.Object, taxpayer);
        }

        [Fact]
        public async Task UTCID43_ValidateDependentDocumentAsync_AdoptionCert_ParentRelationship_Success()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var taxpayer = new User { FullName = "Nguyễn Văn Con Nuôi" };
            var dependent = new Dependent
            {
                FullName = "Trần Văn Cha Nuôi",
                Relationship = "PARENT"
            };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "ADOPTION_CERTIFICATE",
                    AdoptiveParentFullName = "Trần Văn Cha Nuôi",
                    AdoptiveChildFullName = "Nguyễn Văn Con Nuôi"
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("adoption.jpg");

            await _service.ValidateDependentDocumentAsync(dependent, "ADOPTION_CERTIFICATE", mockFile.Object, taxpayer);
        }

        [Fact]
        public async Task UTCID44_ValidateDependentDocumentAsync_SupportCommitment_MissingStamp_ThrowsCommitmentNotCertified()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var taxpayer = new User { FullName = "Nguyễn Văn Cháu" };
            var dependent = new Dependent { FullName = "Trần Thị Bà" };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "SUPPORT_COMMITMENT",
                    FullName = "Nguyễn Văn Cháu",
                    SpouseFullName = "Trần Thị Bà",
                    HasSignature = true,
                    HasStamp = false // Chưa có con dấu UBND
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("camket.jpg");

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ValidateDependentDocumentAsync(dependent, "SUPPORT_COMMITMENT", mockFile.Object, taxpayer));

            ex.ErrorCode.Should().Be("COMMITMENT_NOT_CERTIFIED");
            ex.Message.Should().Contain("chưa có con dấu xác nhận");
        }

        [Fact]
        public async Task UTCID45_ValidateDependentDocumentAsync_Other_BasicChecksPass_Success()
        {
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns((IGenericRepository<SystemConfig>)null!);

            var dependent = new Dependent { FullName = "Nguyễn Văn Khác" };

            var ocrResponse = new OcrExtractResponseMessage
            {
                Success = true,
                Data = new ExtractedDependentDataDto
                {
                    IsReadable = true,
                    DocumentType = "OTHER",
                    IsPassedThreshold = true
                }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ocrResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.FileName).Returns("other.jpg");

            await _service.ValidateDependentDocumentAsync(dependent, "OTHER", mockFile.Object);
        }

        #endregion
    }
}
