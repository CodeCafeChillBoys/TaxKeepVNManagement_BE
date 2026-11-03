using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Implementations;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class AiKeyServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<AiKeyService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly AiKeyService _service;

        public AiKeyServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<AiKeyService>>();
            _mockHandler = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("http://ai-service.local")
            };

            _mockHttpClientFactory.Setup(f => f.CreateClient("TaxAIService")).Returns(_httpClient);
            _service = new AiKeyService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        #region GetAllKeysAsync

        [Fact]
        public async Task GetAllKeysAsync_Success_ReturnsList()
        {
            var keys = new List<AiKeyResponseDto>
            {
                new AiKeyResponseDto { Id = Guid.NewGuid(), KeyName = "Primary Key", Provider = "GEMINI", MaskedApiKey = "AIzaSy...1234" }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(keys), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/api/ai-keys"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetAllKeysAsync();

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].KeyName.Should().Be("Primary Key");
        }

        [Fact]
        public async Task GetAllKeysAsync_Failure_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var act = () => _service.GetAllKeysAsync();

            await act.Should().ThrowAsync<HttpRequestException>();
        }

        #endregion

        #region CreateKeyAsync

        [Fact]
        public async Task CreateKeyAsync_Success_ReturnsCreatedKey()
        {
            var request = new AiKeyCreateRequestDto
            {
                KeyName = "New Key",
                ApiKey = "AIzaSyTest123",
                Provider = "GEMINI",
                Priority = 1
            };

            var expectedResponse = new AiKeyResponseDto
            {
                Id = Guid.NewGuid(),
                KeyName = request.KeyName,
                Provider = request.Provider,
                MaskedApiKey = "AIzaSy...t123",
                Priority = 1,
                IsActive = true,
                Status = "ACTIVE"
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath == "/api/ai-keys"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.CreateKeyAsync(request);

            result.Should().NotBeNull();
            result.KeyName.Should().Be("New Key");
        }

        [Fact]
        public async Task CreateKeyAsync_Failure_ThrowsHttpRequestException()
        {
            var request = new AiKeyCreateRequestDto
            {
                KeyName = "Invalid Key",
                ApiKey = "bad-key"
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Invalid API Key")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var act = () => _service.CreateKeyAsync(request);

            await act.Should().ThrowAsync<HttpRequestException>();
        }

        #endregion

        #region UpdateKeyAsync

        [Fact]
        public async Task UpdateKeyAsync_Success_ReturnsUpdatedKey()
        {
            var id = Guid.NewGuid();
            var request = new AiKeyUpdateRequestDto { Priority = 2, IsActive = false };
            var expectedResponse = new AiKeyResponseDto { Id = id, Priority = 2, IsActive = false };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == $"/api/ai-keys/{id}"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.UpdateKeyAsync(id, request);

            result.Should().NotBeNull();
            result!.Priority.Should().Be(2);
            result.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateKeyAsync_NotFound_ReturnsNull()
        {
            var id = Guid.NewGuid();
            var request = new AiKeyUpdateRequestDto { Priority = 2 };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.UpdateKeyAsync(id, request);

            result.Should().BeNull();
        }

        #endregion

        #region DeleteKeyAsync

        [Fact]
        public async Task DeleteKeyAsync_Success_ReturnsTrue()
        {
            var id = Guid.NewGuid();
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NoContent);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete && r.RequestUri!.AbsolutePath == $"/api/ai-keys/{id}"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteKeyAsync(id);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteKeyAsync_NotFound_ReturnsFalse()
        {
            var id = Guid.NewGuid();
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteKeyAsync(id);

            result.Should().BeFalse();
        }

        #endregion
    }
}
