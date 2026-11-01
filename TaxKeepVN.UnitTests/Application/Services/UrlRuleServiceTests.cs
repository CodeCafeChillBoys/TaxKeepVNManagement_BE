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
    public class UrlRuleServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<UrlRuleService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly UrlRuleService _service;

        public UrlRuleServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<UrlRuleService>>();
            _mockHandler = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("http://ai-service.local")
            };

            _mockHttpClientFactory.Setup(f => f.CreateClient("TaxAIService")).Returns(_httpClient);

            _service = new UrlRuleService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        #region GetUrlRulesAsync

        [Fact]
        public async Task UTCID01_GetUrlRulesAsync_Success_ReturnsList()
        {
            var rules = new List<UrlRuleResponse>
            {
                new UrlRuleResponse { Id = Guid.NewGuid(), Name = "Thuế điện tử", Domain = "thuedientu.gdt.gov.vn" }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(rules), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetUrlRulesAsync(false);

            result.Should().HaveCount(1);
            result[0].Domain.Should().Be("thuedientu.gdt.gov.vn");
        }

        #endregion

        #region GetUrlRuleByIdAsync

        [Fact]
        public async Task UTCID02_GetUrlRuleByIdAsync_NotFound_ReturnsNull()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetUrlRuleByIdAsync(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task UTCID03_GetUrlRuleByIdAsync_Success_ReturnsDto()
        {
            var rule = new UrlRuleResponse { Id = Guid.NewGuid(), Name = "Thư viện pháp luật" };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(rule), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetUrlRuleByIdAsync(rule.Id);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Thư viện pháp luật");
        }

        #endregion

        #region CreateUrlRuleAsync

        [Fact]
        public async Task UTCID04_CreateUrlRuleAsync_Success_ReturnsCreated()
        {
            var created = new UrlRuleResponse { Id = Guid.NewGuid(), Name = "Luật thuế TNCN" };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(created), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var request = new UrlRuleCreateRequest { Name = "Luật thuế TNCN", Domain = "gdt.gov.vn" };
            var result = await _service.CreateUrlRuleAsync(request, Guid.NewGuid());

            result.Should().NotBeNull();
            result.Name.Should().Be("Luật thuế TNCN");
        }

        #endregion

        #region UpdateUrlRuleAsync

        [Fact]
        public async Task UTCID05_UpdateUrlRuleAsync_NotFound_ReturnsNull()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.UpdateUrlRuleAsync(Guid.NewGuid(), new UrlRuleUpdateRequest(), Guid.NewGuid());

            result.Should().BeNull();
        }

        [Fact]
        public async Task UTCID08_UpdateUrlRuleAsync_Success_ReturnsUpdated()
        {
            var updated = new UrlRuleResponse { Id = Guid.NewGuid(), Name = "Luật mới" };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(updated), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.UpdateUrlRuleAsync(updated.Id, new UrlRuleUpdateRequest { Name = "Luật mới" }, Guid.NewGuid());

            result.Should().NotBeNull();
            result!.Name.Should().Be("Luật mới");
        }

        #endregion

        #region DeleteUrlRuleAsync

        [Fact]
        public async Task UTCID06_DeleteUrlRuleAsync_NotFound_ReturnsFalse()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteUrlRuleAsync(Guid.NewGuid());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID07_DeleteUrlRuleAsync_Success_ReturnsTrue()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteUrlRuleAsync(Guid.NewGuid());

            result.Should().BeTrue();
        }

        [Fact]
        public async Task UTCID08_GetUrlRulesAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetUrlRulesAsync(false));
        }

        [Fact]
        public async Task UTCID09_GetUrlRuleByIdAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.GetUrlRuleByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID10_CreateUrlRuleAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.CreateUrlRuleAsync(new UrlRuleCreateRequest { Name = "Test", Domain = "test.vn" }, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID11_UpdateUrlRuleAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.UpdateUrlRuleAsync(Guid.NewGuid(), new UrlRuleUpdateRequest { Name = "Test" }, Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID12_DeleteUrlRuleAsync_Error_ThrowsHttpRequestException()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("AI Error")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                _service.DeleteUrlRuleAsync(Guid.NewGuid()));
        }

        #endregion
    }
}
