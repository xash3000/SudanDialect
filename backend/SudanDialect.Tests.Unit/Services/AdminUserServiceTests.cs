using System.Linq.Expressions;
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

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoUsers()
    {
        var mockUsers = CreateMockQueryable(new List<IdentityUser>());
        _userManagerMock.Setup(u => u.Users).Returns(mockUsers);
        _userManagerMock.Setup(u => u.GetRolesAsync(It.IsAny<IdentityUser>())).ReturnsAsync(new List<string>());

        var result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnUsersWithRoles_WhenUsersExist()
    {
        var users = new List<IdentityUser>
        {
            new() { Id = "1", UserName = "user1" },
            new() { Id = "2", UserName = "user2" }
        };
        var mockUsers = CreateMockQueryable(users);
        _userManagerMock.Setup(u => u.Users).Returns(mockUsers);
        _userManagerMock.Setup(u => u.GetRolesAsync(It.IsAny<IdentityUser>())).ReturnsAsync(new List<string> { "admin" });

        var result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.Roles.Should().Contain("admin"));
    }

    [Fact]
    public async Task GetAllAsync_ShouldAssignDefaultRole_WhenUserHasNoRoles()
    {
        var user = new IdentityUser { Id = "1", UserName = "newuser" };
        var mockUsers = CreateMockQueryable(new List<IdentityUser> { user });
        _userManagerMock.Setup(u => u.Users).Returns(mockUsers);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.AddToRoleAsync(user, AdminRoleNames.Moderator)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result.First().Roles.Should().Contain(AdminRoleNames.Moderator);
    }

    private static IQueryable<IdentityUser> CreateMockQueryable(List<IdentityUser> data)
    {
        return new MockQueryable<IdentityUser>(data);
    }

    private class MockQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>, IOrderedQueryable<T>
    {
        private readonly List<T> _data;

        public MockQueryable(List<T> data) => _data = data;

        public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _data.GetEnumerator();
        public Expression Expression => Expression.Constant(this);
        public Type ElementType => typeof(T);
        public IQueryProvider Provider => new MockQueryProvider<T>(_data);

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new MockAsyncEnumerator<T>(_data.GetEnumerator());
        }
    }

    private class MockQueryProvider<T> : IQueryProvider
    {
        private readonly List<T> _data;
        public MockQueryProvider(List<T> data) => _data = data;
        public IQueryable CreateQuery(Expression expression) => new MockQueryable<T>(_data);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => (IQueryable<TElement>)new MockQueryable<T>(_data);
        public object Execute(Expression expression) => _data;
        public TResult Execute<TResult>(Expression expression) => (TResult)(object)_data;
    }

    private class MockAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public MockAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());
        public T Current => _inner.Current;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenUsernameAlreadyExists()
    {
        var request = new AdminUpsertUserRequestDto { Username = "existing", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("existing")).ReturnsAsync(new IdentityUser { UserName = "existing" });

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*exists*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenUsernameIsNull()
    {
        var request = new AdminUpsertUserRequestDto { Username = null!, Password = "password123" };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenUsernameIsEmpty()
    {
        var request = new AdminUpsertUserRequestDto { Username = "", Password = "password123" };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenUsernameIsWhitespace()
    {
        var request = new AdminUpsertUserRequestDto { Username = "   ", Password = "password123" };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenUsernameLessThan3Chars()
    {
        var request = new AdminUpsertUserRequestDto { Username = "ab", Password = "password123" };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*3 characters*");
    }

    [Fact]
    public async Task CreateAsync_ShouldTrimUsername()
    {
        var request = new AdminUpsertUserRequestDto { Username = "  user  ", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("user")).ReturnsAsync((IdentityUser?)null);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), AdminRoleNames.Moderator)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Username.Should().Be("user");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenPasswordIsNull()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = null! };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenPasswordIsEmpty()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "" };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenPasswordIsWhitespace()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "   " };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenPasswordLessThan8Chars()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "pass123" };

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*8 characters*");
    }

    [Fact]
    public async Task CreateAsync_ShouldTrimPassword()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "  password123  " };
        _userManagerMock.Setup(u => u.FindByNameAsync("newuser")).ReturnsAsync((IdentityUser?)null);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), AdminRoleNames.Moderator)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenCreateFails()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("newuser")).ReturnsAsync((IdentityUser?)null);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "password123"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Error", Description = "Create failed" }));

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Create*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenAddRoleFails()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("newuser")).ReturnsAsync((IdentityUser?)null);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), AdminRoleNames.Moderator))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Error", Description = "Role failed" }));

        var act = () => _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Role*");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnUser_WhenValid()
    {
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("newuser")).ReturnsAsync((IdentityUser?)null);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), AdminRoleNames.Moderator)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.CreateAsync(request, TestContext.Current.CancellationToken);

        result.Username.Should().Be("newuser");
        result.Roles.Should().Contain(AdminRoleNames.Moderator);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenCancellationTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var request = new AdminUpsertUserRequestDto { Username = "newuser", Password = "password123" };
        _userManagerMock.Setup(u => u.FindByNameAsync("newuser")).ReturnsAsync((IdentityUser?)null);

        var act = () => _sut.CreateAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenIdIsNull()
    {
        var request = new AdminUpsertUserRequestDto { Username = "user", Password = "password123" };

        var act = () => _sut.UpdateAsync(null!, request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenIdIsEmpty()
    {
        var request = new AdminUpsertUserRequestDto { Username = "user", Password = "password123" };

        var act = () => _sut.UpdateAsync("", request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenIdIsWhitespace()
    {
        var request = new AdminUpsertUserRequestDto { Username = "user", Password = "password123" };

        var act = () => _sut.UpdateAsync("   ", request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenUserNotFound()
    {
        _userManagerMock.Setup(u => u.FindByIdAsync("999")).ReturnsAsync((IdentityUser?)null);

        var result = await _sut.UpdateAsync("999", new AdminUpsertUserRequestDto { Username = "user", Password = "password123" }, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenUsernameExistsForDifferentUser()
    {
        var user = new IdentityUser { Id = "1", UserName = "user1" };
        var otherUser = new IdentityUser { Id = "2", UserName = "taken" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.FindByNameAsync("taken")).ReturnsAsync(otherUser);

        var act = () => _sut.UpdateAsync("1", new AdminUpsertUserRequestDto { Username = "taken", Password = "password123" }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*exists*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSucceed_WhenUsernameExistsForSameUser()
    {
        var user = new IdentityUser { Id = "1", UserName = "user1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.FindByNameAsync("user1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");
        _userManagerMock.Setup(u => u.ResetPasswordAsync(user, "token", "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "admin" });

        var result = await _sut.UpdateAsync("1", new AdminUpsertUserRequestDto { Username = "user1", Password = "password123" }, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Username.Should().Be("user1");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenUpdateFails()
    {
        var user = new IdentityUser { Id = "1", UserName = "user1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.FindByNameAsync("user1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Error", Description = "Update failed" }));

        var act = () => _sut.UpdateAsync("1", new AdminUpsertUserRequestDto { Username = "user1", Password = "password123" }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenPasswordResetFails()
    {
        var user = new IdentityUser { Id = "1", UserName = "user1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.FindByNameAsync("user1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");
        _userManagerMock.Setup(u => u.ResetPasswordAsync(user, "token", "password123"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Error", Description = "Password failed" }));

        var act = () => _sut.UpdateAsync("1", new AdminUpsertUserRequestDto { Username = "user1", Password = "password123" }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*password*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldAssignDefaultRole_WhenUserHasNoRoles()
    {
        var user = new IdentityUser { Id = "1", UserName = "user1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.FindByNameAsync("user1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");
        _userManagerMock.Setup(u => u.ResetPasswordAsync(user, "token", "password123")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _userManagerMock.Setup(u => u.AddToRoleAsync(user, AdminRoleNames.Moderator)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.UpdateAsync("1", new AdminUpsertUserRequestDto { Username = "user1", Password = "password123" }, TestContext.Current.CancellationToken);

        result!.Roles.Should().Contain(AdminRoleNames.Moderator);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenCancellationTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var user = new IdentityUser { Id = "1", UserName = "user1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);

        var act = () => _sut.UpdateAsync("1", new AdminUpsertUserRequestDto { Username = "user1", Password = "password123" }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenIdIsNull()
    {
        var act = () => _sut.DeleteAsync(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenIdIsEmpty()
    {
        var act = () => _sut.DeleteAsync("", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenIdIsWhitespace()
    {
        var act = () => _sut.DeleteAsync("   ", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenUserDoesNotExist()
    {
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync((IdentityUser?)null);

        var result = await _sut.DeleteAsync("1", TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenUserExists()
    {
        var user = new IdentityUser { Id = "1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.DeleteAsync("1", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenDeleteFails()
    {
        var user = new IdentityUser { Id = "1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(u => u.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Error", Description = "Delete failed" }));

        var act = () => _sut.DeleteAsync("1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*delete*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrow_WhenCancellationTokenAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var user = new IdentityUser { Id = "1" };
        _userManagerMock.Setup(u => u.FindByIdAsync("1")).ReturnsAsync(user);

        var act = () => _sut.DeleteAsync("1", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}