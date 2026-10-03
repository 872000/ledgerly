using Ledgerly.Api.Data;
using Ledgerly.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly LedgerlyContext _db;
    private readonly SummaryService _summary;

    public ReportsController(LedgerlyContext db, SummaryService summary)
    {
        _db = db;
        _summary = summary;
    }

    /// <summary>
    /// Monthly summary: income/expense/net totals, per-category spend vs.
    /// budget, a daily income/expense series, and an expense breakdown —
    /// everything the dashboard charts need in one call.
    /// </summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<object>> Monthly([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month is < 1 or > 12)
        {
            return BadRequest("Month must be between 1 and 12.");
        }

        var summary = await _summary.GetMonthlyAsync(_db, year, month, ct);
        return Ok(new
        {
            summary.Year,
            summary.Month,
            summary.Income,
            summary.Expenses,
            summary.Net,
            Categories = summary.Categories.Select(c => new
            {
                c.CategoryId,
                c.CategoryName,
                c.Budgeted,
                c.Spent,
                c.Remaining,
                c.PercentUsed
            }),
            Daily = summary.Daily.Select(d => new
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                d.Income,
                d.Expenses
            }),
            ExpenseBreakdown = summary.ExpenseBreakdown.Select(b => new { b.Category, b.Amount })
        });
    }
}
