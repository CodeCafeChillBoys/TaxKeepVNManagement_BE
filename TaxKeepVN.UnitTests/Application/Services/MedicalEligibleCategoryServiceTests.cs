using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.MedicalCategories;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class MedicalEligibleCategoryServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<MedicalEligibleCategory>> _mockRepo;
        private readonly Mock<ILogger<MedicalEligibleCategoryService>> _mockLogger;
        private readonly MedicalEligibleCategoryService _service;

        public MedicalEligibleCategoryServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<MedicalEligibleCategory>>();
            _mockLogger = new Mock<ILogger<MedicalEligibleCategoryService>>();

            _mockUnitOfWork.Setup(u => u.Repository<MedicalEligibleCategory>()).Returns(_mockRepo.Object);

            _service = new MedicalEligibleCategoryService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task GetAllAsync_WhenQueryIsNull_ReturnsOnlyActiveCategoriesOrderedBySectionAndCode()
        {
            // Arrange
            var list = new List<MedicalEligibleCategory>
            {
                new() { Id = Guid.NewGuid(), SectionCode = "B", CategoryCode = "02", CategoryName = "Cat 02", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "03", CategoryName = "Cat 03", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "01", CategoryName = "Cat 01", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "04", CategoryName = "Cat 04 (deleted)", IsDeleted = true }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            // Act
            var result = (await _service.GetAllAsync(null)).ToList();

            // Assert
            result.Should().HaveCount(3);
            result[0].CategoryCode.Should().Be("01");
            result[0].SectionCode.Should().Be("A");
            result[1].CategoryCode.Should().Be("03");
            result[1].SectionCode.Should().Be("A");
            result[2].CategoryCode.Should().Be("02");
            result[2].SectionCode.Should().Be("B");
        }

        [Fact]
        public async Task GetAllAsync_WhenIncludeDeletedIsTrue_ReturnsBothActiveAndDeleted()
        {
            // Arrange
            var list = new List<MedicalEligibleCategory>
            {
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "01", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "02", IsDeleted = true }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            // Act
            var result = (await _service.GetAllAsync(new MedicalCategoryQueryParameters { IncludeDeleted = true })).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAllAsync_WhenFilteredByIsEligible_ReturnsMatchingOnly()
        {
            // Arrange
            var list = new List<MedicalEligibleCategory>
            {
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "01", IsEligible = true, IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "02", IsEligible = false, IsDeleted = false }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            // Act
            var resultEligible = (await _service.GetAllAsync(new MedicalCategoryQueryParameters { IsEligible = true })).ToList();
            var resultIneligible = (await _service.GetAllAsync(new MedicalCategoryQueryParameters { IsEligible = false })).ToList();

            // Assert
            resultEligible.Should().ContainSingle(c => c.CategoryCode == "01");
            resultIneligible.Should().ContainSingle(c => c.CategoryCode == "02");
        }

        [Fact]
        public async Task GetAllAsync_WhenFilteredBySectionCode_ReturnsMatchingSectionCaseInsensitive()
        {
            // Arrange
            var list = new List<MedicalEligibleCategory>
            {
                new() { Id = Guid.NewGuid(), SectionCode = "SEC-A", CategoryCode = "01", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "SEC-B", CategoryCode = "02", IsDeleted = false }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            // Act
            var result = (await _service.GetAllAsync(new MedicalCategoryQueryParameters { SectionCode = " sec-a " })).ToList();

            // Assert
            result.Should().ContainSingle(c => c.CategoryCode == "01");
        }

        [Fact]
        public async Task GetAllAsync_WhenFilteredBySearch_MatchesCodeNameKeywordsOrDescription()
        {
            // Arrange
            var list = new List<MedicalEligibleCategory>
            {
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "MATCH_CODE", CategoryName = "Name A", Keywords = "kw", Description = "desc", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "OTHER1", CategoryName = "Special Name", Keywords = "kw", Description = "desc", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "OTHER2", CategoryName = "Name B", Keywords = "TargetKeyword", Description = "desc", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "OTHER3", CategoryName = "Name C", Keywords = "kw", Description = "TargetDescription", IsDeleted = false },
                new() { Id = Guid.NewGuid(), SectionCode = "A", CategoryCode = "NOMATCH", CategoryName = "None", Keywords = null, Description = null, IsDeleted = false }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            // Act & Assert
            var resCode = await _service.GetAllAsync(new MedicalCategoryQueryParameters { Search = "match_code" });
            resCode.Should().ContainSingle(c => c.CategoryCode == "MATCH_CODE");

            var resName = await _service.GetAllAsync(new MedicalCategoryQueryParameters { Search = "special" });
            resName.Should().ContainSingle(c => c.CategoryCode == "OTHER1");

            var resKw = await _service.GetAllAsync(new MedicalCategoryQueryParameters { Search = "targetkeyword" });
            resKw.Should().ContainSingle(c => c.CategoryCode == "OTHER2");

            var resDesc = await _service.GetAllAsync(new MedicalCategoryQueryParameters { Search = "targetdescription" });
            resDesc.Should().ContainSingle(c => c.CategoryCode == "OTHER3");
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task GetByIdAsync_WhenCategoryExistsAndActive_ReturnsMappedDto()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                CategoryName = "Chi phí khám bệnh",
                SectionCode = "I",
                IsEligible = true,
                Description = "Mô tả",
                Keywords = "khám bệnh",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act
            var result = await _service.GetByIdAsync(id);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(id);
            result.CategoryCode.Should().Be("CAT01");
            result.CategoryName.Should().Be("Chi phí khám bệnh");
            result.SectionCode.Should().Be("I");
            result.IsEligible.Should().BeTrue();
            result.Description.Should().Be("Mô tả");
            result.Keywords.Should().Be("khám bệnh");
            result.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task GetByIdAsync_WhenCategoryNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((MedicalEligibleCategory?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(id));
        }

        [Fact]
        public async Task GetByIdAsync_WhenCategoryIsDeleted_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                IsDeleted = true
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(id));
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task CreateAsync_WhenCategoryCodeAlreadyExistsAndActive_ThrowsConflictException()
        {
            // Arrange
            var dto = new CreateMedicalEligibleCategoryDto
            {
                CategoryCode = "cat01",
                CategoryName = "Tên",
                SectionCode = "I"
            };
            var existing = new MedicalEligibleCategory
            {
                CategoryCode = "CAT01",
                IsDeleted = false
            };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalEligibleCategory, bool>>>()))
                     .ReturnsAsync(new List<MedicalEligibleCategory> { existing });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("DUPLICATE_CATEGORY_CODE");
        }

        [Fact]
        public async Task CreateAsync_WhenCategoryCodeExistsAndSoftDeleted_ReactivatesAndUpdatesFields()
        {
            // Arrange
            var dto = new CreateMedicalEligibleCategoryDto
            {
                CategoryCode = "cat01",
                CategoryName = "Tên Mới",
                SectionCode = "ii",
                IsEligible = true,
                Description = "Mô tả mới",
                Keywords = "từ khóa mới"
            };
            var existing = new MedicalEligibleCategory
            {
                Id = Guid.NewGuid(),
                CategoryCode = "CAT01",
                CategoryName = "Tên Cũ",
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow.AddDays(-5)
            };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalEligibleCategory, bool>>>()))
                     .ReturnsAsync(new List<MedicalEligibleCategory> { existing });

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.CategoryCode.Should().Be("CAT01");
            result.CategoryName.Should().Be("Tên Mới");
            result.SectionCode.Should().Be("II");
            result.IsEligible.Should().BeTrue();
            result.Description.Should().Be("Mô tả mới");
            result.Keywords.Should().Be("từ khóa mới");
            result.IsDeleted.Should().BeFalse();
            result.DeletedAt.Should().BeNull();

            _mockRepo.Verify(r => r.Update(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenCategoryCodeIsNew_CreatesNewEntityAndReturnsDto()
        {
            // Arrange
            var dto = new CreateMedicalEligibleCategoryDto
            {
                CategoryCode = "cat02",
                CategoryName = "Tên Hợp Lệ",
                SectionCode = "i",
                IsEligible = true,
                Description = "Mô tả",
                Keywords = "từ khóa"
            };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<MedicalEligibleCategory, bool>>>()))
                     .ReturnsAsync(new List<MedicalEligibleCategory>());

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.CategoryCode.Should().Be("CAT02");
            result.CategoryName.Should().Be("Tên Hợp Lệ");
            result.SectionCode.Should().Be("I");
            result.IsEligible.Should().BeTrue();
            result.Description.Should().Be("Mô tả");
            result.Keywords.Should().Be("từ khóa");
            result.IsDeleted.Should().BeFalse();

            _mockRepo.Verify(r => r.AddAsync(It.Is<MedicalEligibleCategory>(c =>
                c.CategoryCode == "CAT02" &&
                c.CategoryName == "Tên Hợp Lệ" &&
                c.SectionCode == "I" &&
                !c.IsDeleted)), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UpdateAsync_WhenCategoryNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((MedicalEligibleCategory?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(id, new UpdateMedicalEligibleCategoryDto()));
        }

        [Fact]
        public async Task UpdateAsync_WhenCategoryIsDeleted_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory { Id = id, IsDeleted = true };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(id, new UpdateMedicalEligibleCategoryDto()));
        }

        [Fact]
        public async Task UpdateAsync_WhenCategoryExists_UpdatesProvidedFieldsAndReturnsDto()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                CategoryName = "Tên Cũ",
                SectionCode = "I",
                IsEligible = true,
                Description = "Desc Cũ",
                Keywords = "Kw Cũ",
                IsDeleted = false
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            var updateDto = new UpdateMedicalEligibleCategoryDto
            {
                CategoryName = " Tên Mới ",
                SectionCode = " ii ",
                IsEligible = false,
                Description = " Desc Mới ",
                Keywords = " Kw Mới "
            };

            // Act
            var result = await _service.UpdateAsync(id, updateDto);

            // Assert
            result.CategoryName.Should().Be("Tên Mới");
            result.SectionCode.Should().Be("II");
            result.IsEligible.Should().BeFalse();
            result.Description.Should().Be("Desc Mới");
            result.Keywords.Should().Be("Kw Mới");

            _mockRepo.Verify(r => r.Update(category), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenOptionalFieldsAreNullOrWhitespace_DoesNotOverwriteExistingValues()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                CategoryName = "Tên Cũ",
                SectionCode = "I",
                IsEligible = true,
                Description = "Desc Cũ",
                Keywords = "Kw Cũ",
                IsDeleted = false
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            var updateDto = new UpdateMedicalEligibleCategoryDto
            {
                CategoryName = "   ",
                SectionCode = null,
                IsEligible = null,
                Description = null,
                Keywords = null
            };

            // Act
            var result = await _service.UpdateAsync(id, updateDto);

            // Assert
            result.CategoryName.Should().Be("Tên Cũ");
            result.SectionCode.Should().Be("I");
            result.IsEligible.Should().BeTrue();
            result.Description.Should().Be("Desc Cũ");
            result.Keywords.Should().Be("Kw Cũ");
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_WhenCategoryNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((MedicalEligibleCategory?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(id));
        }

        [Fact]
        public async Task DeleteAsync_WhenCategoryAlreadyDeleted_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory { Id = id, IsDeleted = true };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(id));
        }

        [Fact]
        public async Task DeleteAsync_WhenCategoryExistsAndActive_SoftDeletesAndReturnsTrue()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                IsDeleted = false,
                DeletedAt = null
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act
            var result = await _service.DeleteAsync(id);

            // Assert
            result.Should().BeTrue();
            category.IsDeleted.Should().BeTrue();
            category.DeletedAt.Should().NotBeNull();

            _mockRepo.Verify(r => r.Update(category), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region RestoreAsync

        [Fact]
        public async Task RestoreAsync_WhenCategoryNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var id = Guid.NewGuid();
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((MedicalEligibleCategory?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.RestoreAsync(id));
        }

        [Fact]
        public async Task RestoreAsync_WhenCategoryAlreadyActive_ReturnsTrueWithoutUpdating()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                IsDeleted = false
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act
            var result = await _service.RestoreAsync(id);

            // Assert
            result.Should().BeTrue();
            _mockRepo.Verify(r => r.Update(It.IsAny<MedicalEligibleCategory>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task RestoreAsync_WhenCategoryIsSoftDeleted_RestoresSuccessfullyAndReturnsTrue()
        {
            // Arrange
            var id = Guid.NewGuid();
            var category = new MedicalEligibleCategory
            {
                Id = id,
                CategoryCode = "CAT01",
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow.AddDays(-3)
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(category);

            // Act
            var result = await _service.RestoreAsync(id);

            // Assert
            result.Should().BeTrue();
            category.IsDeleted.Should().BeFalse();
            category.DeletedAt.Should().BeNull();

            _mockRepo.Verify(r => r.Update(category), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
