using Ledgerly.Api.Data;
using Ledgerly.Api.Dtos;
using Ledgerly.Api.Models;
using Ledgerly.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly LedgerlyContext _db;

    public CategoriesController(LedgerlyContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Category>>> List(CancellationToken ct) =>
        Ok(await _db.Categories.OrderBy(c => c.Name).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<Category>> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        if (await _db.Categories.AnyAsync(c => c.Name == request.Name, ct))
        {
            return Conflict("A category with that name already exists.");
        }

        var category = new Category { Id = Guid.NewGuid(), Name = request.Name, Kind = request.Kind };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { }, category);
    }
}

[ApiController]
[Route("api/budgets")]
public sealed class BudgetsController : ControllerBase
{
    private readonly LedgerlyContext _db;

    public BudgetsController(LedgerlyContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> List([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var budgets = await _db.Budgets.Where(b => b.Year == year && b.Month == month).ToListAsync(ct);
        var names = await _db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return Ok(budgets.Select(b => new
        {
            b.Id,
            b.CategoryId,
            Category = names.TryGetValue(b.CategoryId, out var n) ? n : "Unknown",
            b.Year,
            b.Month,
            b.Amount
        }).ToList());
    }

    /// <summary>Upserts the budget for a category/month.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Set([FromBody] SetBudgetRequest request, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return BadRequest("Unknown category.");
        }

        var budget = await _db.Budgets.FirstOrDefaultAsync(
            b => b.CategoryId == request.CategoryId && b.Year == request.Year && b.Month == request.Month, ct);

        if (budget is null)
        {
            budget = new Budget
            {
                Id = Guid.NewGuid(),
                CategoryId = request.CategoryId,
                Year = request.Year,
                Month = request.Month,
                Amount = request.Amount
            };
            _db.Budgets.Add(budget);
        }
        else
        {
            budget.Amount = request.Amount;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { budget.Id, budget.Amount });
    }
}

[ApiController]
[Route("api/recurring")]
public sealed class RecurringController : ControllerBase
{
    private readonly LedgerlyContext _db;
    private readonly RecurringService _recurring;

    public RecurringController(LedgerlyContext db, RecurringService recurring)
    {
        _db = db;
        _recurring = recurring;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> List(CancellationToken ct)
    {
        var rules = await _db.RecurringTransactions.OrderBy(r => r.NextRunDate).ToListAsync(ct);
        var names = await _db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return Ok(rules.Select(r => new
        {
            r.Id,
            r.Description,
            r.Amount,
            r.CategoryId,
            Category = names.TryGetValue(r.CategoryId, out var n) ? n : "Unknown",
            Frequency = r.Frequency.ToString(),
            r.NextRunDate,
            r.IsActive
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateRecurringRequest request, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return BadRequest("Unknown category.");
        }

        var rule = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            Frequency = request.Frequency,
            NextRunDate = request.NextRunDate.Date,
            IsActive = true
        };

        _db.RecurringTransactions.Add(rule);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { }, new { rule.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var rule = await _db.RecurringTransactions.FindAsync(new object[] { id }, ct);
        if (rule is null)
        {
            return NotFound();
        }

        _db.RecurringTransactions.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Generates transactions for every recurrence due up to today.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<object>> Run(CancellationToken ct)
    {
        var generated = await _recurring.GenerateDueAsync(_db, DateTime.Today, ct);
        return Ok(new { generated });
    }
}
