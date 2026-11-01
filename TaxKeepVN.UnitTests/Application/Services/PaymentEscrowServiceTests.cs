using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Payments;
using TaxKeepVN.Application.DTOs.Wallets;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class PaymentEscrowServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<PaymentEscrowService>> _mockLogger;
        private readonly PaymentEscrowService _service;

        public PaymentEscrowServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<PaymentEscrowService>>();

            _service = new PaymentEscrowService(
                _mockUnitOfWork.Object,
                _mockConfig.Object,
                _mockLogger.Object
            );
        }

        private static string GenerateVnPaySecureHash(IDictionary<string, string> queryParams, string hashSecret)
        {
            var sortedParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in queryParams)
            {
                if (!string.IsNullOrEmpty(kv.Key) &&
                    kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase) &&
                    !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                    !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(kv.Value))
                {
                    sortedParams[kv.Key] = kv.Value;
                }
            }

            var data = new StringBuilder();
            foreach (var kv in sortedParams)
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }
            var rawData = data.ToString().TrimEnd('&');
            return VnPayHelper.HmacSha512(hashSecret, rawData);
        }

        #region CreateOrGetPaymentLinkAsync

        [Fact]
        public async Task UTCID01_CreatePaymentLink_BookingNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync((Booking?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.CreateOrGetPaymentLinkAsync(userId, bookingId));
        }

        [Fact]
        public async Task UTCID02_CreatePaymentLink_NotOwner_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = ownerId };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.CreateOrGetPaymentLinkAsync(otherUserId, bookingId));
            ex.ErrorCode.Should().Be("FORBIDDEN");
        }

        [Fact]
        public async Task UTCID03_CreatePaymentLink_InvalidBookingStatus_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.CONFIRMED // Không phải PENDING_PAYMENT
            };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CreateOrGetPaymentLinkAsync(userId, bookingId));
            ex.ErrorCode.Should().Be("INVALID_BOOKING_STATUS");
        }

        #endregion

        #region GetEscrowByBookingIdAsync

        [Fact]
        public async Task UTCID04_GetEscrowByBookingIdAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Booking?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetEscrowByBookingIdAsync(Guid.NewGuid(), Guid.NewGuid(), "user"));
        }

        [Fact]
        public async Task UTCID05_GetEscrowByBookingIdAsync_Forbidden_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, UserId = ownerId, ExpertSlotId = Guid.NewGuid() };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetEscrowByBookingIdAsync(bookingId, Guid.NewGuid(), "user"));
        }

        [Fact]
        public async Task UTCID06_GetEscrowByBookingIdAsync_Valid_ReturnsEscrowResponse()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, UserId = userId, BookingCode = "BK123", ExpertSlotId = Guid.NewGuid() };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var escrow = new EscrowTransaction
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                Amount = 500000,
                EscrowStatus = EscrowStatus.HeldInEscrow
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                           .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var result = await _service.GetEscrowByBookingIdAsync(bookingId, userId, "user");

            result.Should().NotBeNull();
            result.Amount.Should().Be(500000);
            result.EscrowStatus.Should().Be(EscrowStatus.HeldInEscrow);
        }

        #endregion

        #region GetAdminPlatformWalletAsync

        [Fact]
        public async Task UTCID07_GetAdminPlatformWalletAsync_NonAdmin_ThrowsForbiddenException()
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetAdminPlatformWalletAsync(Guid.NewGuid(), "user"));
        }

        [Fact]
        public async Task UTCID08_GetAdminPlatformWalletAsync_Admin_ReturnsWalletResponse()
        {
            var adminId = Guid.NewGuid();
            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", AvailableBalance = 1000000 };
            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                          .ReturnsAsync(new List<UserWallet> { platformWallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { Id = 1, IsActive = true, PlatformCommissionRatePercent = 10 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var escrows = new List<EscrowTransaction>
            {
                new EscrowTransaction { Amount = 500000, CommissionAmount = 50000, EscrowStatus = EscrowStatus.HeldInEscrow },
                new EscrowTransaction { Amount = 300000, CommissionAmount = 30000, EscrowStatus = EscrowStatus.Settled }
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(escrows);
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Booking>());
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                      .ReturnsAsync(new List<WalletTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var result = await _service.GetAdminPlatformWalletAsync(adminId, "admin");

            result.Should().NotBeNull();
            result.PlatformAvailableBalance.Should().Be(1000000);
            result.EscrowHoldingBalance.Should().Be(500000);
            result.RealizedPlatformRevenue.Should().Be(30000);
        }

        #endregion

        #region CreateWalletTopUpPaymentUrlAsync

        [Fact]
        public async Task UTCID09_CreateWalletTopUpPaymentUrlAsync_InvalidAmount_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CreateWalletTopUpPaymentUrlAsync(Guid.NewGuid(), new WalletTopUpRequest { Amount = 0 }));
            ex.ErrorCode.Should().Be("INVALID_AMOUNT");
        }

        [Fact]
        public async Task UTCID10_CreateWalletTopUpPaymentUrlAsync_Valid_ReturnsTopUpResponse()
        {
            var userId = Guid.NewGuid();
            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 50000 };
            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                .ReturnsAsync(new List<UserWallet> { wallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var result = await _service.CreateWalletTopUpPaymentUrlAsync(userId, new WalletTopUpRequest { Amount = 100000, ReturnUrl = "https://taxkeep.vn/payment-return" }, "127.0.0.1");

            result.Should().NotBeNull();
            result.Amount.Should().Be(100000);
        }

        #endregion

        #region Withdrawals

        [Fact]
        public async Task UTCID11_RequestAdminPlatformWithdrawalAsync_NonAdmin_ThrowsForbiddenException()
        {
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { UserRole = "user" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.RequestAdminPlatformWithdrawalAsync(Guid.NewGuid(), new CreateWithdrawalRequest()));
        }

        [Fact]
        public async Task UTCID12_GetAdminPlatformWithdrawalsAsync_NonAdmin_ThrowsForbiddenException()
        {
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { UserRole = "user" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetAdminPlatformWithdrawalsAsync(Guid.NewGuid()));
        }

        #endregion

        #region Extended Payment & Escrow Coverage

        [Fact]
        public async Task UTCID13_CreateOrGetPaymentLinkAsync_HoldSlotExpired_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.PENDING_PAYMENT,
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CreateOrGetPaymentLinkAsync(userId, bookingId));
            ex.ErrorCode.Should().Be("HOLD_SLOT_EXPIRED");
        }

        [Fact]
        public async Task UTCID14_CreateOrGetPaymentLinkAsync_ExistingEscrowWithLink_ReturnsExistingResponse()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.PENDING_PAYMENT,
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                BookingCode = "BK_EXISTING"
            };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var existingEscrow = new EscrowTransaction
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                EscrowStatus = EscrowStatus.PendingPayment,
                CheckoutUrl = "https://sandbox.vnpayment.vn/test",
                Amount = 500000m
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { existingEscrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var result = await _service.CreateOrGetPaymentLinkAsync(userId, bookingId);

            result.Should().NotBeNull();
            result.CheckoutUrl.Should().Be("https://sandbox.vnpayment.vn/test");
        }

        [Fact]
        public async Task UTCID15_CreateOrGetPaymentLinkAsync_WalletPayment_InsufficientBalance_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.PENDING_PAYMENT,
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                Fee = 500000m
            };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { PlatformCommissionRatePercent = 10 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                          .ReturnsAsync(new List<UserWallet> { new UserWallet { UserId = userId, WalletType = "USER", AvailableBalance = 100000m } });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CreateOrGetPaymentLinkAsync(userId, bookingId, new CreatePaymentLinkRequest { PaymentMethod = "WALLET" }));
            ex.ErrorCode.Should().Be("INSUFFICIENT_WALLET_BALANCE");
        }

        [Fact]
        public async Task UTCID16_CreateOrGetPaymentLinkAsync_WalletPayment_Success()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                ExpertSlotId = slotId,
                Status = BookingStatus.PENDING_PAYMENT,
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                Fee = 500000m,
                BookingCode = "BK_WALLET"
            };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { PlatformCommissionRatePercent = 10, ExpertConfirmTimeoutHours = 2 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 1000000m };
            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                          .ReturnsAsync(new List<UserWallet> { wallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(new ExpertSlot { Id = slotId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var result = await _service.CreateOrGetPaymentLinkAsync(userId, bookingId, new CreatePaymentLinkRequest { PaymentMethod = "WALLET" });

            result.Should().NotBeNull();
            result.PaymentGateway.Should().Be("WALLET");
            wallet.AvailableBalance.Should().Be(500000m);
            booking.Status.Should().Be(BookingStatus.AWAITING_EXPERT_APPROVAL);
        }

        [Fact]
        public async Task UTCID17_CreateOrGetPaymentLinkAsync_VnPay_CreatesNewAndUpdatesExisting()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.PENDING_PAYMENT,
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                Fee = 500000m,
                BookingCode = "BK_VNP"
            };
            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            var escrows = new List<EscrowTransaction>();
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(escrows);
            mockEscrowRepo.Setup(r => r.AddAsync(It.IsAny<EscrowTransaction>()))
                          .Callback<EscrowTransaction>(e => escrows.Add(e))
                          .Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { PlatformCommissionRatePercent = 10 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            // New escrow creation
            var resultNew = await _service.CreateOrGetPaymentLinkAsync(userId, bookingId);
            resultNew.Should().NotBeNull();
            resultNew.CheckoutUrl.Should().NotBeNullOrEmpty();
            resultNew.QrCode.Should().NotBeNullOrEmpty();

            // Existing escrow update
            var resultExisting = await _service.CreateOrGetPaymentLinkAsync(userId, bookingId);
            resultExisting.Should().NotBeNull();
        }

        [Fact]
        public async Task UTCID18_HandleVnPayIpnAsync_EmptyAndInvalidSignature()
        {
            var resEmpty = await _service.HandleVnPayIpnAsync(new Dictionary<string, string>());
            resEmpty.RspCode.Should().Be("99");

            var queryParams = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = "123456",
                ["vnp_SecureHash"] = "INVALID_HASH"
            };
            var resInvalid = await _service.HandleVnPayIpnAsync(queryParams);
            resInvalid.RspCode.Should().Be("97");
        }

        [Fact]
        public async Task UTCID19_HandleVnPayIpnAsync_TopUpWallet_SuccessAndFailed()
        {
            const string hashSecret = "RAOQUIASHAEJGWFNYAOMKYFISOPSPAGK";
            var orderCode = 998877L;
            var walletId = Guid.NewGuid();
            var wallet = new UserWallet { Id = walletId, AvailableBalance = 100000m, TotalEarned = 100000m };

            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.GetByIdAsync(walletId)).ReturnsAsync(wallet);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var topUpTx = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                Amount = 200000m,
                TransactionType = "TOPUP_PENDING",
                Description = $"TOPUP_ORDER:{orderCode}"
            };

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                      .ReturnsAsync(new List<WalletTransaction> { topUpTx });
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            // Success (00)
            var queryParams = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = orderCode.ToString(),
                ["vnp_Amount"] = "20000000",
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = "TX123"
            };
            queryParams["vnp_SecureHash"] = GenerateVnPaySecureHash(queryParams, hashSecret);

            var res = await _service.HandleVnPayIpnAsync(queryParams);
            res.RspCode.Should().Be("00");
            wallet.AvailableBalance.Should().Be(300000m);
            topUpTx.TransactionType.Should().Be("TOPUP");

            // Already confirmed
            var resConfirmed = await _service.HandleVnPayIpnAsync(queryParams);
            resConfirmed.RspCode.Should().Be("02");

            // Failed code
            topUpTx.TransactionType = "TOPUP_PENDING";
            var queryParamsFail = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = orderCode.ToString(),
                ["vnp_Amount"] = "20000000",
                ["vnp_ResponseCode"] = "24",
                ["vnp_TransactionNo"] = "TX123"
            };
            queryParamsFail["vnp_SecureHash"] = GenerateVnPaySecureHash(queryParamsFail, hashSecret);

            var resFail = await _service.HandleVnPayIpnAsync(queryParamsFail);
            resFail.RspCode.Should().Be("00");
            topUpTx.TransactionType.Should().Be("TOPUP_FAILED");
        }

        [Fact]
        public async Task UTCID20_HandleVnPayIpnAsync_ConsultationBooking_SuccessAndIdempotency()
        {
            const string hashSecret = "RAOQUIASHAEJGWFNYAOMKYFISOPSPAGK";
            var orderCode = 887766L;
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                BookingCode = "BK_IPN",
                ExpertSlotId = slotId,
                Status = BookingStatus.PENDING_PAYMENT
            };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var escrow = new EscrowTransaction
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                OrderCode = orderCode,
                Amount = 500000m,
                EscrowStatus = EscrowStatus.PendingPayment
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { ExpertConfirmTimeoutHours = 2 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var slot = new ExpertSlot { Id = slotId };
            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            // Amount mismatch
            var queryLowAmount = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = orderCode.ToString(),
                ["vnp_Amount"] = "1000000",
                ["vnp_ResponseCode"] = "00"
            };
            queryLowAmount["vnp_SecureHash"] = GenerateVnPaySecureHash(queryLowAmount, hashSecret);
            var resLow = await _service.HandleVnPayIpnAsync(queryLowAmount);
            resLow.RspCode.Should().Be("04");

            // Success (00)
            var querySuccess = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = orderCode.ToString(),
                ["vnp_Amount"] = "50000000",
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = "TX_VNP_01"
            };
            querySuccess["vnp_SecureHash"] = GenerateVnPaySecureHash(querySuccess, hashSecret);
            var resOk = await _service.HandleVnPayIpnAsync(querySuccess);
            resOk.RspCode.Should().Be("00");
            escrow.EscrowStatus.Should().Be(EscrowStatus.HeldInEscrow);
            booking.Status.Should().Be(BookingStatus.AWAITING_EXPERT_APPROVAL);
            slot.IsBooked.Should().BeTrue();

            // Idempotent already confirmed
            var resConfirmed = await _service.HandleVnPayIpnAsync(querySuccess);
            resConfirmed.RspCode.Should().Be("02");
        }

        [Fact]
        public async Task UTCID21_HandleVnPayReturnAsync_ValidationsAndSuccess()
        {
            const string hashSecret = "RAOQUIASHAEJGWFNYAOMKYFISOPSPAGK";
            var orderCode = 776655L;

            // Empty
            var resNull = await _service.HandleVnPayReturnAsync(new Dictionary<string, string>());
            resNull.Should().BeNull();

            // Invalid signature
            var queryInvalid = new Dictionary<string, string> { ["vnp_TxnRef"] = "123", ["vnp_SecureHash"] = "FAKE" };
            var exSign = await Assert.ThrowsAsync<BadRequestException>(() => _service.HandleVnPayReturnAsync(queryInvalid));
            exSign.ErrorCode.Should().Be("INVALID_SIGNATURE");

            // Topup Return
            var walletId = Guid.NewGuid();
            var wallet = new UserWallet { Id = walletId, AvailableBalance = 50000m, TotalEarned = 50000m };
            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.GetByIdAsync(walletId)).ReturnsAsync(wallet);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var topUpTx = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                Amount = 200000m,
                TransactionType = "TOPUP_PENDING",
                Description = $"TOPUP_ORDER:{orderCode}"
            };
            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                      .ReturnsAsync(new List<WalletTransaction> { topUpTx });
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var queryTopUp = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = orderCode.ToString(),
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = "TOPUP_VNP_01"
            };
            queryTopUp["vnp_SecureHash"] = GenerateVnPaySecureHash(queryTopUp, hashSecret);

            var resTopUp = await _service.HandleVnPayReturnAsync(queryTopUp);
            resTopUp.Should().NotBeNull();
            resTopUp!.EscrowStatus.Should().Be(EscrowStatus.Settled);
            wallet.AvailableBalance.Should().Be(250000m);
        }

        [Fact]
        public async Task UTCID22_GetEscrowTransactionsAsync_FiltersAndSorting()
        {
            var adminId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var bookingId1 = Guid.NewGuid();
            var bookingId2 = Guid.NewGuid();

            var bookings = new List<Booking>
            {
                new Booking { Id = bookingId1, UserId = userId, BookingCode = "BK_SORT1" },
                new Booking { Id = bookingId2, UserId = userId, BookingCode = "BK_SORT2" }
            };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(bookings);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var escrows = new List<EscrowTransaction>
            {
                new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId1, Amount = 100000m, EscrowStatus = EscrowStatus.HeldInEscrow, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2), OrderCode = 111 },
                new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId2, Amount = 200000m, EscrowStatus = EscrowStatus.Settled, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), OrderCode = 222 }
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(escrows);
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var q1 = new EscrowTransactionQueryParameters { Status = EscrowStatus.HeldInEscrow, Sort = "amount" };
            var res1 = await _service.GetEscrowTransactionsAsync(adminId, "admin", q1);
            res1.Items.Should().HaveCount(1);

            var q2 = new EscrowTransactionQueryParameters { Search = "BK_SORT2", Sort = "-createdat" };
            var res2 = await _service.GetEscrowTransactionsAsync(adminId, "admin", q2);
            res2.Items.Should().HaveCount(1);
            res2.Items[0].BookingCode.Should().Be("BK_SORT2");

            // Sort tests
            var qSort = new EscrowTransactionQueryParameters { Sort = "-amount" };
            var resSort = await _service.GetEscrowTransactionsAsync(adminId, "admin", qSort);
            resSort.Items.First().Amount.Should().Be(200000m);
        }

        [Fact]
        public async Task UTCID23_RequestAdminPlatformWithdrawalAsync_ValidationsAndSuccess()
        {
            var adminId = Guid.NewGuid();
            var admin = new User { UserId = adminId, UserRole = "admin", FullName = "Admin System" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(admin);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            // Min amount
            var exMin = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestAdminPlatformWithdrawalAsync(adminId, new CreateWithdrawalRequest { Amount = 10000m }));
            exMin.ErrorCode.Should().Be("INVALID_AMOUNT");

            // Missing bank details
            var exBank = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestAdminPlatformWithdrawalAsync(adminId, new CreateWithdrawalRequest { Amount = 100000m }));
            exBank.ErrorCode.Should().Be("BANK_INFO_REQUIRED");

            // Insufficient balance
            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", AvailableBalance = 50000m, TotalEarned = 1000000m };
            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                          .ReturnsAsync(new List<UserWallet> { platformWallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var exBal = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestAdminPlatformWithdrawalAsync(adminId, new CreateWithdrawalRequest
                {
                    Amount = 100000m,
                    BankName = "MBBank",
                    AccountNumber = "123",
                    AccountName = "ADMIN"
                }));
            exBal.ErrorCode.Should().Be("INSUFFICIENT_BALANCE");

            // Success
            platformWallet.AvailableBalance = 500000m;
            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var result = await _service.RequestAdminPlatformWithdrawalAsync(adminId, new CreateWithdrawalRequest
            {
                Amount = 100000m,
                BankName = "MBBank",
                AccountNumber = "123",
                AccountName = "ADMIN"
            });

            result.Should().NotBeNull();
            result.Status.Should().Be("COMPLETED");
            platformWallet.AvailableBalance.Should().Be(400000m);
        }

        [Fact]
        public async Task UTCID24_GetAdminPlatformWithdrawalsAsync_Admin_ReturnsPagedResult()
        {
            var adminId = Guid.NewGuid();
            var admin = new User { UserId = adminId, UserRole = "admin", FullName = "Admin System" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(admin);
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User> { admin });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", TotalEarned = 1000000m };
            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                          .ReturnsAsync(new List<UserWallet> { platformWallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var withdrawalId = Guid.NewGuid();
            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                      .ReturnsAsync(new List<WalletTransaction> { new WalletTransaction { WalletId = platformWallet.Id, TransactionType = "WITHDRAWAL", ReferenceId = withdrawalId } });
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            mockWithdrawalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WithdrawalRequest, bool>>>()))
                              .ReturnsAsync(new List<WithdrawalRequest> { new WithdrawalRequest { Id = withdrawalId, UserId = adminId, Amount = 100000m, Status = "COMPLETED" } });
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var result = await _service.GetAdminPlatformWithdrawalsAsync(adminId, 1, 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].Amount.Should().Be(100000m);
        }

        #endregion
    }
}
