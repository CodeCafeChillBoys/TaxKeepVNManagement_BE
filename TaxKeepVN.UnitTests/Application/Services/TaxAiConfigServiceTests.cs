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
using TaxKeepVN.Application.DTOs.SystemConfig;
using TaxKeepVN.Application.Service.Implementations;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class TaxAiConfigServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<TaxAiConfigService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly TaxAiConfigService _service;

        public TaxAiConfigServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<TaxAiConfigService>>();
            _mockHandler = new Mock<HttpMessageHandler>();

            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("http://ai-service.local")
            };

            _mockHttpClientFactory.Setup(f => f.CreateClient("TaxAIService")).Returns(_httpClient);

            _service = new TaxAiConfigService(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        #region GetAllConfigsAsync

        [Fact]
        public async Task UTCID01_GetAllConfigsAsync_Success_ReturnsList()
        {
            var configs = new List<SystemConfigResponseDto>
            {
                new SystemConfigResponseDto { ConfigKey = "TAX_RATE", ConfigValue = "0.1" }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(configs), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetAllConfigsAsync();

            result.Should().HaveCount(1);
            result[0].ConfigKey.Should().Be("TAX_RATE");
        }

        #endregion

        #region GetConfigByKeyAsync

        [Fact]
        public async Task UTCID02_GetConfigByKeyAsync_NotFound_ReturnsNull()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetConfigByKeyAsync("NON_EXISTING");

            result.Should().BeNull();
        }

        [Fact]
        public async Task UTCID03_GetConfigByKeyAsync_Found_ReturnsDto()
        {
            var config = new SystemConfigResponseDto { ConfigKey = "DEDUCTION", ConfigValue = "11000000" };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(config), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.GetConfigByKeyAsync("DEDUCTION");

            result.Should().NotBeNull();
            result!.ConfigValue.Should().Be("11000000");
        }

        #endregion

        #region CreateConfigAsync

        [Fact]
        public async Task UTCID04_CreateConfigAsync_Success_ReturnsCreatedDto()
        {
            var config = new SystemConfigResponseDto { ConfigKey = "NEW_CONFIG", ConfigValue = "VALUE" };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(config), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.CreateConfigAsync(new SystemConfigCreateRequestDto { ConfigKey = "NEW_CONFIG" });

            result.Should().NotBeNull();
            result.ConfigKey.Should().Be("NEW_CONFIG");
        }

        #endregion

        #region DeleteConfigAsync

        [Fact]
        public async Task UTCID05_DeleteConfigAsync_NotFound_ReturnsFalse()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteConfigAsync("UNKNOWN");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID06_DeleteConfigAsync_Success_ReturnsTrue()
        {
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.DeleteConfigAsync("KEY", Guid.NewGuid());

            result.Should().BeTrue();
        }

        #endregion

        #region UpdateConfigAsync

        [Fact]
        public async Task UTCID07_UpdateConfigAsync_Success_ReturnsUpdatedDto()
        {
            var config = new SystemConfigResponseDto { ConfigKey = "KEY", ConfigValue = "NEW_VALUE" };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(config), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.UpdateConfigAsync("KEY", new SystemConfigUpdateRequestDto { ConfigValue = "NEW_VALUE" });

            result.Should().NotBeNull();
            result.ConfigValue.Should().Be("NEW_VALUE");
        }

        #endregion

        #region RestoreConfigAsync

        [Fact]
        public async Task UTCID08_RestoreConfigAsync_Success_ReturnsRestoredDto()
        {
            var config = new SystemConfigResponseDto { ConfigKey = "KEY", IsActive = true };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(config), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Patch),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.RestoreConfigAsync("KEY", Guid.NewGuid());

            result.Should().NotBeNull();
            result.IsActive.Should().BeTrue();
        }

        #endregion

        #region TestResolveThresholdAsync

        [Fact]
        public async Task UTCID09_TestResolveThresholdAsync_Success_ReturnsResponse()
        {
            var response = new ThresholdResolveTestResponseDto { CategoryCode = "INCOME_TAX", ResolvedThreshold = 11000000f };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response), System.Text.Encoding.UTF8, "application/json")
            };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            var result = await _service.TestResolveThresholdAsync("INCOME_TAX");

            result.Should().NotBeNull();
            result.CategoryCode.Should().Be("INCOME_TAX");
        }

        #endregion
    }
}
