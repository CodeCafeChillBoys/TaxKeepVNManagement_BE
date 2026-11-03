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
    public class PromptTemplateServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<PromptTemplateService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly PromptTemplateService _service;

        public PromptTemplateServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<PromptTemplateService>>();
            _mockHandler = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("http://ai-service.local")
            };

            _mockHttpClientFactory.Setup(f => f.CreateClient("TaxAIService")).Returns(_httpClient);
            _service = new PromptTemplateService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        #region GetAllPromptsAsync

        [Fact]
        public async Task GetAllPromptsAsync_Success_ReturnsList()
        {
            var prompts = new List<PromptTemplateResponseDto>
            {
                new PromptTemplateResponseDto { PromptKey = "PROMPT_TEST", SystemPrompt = "You are a test assistant.", Version = 1 }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(prompts), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/api/prompt-templates"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetAllPromptsAsync();

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].PromptKey.Should().Be("PROMPT_TEST");
        }

        [Fact]
        public async Task GetAllPromptsAsync_Failure_ThrowsHttpRequestException()
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

            var act = () => _service.GetAllPromptsAsync();

            await act.Should().ThrowAsync<HttpRequestException>();
        }

        #endregion

        #region GetPromptByKeyAsync

        [Fact]
        public async Task GetPromptByKeyAsync_Success_ReturnsPrompt()
        {
            var expected = new PromptTemplateResponseDto { PromptKey = "KEY_01", SystemPrompt = "Prompt 01", Version = 1 };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expected), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/api/prompt-templates/KEY_01"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetPromptByKeyAsync("KEY_01");

            result.Should().NotBeNull();
            result!.PromptKey.Should().Be("KEY_01");
        }

        [Fact]
        public async Task GetPromptByKeyAsync_NotFound_ReturnsNull()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetPromptByKeyAsync("KEY_UNKNOWN");

            result.Should().BeNull();
        }

        #endregion

        #region CreatePromptAsync

        [Fact]
        public async Task CreatePromptAsync_Success_ReturnsCreatedPrompt()
        {
            var request = new PromptTemplateCreateRequestDto
            {
                PromptKey = "PROMPT_NEW",
                SystemPrompt = "New Prompt Content",
                Description = "Test desc"
            };

            var expected = new PromptTemplateResponseDto
            {
                PromptKey = request.PromptKey,
                SystemPrompt = request.SystemPrompt,
                Version = 1,
                IsActive = true
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(expected), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath == "/api/prompt-templates"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.CreatePromptAsync(request);

            result.Should().NotBeNull();
            result.PromptKey.Should().Be("PROMPT_NEW");
        }

        #endregion

        #region UpdatePromptAsync

        [Fact]
        public async Task UpdatePromptAsync_Success_ReturnsUpdatedPrompt()
        {
            var request = new PromptTemplateUpdateRequestDto
            {
                SystemPrompt = "Updated System Prompt"
            };

            var expected = new PromptTemplateResponseDto
            {
                PromptKey = "PROMPT_EXISTING",
                SystemPrompt = request.SystemPrompt,
                Version = 2
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expected), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/api/prompt-templates/PROMPT_EXISTING"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.UpdatePromptAsync("PROMPT_EXISTING", request);

            result.Should().NotBeNull();
            result.Version.Should().Be(2);
        }

        #endregion

        #region DeletePromptAsync

        [Fact]
        public async Task DeletePromptAsync_Success_ReturnsTrue()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NoContent);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete && r.RequestUri!.AbsolutePath == "/api/prompt-templates/PROMPT_DEL"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeletePromptAsync("PROMPT_DEL");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeletePromptAsync_NotFound_ReturnsFalse()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeletePromptAsync("PROMPT_NOT_FOUND");

            result.Should().BeFalse();
        }

        #endregion

        #region TestRenderPromptAsync

        [Fact]
        public async Task TestRenderPromptAsync_Success_ReturnsRenderedResponse()
        {
            var request = new PromptTestRenderRequestDto
            {
                PromptKey = "PROMPT_RENDER",
                Params = new Dictionary<string, object> { { "name", "Cuong" } }
            };

            var expected = new PromptTestRenderResponseDto
            {
                PromptKey = "PROMPT_RENDER",
                RenderedPrompt = "Hello Cuong",
                VariablesUsed = new List<string> { "name" }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expected), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath == "/api/prompt-templates/test-render"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.TestRenderPromptAsync(request);

            result.Should().NotBeNull();
            result.RenderedPrompt.Should().Be("Hello Cuong");
        }

        #endregion
    }
}
