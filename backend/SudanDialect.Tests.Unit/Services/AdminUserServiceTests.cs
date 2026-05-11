using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Services;
using SudanDialect.Api.Utilities;

namespace SudanDialect.Tests.Unit.Services;

public class AdminUserServiceTests
{
    private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
    private readonly AdminUserService _sut;

    public AdminUserServiceTests()
    {
        _userManagerMock = MockHelpers.MockUserManager<IdentityUser>();
        _sut = new AdminUserService(_userManagerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenUsernameAlreadyExists()
    {
        // arrange
        var request = new AdminUpsertUserRequestDto { Username = "existing", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("existing")).ReturnsAsync(new IdentityUser { UserName = "existing" });

        // act & assert
        await _sut.Invoking(s => s.CreateAsync(request))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*exists*");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnUser_WhenValid()
    {
        // arrange
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("newuser")).ReturnsAsync((IdentityUser?)null);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), AdminRoleNames.Moderator)).ReturnsAsync(IdentityResult.Success);

        // act
        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        // assert
        result.Username.Should().Be("newuser");
        result.Roles.Should().Contain(AdminRoleNames.Moderator);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenUserExists()
    {
        // arrange
        var user = new IdentityUser { Id = "1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        // act
        var result = await _sut.DeleteAsync("1", TestContext.Current.CancellationToken);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenUserDoesNotExist()
    {
        // arrange
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync((IdentityUser?)null);

        // act
        var result = await _sut.DeleteAsync("1", TestContext.Current.CancellationToken);

        // assert
        result.Should().BeFalse();
    }
}
