using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentReminderServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<Dependent>> _mockRepo;
        private readonly DependentReminderService _service;

        public DependentReminderServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<Dependent>>();

            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(_mockRepo.Object);

            _service = new DependentReminderService(_mockUnitOfWork.Object);
        }

        #region GetAgeTransitionRemindersAsync

        [Theory]
        [InlineData(1999)]
        [InlineData(2101)]
        public async Task UTCID01_GetAgeTransitionRemindersAsync_InvalidTaxYear_ThrowsBadRequestException(int year)
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.GetAgeTransitionRemindersAsync(Guid.NewGuid(), year, new QueryParameters()));
            ex.ErrorCode.Should().Be("INVALID_TAX_YEAR");
        }

        [Fact]
        public async Task UTCID02_GetAgeTransitionRemindersAsync_DependentTurning18Soon_GeneratesReminder()
        {
            var userId = Guid.NewGuid();
            var birthDate = DateTime.UtcNow.AddYears(-18).AddDays(15); // Will turn 18 in 15 days

            var dependents = new List<Dependent>
            {
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    FullName = "Nguyễn Văn Con",
                    BirthDate = birthDate,
                    CurrentGroup = DependentGroup.CHILD_UNDER_18,
                    IsDeleted = false
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                     .ReturnsAsync(dependents);

            var query = new QueryParameters { Page = 1, Size = 10 };
            var result = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].TransitionStatus.Should().Be("TURNING_18_SOON");
        }

        [Fact]
        public async Task UTCID03_GetAgeTransitionRemindersAsync_DependentAlready18_GeneratesPendingAction()
        {
            var userId = Guid.NewGuid();
            var birthDate = DateTime.UtcNow.AddYears(-18).AddDays(-30); // Turned 18 a month ago

            var dependents = new List<Dependent>
            {
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    FullName = "Nguyễn Thị Bé",
                    BirthDate = birthDate,
                    CurrentGroup = DependentGroup.CHILD_UNDER_18,
                    IsDeleted = false
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                     .ReturnsAsync(dependents);

            var query = new QueryParameters { Page = 1, Size = 10 };
            var result = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].TransitionStatus.Should().Be("ALREADY_18_PENDING_ACTION");
        }

        [Fact]
        public async Task UTCID04_GetAgeTransitionRemindersAsync_SearchAndSorting()
        {
            var userId = Guid.NewGuid();
            var dependents = new List<Dependent>
            {
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    FullName = "Bùi Văn Bé",
                    BirthDate = DateTime.UtcNow.AddYears(-18).AddDays(10),
                    CurrentGroup = DependentGroup.CHILD_UNDER_18,
                    IsDeleted = false
                },
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    FullName = "An Văn Lớn",
                    BirthDate = DateTime.UtcNow.AddYears(-18).AddDays(20),
                    CurrentGroup = DependentGroup.CHILD_UNDER_18,
                    IsDeleted = false
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                     .ReturnsAsync(dependents);

            // Search
            var qSearch = new QueryParameters { Search = "An Văn", Page = 1, Size = 10 };
            var resSearch = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, qSearch);
            resSearch.Items.Should().HaveCount(1);
            resSearch.Items[0].FullName.Should().Be("An Văn Lớn");

            // Sort fullname
            var qSortName = new QueryParameters { Sort = "fullname", Page = 1, Size = 10 };
            var resSortName = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, qSortName);
            resSortName.Items.First().FullName.Should().Be("An Văn Lớn");

            var qSortNameDesc = new QueryParameters { Sort = "-fullname", Page = 1, Size = 10 };
            var resSortNameDesc = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, qSortNameDesc);
            resSortNameDesc.Items.First().FullName.Should().Be("Bùi Văn Bé");

            // Sort daysremaining
            var qDaysDesc = new QueryParameters { Sort = "-daysremaining", Page = 1, Size = 10 };
            var resDaysDesc = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, qDaysDesc);
            resDaysDesc.Items.First().FullName.Should().Be("An Văn Lớn");

            // Sort birthdate
            var qBirth = new QueryParameters { Sort = "birthdate", Page = 1, Size = 10 };
            var resBirth = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, qBirth);
            resBirth.Items.Should().HaveCount(2);

            // Sort transitionstatus
            var qStatus = new QueryParameters { Sort = "-transitionstatus", Page = 1, Size = 10 };
            var resStatus = await _service.GetAgeTransitionRemindersAsync(userId, DateTime.UtcNow.Year, qStatus);
            resStatus.Items.Should().HaveCount(2);
        }

        #endregion
    }
}
