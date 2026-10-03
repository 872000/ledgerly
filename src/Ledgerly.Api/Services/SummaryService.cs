using Ledgerly.Api.Data;
using Ledgerly.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ledgerly.Api.Services;

public sealed record CategorySpend(
    Guid CategoryId,
    string CategoryName,
    decimal Budgeted,
    decimal Spent,
    decimal Remaining,
    double PercentUsed);

public sealed record MonthlySummary(
    int Year,
    int Month,
    decimal Income,
    decimal Expenses,
    decimal Net,
    IReadOnlyList<CategorySpend> Categories,
    IReadOnlyList<DailyPoint> Daily,
    IReadOnlyList<CategorySlice> ExpenseBreakdown);

public sealed record DailyPoint(DateTime Date, decimal Income, decimal Expenses);
public sealed record CategorySlice(string Category, decimal Amount);

/// <summary>
/// Builds monthly aggregates: totals, per-category spend vs. budget, a daily
/// income/expense series, and an expense breakdown — all chart-ready JSON.
/// </summary>
public sealed class SummaryService
{
    public async Task<MonthlySummary> GetMonthlyAsync(LedgerlyContext db, int year, int month, CancellationToken ct = default)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        var transactions = await db.Transactions
            .Where(t => t.Date >= start && t.Date < end)
            .ToListAsync(ct);

        var categories = await db.Categories.ToListAsync(ct);
        var budgets = await db.Budgets
            .Where(b => b.Year == year && b.Month == month)
            .ToListAsync(ct);

        var income = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        var expenses = -transactions.Where(t => t.Amount < 0).Sum(t => t.Amount);

        var perCategory = categories
            .Select(c =>
            {
                var spent = -transactions.Where(t => t.CategoryId == c.Id && t.Amount < 0).Sum(t => t.Amount);
                var budgeted = budgets.FirstOrDefault(b => b.CategoryId == c.Id)?.Amount ?? 0m;
                var percent = budgeted > 0 ? (double)(spent / budgeted) * 100 : 0;
                return new CategorySpend(c.Id, c.Name, budgeted, spent, budgeted - spent, Math.Round(percent, 1));
            })
            .OrderByDescending(c => c.Spent)
            .ToList();

        var days = (end - start).Days;
        var daily = Enumerable.Range(0, days)
            .Select(i =>
            {
                var day = start.AddDays(i);
                var dayTx = transactions.Where(t => t.Date.Date == day.Date);
                return new DailyPoint(
                    day,
                    dayTx.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    -dayTx.Where(t => t.Amount < 0).Sum(t => t.Amount));
            })
            .ToList();

        var breakdown = perCategory
            .Where(c => c.Spent > 0)
            .Select(c => new CategorySlice(c.CategoryName, c.Spent))
            .ToList();

        return new MonthlySummary(year, month, income, expenses, income - expenses, perCategory, daily, breakdown);
    }
}
