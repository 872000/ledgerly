using System.ComponentModel.DataAnnotations;
using Ledgerly.Api.Models;

namespace Ledgerly.Api.Dtos;

public sealed class CreateCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; } = CategoryKind.Expense;
}

public sealed class CreateTransactionRequest
{
    [Required]
    public DateTime Date { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public sealed class SetBudgetRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Range(0, 1_000_000)]
    public decimal Amount { get; set; }
}

public sealed class CreateRecurringRequest
{
    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    [Required]
    public DateTime NextRunDate { get; set; }
}
