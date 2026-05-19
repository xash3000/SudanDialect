using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Utilities;
using System.Security.Claims;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Authorize]
[Authorize(Roles = AdminRoleNames.Admin)]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<AdminManagedUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AdminManagedUserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _adminUserService.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminManagedUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminManagedUserDto>> Create(
        [FromBody] AdminUpsertUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var createdUser = await _adminUserService.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/users/{createdUser.Id}", createdUser);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AdminManagedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminManagedUserDto>> Update(
        [FromRoute] string id,
        [FromBody] AdminUpsertUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var updatedUser = await _adminUserService.UpdateAsync(id, request, cancellationToken);
        if (updatedUser is null)
        {
            return NotFound();
        }

        return Ok(updatedUser);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> Delete([FromRoute] string id, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(currentUserId, id, StringComparison.Ordinal))
        {
            return BadRequest(new { error = "Cannot delete your own account." });
        }

        var deleted = await _adminUserService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return Ok(new { id, deleted = true });
    }
}
