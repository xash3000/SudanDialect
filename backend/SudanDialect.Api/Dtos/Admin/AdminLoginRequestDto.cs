using System.ComponentModel.DataAnnotations;

namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminLoginRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
