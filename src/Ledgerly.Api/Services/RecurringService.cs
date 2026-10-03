using Ledgerly.Api.Data;
using Ledgerly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ledgerly.Api.Services;

/// <summary>
/// Materializes due recurring transactions into real transactions and
/// advances each schedule. Catches up on missed runs (bounded) so a laptop
/// that slept for a month still generates every missed occurrence.
/// </summary>
public sealed class RecurringService
{
    private const int MaxCatchUpRuns = 36;

    public static DateTime Advance(DateTime date, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => date.AddDays(1),
        RecurrenceFrequency.Weekly => date.AddDays(7),
        RecurrenceFrequency.Monthly => date.AddMonths(1),
        RecurrenceFrequency.Yearly => date.AddYears(1),
        _ => date.AddMonths(1)
    };

    public async Task<int> GenerateDueAsync(LedgerlyContext db, DateTime today, CancellationToken ct = default)
    {
        today = today.Date;
        var due = await db.RecurringTransactions
            .Where(r => r.IsActive && r.NextRunDate.Date <= today)
            .ToListAsync(ct);

        var generated = 0;
        foreach (var rule in due)
        {
            var runs = 0;
            while (rule.NextRunDate.Date <= today && runs < MaxCatchUpRuns)
            {
                db.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = rule.NextRunDate.Date,
                    Description = rule.Description,
                    Amount = rule.Amount,
                    CategoryId = rule.CategoryId,
                    RecurringTransactionId = rule.Id,
                    CreatedAt = DateTime.UtcNow
                });

                rule.NextRunDate = Advance(rule.NextRunDate, rule.Frequency);
                runs++;
                generated++;
            }
        }

        if (generated > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return generated;
    }
}
