using System.Text;
using Ledgerly.Api.Services;

namespace Ledgerly.Tests;

public sealed class CsvImporterTests
{
    private readonly CsvImporter _importer = new();

    private static MemoryStream Csv(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Parse_BasicRows_ReturnsRows()
    {
        var rows = _importer.Parse(Csv("2026-09-01,Paycheque,2500.00,Salary\n2026-09-02,Grocery run,-84.12,Groceries\n"));

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateTime(2026, 9, 1), rows[0].Date);
        Assert.Equal("Paycheque", rows[0].Description);
        Assert.Equal(2500.00m, rows[0].Amount);
        Assert.Equal("Salary", rows[0].Category);
        Assert.Equal(-84.12m, rows[1].Amount);
    }

    [Fact]
    public void Parse_SkipsHeaderRow()
    {
        var rows = _importer.Parse(Csv("date,description,amount,category\n2026-09-01,Coffee,-4.50,Dining\n"));

        Assert.Single(rows);
        Assert.Equal("Coffee", rows[0].Description);
    }

    [Fact]
    public void Parse_QuotedFieldWithComma_KeepsTogether()
    {
        var rows = _importer.Parse(Csv("2026-09-03,\"Dinner at Joe's, downtown\",-62.40,Dining\n"));

        Assert.Single(rows);
        Assert.Equal("Dinner at Joe's, downtown", rows[0].Description);
        Assert.Equal(-62.40m, rows[0].Amount);
    }

    [Fact]
    public void Parse_EscapedQuotes_Unescape()
    {
        var rows = _importer.Parse(Csv("2026-09-03,\"Book \"\"Clean Code\"\"\",-39.99,Entertainment\n"));

        Assert.Single(rows);
        Assert.Equal("Book \"Clean Code\"", rows[0].Description);
    }

    [Fact]
    public void Parse_ParenthesizedAmount_IsNegative()
    {
        var rows = _importer.Parse(Csv("2026-09-04,Refund,($12.50),Groceries\n"));

        Assert.Single(rows);
        Assert.Equal(-12.50m, rows[0].Amount);
    }

    [Fact]
    public void Parse_CurrencySymbol_Stripped()
    {
        var rows = _importer.Parse(Csv("2026-09-04,Concert,$89.00,Entertainment\n"));

        Assert.Single(rows);
        Assert.Equal(89.00m, rows[0].Amount);
    }

    [Fact]
    public void Parse_BlankLines_Skipped()
    {
        var rows = _importer.Parse(Csv("\n2026-09-05,Bus,-3.25,Transport\n\n"));

        Assert.Single(rows);
    }

    [Fact]
    public void Parse_TooFewColumns_Throws()
    {
        Assert.Throws<CsvFormatException>(() => _importer.Parse(Csv("2026-09-05,Bus,-3.25\n")));
    }

    [Fact]
    public void Parse_BadDate_Throws()
    {
        Assert.Throws<CsvFormatException>(() => _importer.Parse(Csv("not-a-date,Bus,-3.25,Transport\n")));
    }

    [Fact]
    public void Parse_BadAmount_Throws()
    {
        Assert.Throws<CsvFormatException>(() => _importer.Parse(Csv("2026-09-05,Bus,abc,Transport\n")));
    }

    [Fact]
    public void Parse_EmptyDescription_Throws()
    {
        Assert.Throws<CsvFormatException>(() => _importer.Parse(Csv("2026-09-05,,-3.25,Transport\n")));
    }
}
