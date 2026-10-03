using Microsoft.EntityFrameworkCore;
using Ledgerly.Api.Models;

namespace Ledgerly.Api.Data;

public sealed class LedgerlyContext : DbContext
{
    public LedgerlyContext(DbContextOptions<LedgerlyContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
}
