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
    public class TaxChatServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<TaxChatService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly TaxChatService _service;

        public TaxChatServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<TaxChatService>>();
            _mockHandler = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("http://ai-service.local")
            };

            _mockHttpClientFactory.Setup(f => f.CreateClient("TaxAIService")).Returns(_httpClient);

            _service = new TaxChatService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        #region AskAsync

        [Fact]
        public async Task UTCID01_AskAsync_Success_ReturnsTaxChatResponse()
        {
            var expectedResponse = new TaxChatResponse
            {
                Answer = "Mức giảm trừ gia cảnh cho người nộp thuế là 11 triệu đồng/tháng."
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var request = new TaxChatRequest { Question = "Mức giảm trừ là bao nhiêu?" };
            var result = await _service.AskAsync(Guid.NewGuid(), request);

            result.Should().NotBeNull();
            result.Answer.Should().Contain("11 triệu đồng");
        }

        #endregion

        #region GetSessionsAsync

        [Fact]
        public async Task UTCID02_GetSessionsAsync_Success_ReturnsSessionList()
        {
            var sessions = new List<ChatSessionResponse>
            {
                new ChatSessionResponse { Id = Guid.NewGuid(), Title = "Hỏi về thuế TNCN" }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(sessions), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetSessionsAsync(Guid.NewGuid());

            result.Should().HaveCount(1);
            result[0].Title.Should().Be("Hỏi về thuế TNCN");
        }

        #endregion

        #region DeleteSessionAsync

        [Fact]
        public async Task UTCID03_DeleteSessionAsync_NotFound_ReturnsFalse()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteSessionAsync(Guid.NewGuid());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID04_DeleteSessionAsync_Success_ReturnsTrue()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteSessionAsync(Guid.NewGuid());

            result.Should().BeTrue();
        }

        #endregion

        #region GetMessagesAsync

        [Fact]
        public async Task UTCID05_GetMessagesAsync_NotFound_ReturnsEmptyList()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetMessagesAsync(Guid.NewGuid());

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task UTCID06_GetMessagesAsync_Success_ReturnsMessages()
        {
            var messages = new List<ChatMessageResponse>
            {
                new ChatMessageResponse { Id = Guid.NewGuid(), Role = "user", Content = "Chào bot" },
                new ChatMessageResponse { Id = Guid.NewGuid(), Role = "assistant", Content = "Chào bạn" }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(messages), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetMessagesAsync(Guid.NewGuid());

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task UTCID07_AskAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.AskAsync(Guid.NewGuid(), new TaxChatRequest { Question = "Error test" }));
        }

        [Fact]
        public async Task UTCID08_GetSessionsAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetSessionsAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID09_GetMessagesAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetMessagesAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID10_DeleteSessionAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.DeleteSessionAsync(Guid.NewGuid()));
        }

        #endregion
    }
}
