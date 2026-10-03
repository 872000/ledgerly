using Ledgerly.Api.Data;
using Ledgerly.Api.Models;

namespace Ledgerly.Api.Data;

public static class SeedData
{
    public static void Seed(LedgerlyContext db)
    {
        if (db.Categories.Any())
        {
            return;
        }

        db.Categories.AddRange(
            new Category { Id = Guid.NewGuid(), Name = "Salary", Kind = CategoryKind.Income },
            new Category { Id = Guid.NewGuid(), Name = "Freelance", Kind = CategoryKind.Income },
            new Category { Id = Guid.NewGuid(), Name = "Groceries", Kind = CategoryKind.Expense },
            new Category { Id = Guid.NewGuid(), Name = "Rent", Kind = CategoryKind.Expense },
            new Category { Id = Guid.NewGuid(), Name = "Transport", Kind = CategoryKind.Expense },
            new Category { Id = Guid.NewGuid(), Name = "Dining", Kind = CategoryKind.Expense },
            new Category { Id = Guid.NewGuid(), Name = "Entertainment", Kind = CategoryKind.Expense },
            new Category { Id = Guid.NewGuid(), Name = "Utilities", Kind = CategoryKind.Expense });

        db.SaveChanges();
    }
}
