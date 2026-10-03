using Ledgerly.Api.Data;
using Ledgerly.Api.Dtos;
using Ledgerly.Api.Models;
using Ledgerly.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ledgerly.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly LedgerlyContext _db;
    private readonly CsvImporter _csv;

    public TransactionsController(LedgerlyContext db, CsvImporter csv)
    {
        _db = db;
        _csv = csv;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<object>>> List(
        [FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? categoryId, CancellationToken ct)
    {
        var query = _db.Transactions.AsQueryable();

        if (year.HasValue && month.HasValue)
        {
            var start = new DateTime(year.Value, month.Value, 1);
            query = query.Where(t => t.Date >= start && t.Date < start.AddMonths(1));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        var tx = await query.OrderByDescending(t => t.Date).Take(500).ToListAsync(ct);
        var names = await _db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return Ok(tx.Select(t => new
        {
            t.Id,
            t.Date,
            t.Description,
            t.Amount,
            t.CategoryId,
            Category = names.TryGetValue(t.CategoryId, out var n) ? n : "Unknown",
            t.Note
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return BadRequest("Unknown category.");
        }

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Date = request.Date.Date,
            Description = request.Description,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { }, new { tx.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FindAsync(new object[] { id }, ct);
        if (tx is null)
        {
            return NotFound();
        }

        _db.Transactions.Remove(tx);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Imports a bank CSV export (date,description,amount,category). Unknown
    /// categories are created as expense categories automatically.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        IReadOnlyList<ImportRow> rows;
        try
        {
            await using var stream = file.OpenReadStream();
            rows = _csv.Parse(stream);
        }
        catch (CsvFormatException ex)
        {
            return BadRequest(ex.Message);
        }

        var categories = await _db.Categories.ToListAsync(ct);
        var byName = categories.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        foreach (var row in rows)
        {
            if (!byName.TryGetValue(row.Category, out var category))
            {
                category = new Category { Id = Guid.NewGuid(), Name = row.Category, Kind = CategoryKind.Expense };
                _db.Categories.Add(category);
                byName[row.Category] = category;
            }

            _db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Date = row.Date,
                Description = row.Description,
                Amount = row.Amount,
                CategoryId = category.Id,
                CreatedAt = DateTime.UtcNow
            });
            created++;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { imported = created });
    }
}
