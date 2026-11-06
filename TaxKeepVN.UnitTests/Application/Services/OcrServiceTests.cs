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
        public async Task UTCID14_ExtractDocumentAsync_AiServiceError_ThrowsBadRequestException()
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

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ExtractDocumentAsync(mockFile.Object));
            ex.ErrorCode.Should().Be("OCR_SERVICE_ERROR");
        }

        #endregion
    }
}
