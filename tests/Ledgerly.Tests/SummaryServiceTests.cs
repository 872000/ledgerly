using Microsoft.EntityFrameworkCore;
using Ledgerly.Api.Data;
using Ledgerly.Api.Models;
using Ledgerly.Api.Services;

namespace Ledgerly.Tests;

public sealed class SummaryServiceTests : IDisposable
{
    private readonly LedgerlyContext _db;
    private readonly SummaryService _service = new();

    private readonly Guid _salaryId = Guid.NewGuid();
    private readonly Guid _groceryId = Guid.NewGuid();
    private readonly Guid _rentId = Guid.NewGuid();

    public SummaryServiceTests()
    {
        var options = new DbContextOptionsBuilder<LedgerlyContext>()
            .UseInMemoryDatabase($"summary-{Guid.NewGuid()}")
            .Options;
        _db = new LedgerlyContext(options);

        _db.Categories.AddRange(
            new Category { Id = _salaryId, Name = "Salary", Kind = CategoryKind.Income },
            new Category { Id = _groceryId, Name = "Groceries", Kind = CategoryKind.Expense },
            new Category { Id = _rentId, Name = "Rent", Kind = CategoryKind.Expense });

        // October transactions.
        _db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 10, 1), Description = "Paycheque", Amount = 3000m, CategoryId = _salaryId, CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 10, 1), Description = "Rent", Amount = -1500m, CategoryId = _rentId, CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 10, 5), Description = "Groceries", Amount = -200m, CategoryId = _groceryId, CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 10, 20), Description = "Groceries", Amount = -150m, CategoryId = _groceryId, CreatedAt = DateTime.UtcNow },
            // September transaction must not leak into October's summary.
            new Transaction { Id = Guid.NewGuid(), Date = new DateTime(2026, 9, 15), Description = "Old", Amount = -999m, CategoryId = _groceryId, CreatedAt = DateTime.UtcNow });

        _db.Budgets.AddRange(
            new Budget { Id = Guid.NewGuid(), CategoryId = _groceryId, Year = 2026, Month = 10, Amount = 500m },
            new Budget { Id = Guid.NewGuid(), CategoryId = _rentId, Year = 2026, Month = 10, Amount = 1500m });

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetMonthly_ComputesTotals()
    {
        var summary = await _service.GetMonthlyAsync(_db, 2026, 10);

        Assert.Equal(3000m, summary.Income);
        Assert.Equal(1850m, summary.Expenses);
        Assert.Equal(1150m, summary.Net);
    }

    [Fact]
    public async Task GetMonthly_SpendVsBudget_PerCategory()
    {
        var summary = await _service.GetMonthlyAsync(_db, 2026, 10);

        var groceries = summary.Categories.Single(c => c.CategoryName == "Groceries");
        Assert.Equal(500m, groceries.Budgeted);
        Assert.Equal(350m, groceries.Spent);
        Assert.Equal(150m, groceries.Remaining);
        Assert.Equal(70, groceries.PercentUsed);

        var rent = summary.Categories.Single(c => c.CategoryName == "Rent");
        Assert.Equal(100, rent.PercentUsed);
    }

    [Fact]
    public async Task GetMonthly_DailySeries_CoversWholeMonth()
    {
        var summary = await _service.GetMonthlyAsync(_db, 2026, 10);

        Assert.Equal(31, summary.Daily.Count);
        var first = summary.Daily[0];
        Assert.Equal(new DateTime(2026, 10, 1), first.Date);
        Assert.Equal(3000m, first.Income);
        Assert.Equal(1500m, first.Expenses);
    }

    [Fact]
    public async Task GetMonthly_ExpenseBreakdown_ExcludesIncome()
    {
        var summary = await _service.GetMonthlyAsync(_db, 2026, 10);

        Assert.DoesNotContain(summary.ExpenseBreakdown, b => b.Category == "Salary");
        Assert.Equal(1850m, summary.ExpenseBreakdown.Sum(b => b.Amount));
    }

    [Fact]
    public async Task GetMonthly_EmptyMonth_ReturnsZeros()
    {
        var summary = await _service.GetMonthlyAsync(_db, 2026, 11);

        Assert.Equal(0m, summary.Income);
        Assert.Equal(0m, summary.Expenses);
        Assert.Equal(0m, summary.Net);
        Assert.Equal(30, summary.Daily.Count);
    }
}
