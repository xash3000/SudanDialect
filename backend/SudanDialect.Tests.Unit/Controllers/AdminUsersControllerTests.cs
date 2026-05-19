using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using System.Security.Claims;

namespace SudanDialect.Tests.Unit.Controllers;

public class AdminUsersControllerTests
{
    private readonly Mock<IAdminUserService> _adminUserServiceMock;
    private readonly AdminUsersController _sut;

    public AdminUsersControllerTests()
    {
        _adminUserServiceMock = new Mock<IAdminUserService>();
        _sut = new AdminUsersController(_adminUserServiceMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-id")
        }, "mock"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk_WithUsers()
    {
        // arrange
        var expectedUsers = new List<AdminManagedUserDto>
        {
            new AdminManagedUserDto { Id = "1", Username = "user1", Roles = new[] { "Admin" } }
        };
        _adminUserServiceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // act
        var actionResult = await _sut.GetAll(TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedUsers);
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WithUser()
    {
        // arrange
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password" };
        var createdUser = new AdminManagedUserDto { Id = "2", Username = "newuser", Roles = new[] { "Moderator" } };

        _adminUserServiceMock.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdUser);

        // act
        var actionResult = await _sut.Create(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var createdResult = actionResult.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(createdUser);
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenUpdated()
    {
        // arrange
        var id = "2";
        var request = new AdminUpsertUserRequestDto { Username = "updateduser" };
        var updatedUser = new AdminManagedUserDto { Id = "2", Username = "updateduser", Roles = new[] { "Moderator" } };

        _adminUserServiceMock.Setup(s => s.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedUser);

        // act
        var actionResult = await _sut.Update(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedUser);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenNotUpdated()
    {
        // arrange
        var id = "2";
        var request = new AdminUpsertUserRequestDto { Username = "updateduser" };

        _adminUserServiceMock.Setup(s => s.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminManagedUserDto?)null);

        // act
        var actionResult = await _sut.Update(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ShouldReturnOk_WhenDeleted()
    {
        // arrange
        var id = "other-user-id";
        _adminUserServiceMock.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var actionResult = await _sut.Delete(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Delete_ShouldReturnBadRequest_WhenDeletingOwnAccount()
    {
        // arrange
        var id = "admin-id"; // matches the one set in constructor

        // act
        var actionResult = await _sut.Delete(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Create_ShouldThrowArgumentException_WhenInvalidRequest()
    {
        // arrange
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password" };
        _adminUserServiceMock.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act & assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.Create(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_ShouldThrowArgumentException_WhenInvalidRequest()
    {
        // arrange
        var id = "2";
        var request = new AdminUpsertUserRequestDto { Username = "updateduser" };
        _adminUserServiceMock.Setup(s => s.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act & assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.Update(id, request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenNotDeleted()
    {
        // arrange
        var id = "other-user-id";
        _adminUserServiceMock.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // act
        var actionResult = await _sut.Delete(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ShouldThrowArgumentException_WhenInvalidRequest()
    {
        // arrange
        var id = "other-user-id";
        _adminUserServiceMock.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act & assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.Delete(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_ShouldWork_WhenCurrentUserIdIsMissing()
    {
        // arrange
        var id = "other-user-id";
        _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // No NameIdentifier claim
        _adminUserServiceMock.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var actionResult = await _sut.Delete(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }
}
