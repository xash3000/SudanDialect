using System.ComponentModel.DataAnnotations;

namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminUpdateWordRequestDto
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Headword { get; init; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Definition { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}
