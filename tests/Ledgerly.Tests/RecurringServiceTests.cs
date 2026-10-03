using Microsoft.EntityFrameworkCore;
using Ledgerly.Api.Data;
using Ledgerly.Api.Models;
using Ledgerly.Api.Services;

namespace Ledgerly.Tests;

public sealed class RecurringServiceTests : IDisposable
{
    private readonly LedgerlyContext _db;
    private readonly RecurringService _service = new();
    private readonly Guid _categoryId = Guid.NewGuid();

    public RecurringServiceTests()
    {
        var options = new DbContextOptionsBuilder<LedgerlyContext>()
            .UseInMemoryDatabase($"recur-{Guid.NewGuid()}")
            .Options;
        _db = new LedgerlyContext(options);
        _db.Categories.Add(new Category { Id = _categoryId, Name = "Rent", Kind = CategoryKind.Expense });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private RecurringTransaction AddRule(RecurrenceFrequency frequency, DateTime nextRun, bool active = true)
    {
        var rule = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Description = "Monthly rent",
            Amount = -1500m,
            CategoryId = _categoryId,
            Frequency = frequency,
            NextRunDate = nextRun,
            IsActive = active
        };
        _db.RecurringTransactions.Add(rule);
        _db.SaveChanges();
        return rule;
    }

    [Fact]
    public async Task GenerateDue_DueRule_CreatesTransactionAndAdvances()
    {
        var today = new DateTime(2026, 10, 2);
        AddRule(RecurrenceFrequency.Monthly, new DateTime(2026, 10, 1));

        var generated = await _service.GenerateDueAsync(_db, today);

        Assert.Equal(1, generated);
        var tx = Assert.Single(_db.Transactions.ToList());
        Assert.Equal(-1500m, tx.Amount);
        Assert.Equal(new DateTime(2026, 10, 1), tx.Date);
        var rule = Assert.Single(_db.RecurringTransactions.ToList());
        Assert.Equal(new DateTime(2026, 11, 1), rule.NextRunDate);
    }

    [Fact]
    public async Task GenerateDue_FutureRule_GeneratesNothing()
    {
        AddRule(RecurrenceFrequency.Monthly, new DateTime(2026, 11, 1));

        var generated = await _service.GenerateDueAsync(_db, new DateTime(2026, 10, 2));

        Assert.Equal(0, generated);
        Assert.Empty(_db.Transactions);
    }

    [Fact]
    public async Task GenerateDue_InactiveRule_GeneratesNothing()
    {
        AddRule(RecurrenceFrequency.Monthly, new DateTime(2026, 9, 1), active: false);

        var generated = await _service.GenerateDueAsync(_db, new DateTime(2026, 10, 2));

        Assert.Equal(0, generated);
    }

    [Fact]
    public async Task GenerateDue_MissedRuns_CatchUp()
    {
        // Four missed monthly runs: Jul, Aug, Sep, and Oct 1 (<= today Oct 2).
        AddRule(RecurrenceFrequency.Monthly, new DateTime(2026, 7, 1));

        var generated = await _service.GenerateDueAsync(_db, new DateTime(2026, 10, 2));

        Assert.Equal(4, generated);
        var rule = Assert.Single(_db.RecurringTransactions.ToList());
        Assert.Equal(new DateTime(2026, 11, 1), rule.NextRunDate);
    }

    [Fact]
    public async Task GenerateDue_Weekly_AdvancesBySevenDays()
    {
        // Sep 25 and Oct 2 are both <= today Oct 2, so two runs generate.
        AddRule(RecurrenceFrequency.Weekly, new DateTime(2026, 9, 25));

        var generated = await _service.GenerateDueAsync(_db, new DateTime(2026, 10, 2));

        Assert.Equal(2, generated);
        var rule = Assert.Single(_db.RecurringTransactions.ToList());
        Assert.Equal(new DateTime(2026, 10, 9), rule.NextRunDate);
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Daily, "2026-10-02", "2026-10-03")]
    [InlineData(RecurrenceFrequency.Weekly, "2026-10-02", "2026-10-09")]
    [InlineData(RecurrenceFrequency.Monthly, "2026-01-31", "2026-02-28")]
    [InlineData(RecurrenceFrequency.Yearly, "2026-10-02", "2027-10-02")]
    public void Advance_MovesDateByFrequency(RecurrenceFrequency frequency, string from, string expected)
    {
        Assert.Equal(DateTime.Parse(expected), RecurringService.Advance(DateTime.Parse(from), frequency));
    }
}
