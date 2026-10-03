namespace Ledgerly.Api.Models;

public enum CategoryKind
{
    Income,
    Expense
}

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public sealed class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; } = CategoryKind.Expense;
}

public sealed class Transaction
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Positive = income, negative = expense.</summary>
    public decimal Amount { get; set; }
    public Guid CategoryId { get; set; }
    public string? Note { get; set; }
    public Guid? RecurringTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Budget
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
}

public sealed class RecurringTransaction
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid CategoryId { get; set; }
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    public DateTime NextRunDate { get; set; }
    public bool IsActive { get; set; } = true;
}
