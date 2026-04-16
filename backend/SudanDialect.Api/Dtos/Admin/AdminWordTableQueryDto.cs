using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminWordTableQueryDto
{
    [Range(1, int.MaxValue)]
    [FromQuery(Name = "page")]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = 20;

    [StringLength(200)]
    [FromQuery(Name = "query")]
    public string? Query { get; set; }

    [StringLength(20)]
    [FromQuery(Name = "searchBy")]
    public string SearchBy { get; set; } = "headword";

    [FromQuery(Name = "isActive")]
    public bool? IsActive { get; set; }

    [StringLength(40)]
    [FromQuery(Name = "sortBy")]
    public string SortBy { get; set; } = "updatedAt";

    [StringLength(10)]
    [FromQuery(Name = "sortDirection")]
    public string SortDirection { get; set; } = "desc";
}
