using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Ledgerly.Api.Data;

namespace Ledgerly.Tests;

public sealed class LedgerlyFactory : WebApplicationFactory<Program>
{
    // One shared in-memory store for every service provider this factory builds,
    // so startup seeding and request handling always see the same data.
    private readonly InMemoryDatabaseRoot _databaseRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var root = _databaseRoot;
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<LedgerlyContext>));
            services.Remove(descriptor);
            services.AddDbContext<LedgerlyContext>(options =>
                options.UseInMemoryDatabase("LedgerlyTests", root));
        });
    }
}

public sealed class LedgerlyApiTests : IDisposable
{
    private readonly LedgerlyFactory _factory = new();
    private readonly HttpClient _client;
    private bool _disposed;

    public LedgerlyApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _factory.Dispose();
            _disposed = true;
        }
    }

    [Fact]
    public async Task CreateTransaction_ThenList_ReturnsIt()
    {
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/categories");
        var categoryId = categories.EnumerateArray().First().GetProperty("id").GetGuid();

        var create = await _client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-10-02",
            description = "Test coffee",
            amount = -4.50m,
            categoryId
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/transactions?year=2026&month=10");
        Assert.Contains(list.EnumerateArray(),
            e => e.GetProperty("description").GetString() == "Test coffee");
    }

    [Fact]
    public async Task CreateTransaction_UnknownCategory_BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-10-02",
            description = "Nope",
            amount = -1m,
            categoryId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportCsv_CreatesTransactionsAndCategories()
    {
        var csv = "date,description,amount,category\n2026-10-01,Paycheque,2500.00,Salary\n2026-10-02,\"Lunch, downtown\",-14.25,Dining\n";
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "export.csv");

        var response = await _client.PostAsync("/api/transactions/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, body.GetProperty("imported").GetInt32());

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/transactions?year=2026&month=10");
        Assert.Contains(list.EnumerateArray(),
            e => e.GetProperty("description").GetString() == "Lunch, downtown");
    }

    [Fact]
    public async Task ImportCsv_MalformedFile_BadRequest()
    {
        var csv = "2026-10-01,Broken\n";
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "export.csv");

        var response = await _client.PostAsync("/api/transactions/import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MonthlyReport_HasChartReadyShape()
    {
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/categories");
        var groceries = categories.EnumerateArray().First(e => e.GetProperty("name").GetString() == "Groceries");
        var categoryId = groceries.GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync("/api/budgets", new { categoryId, year = 2026, month = 10, amount = 400m });
        await _client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-10-03",
            description = "Groceries",
            amount = -100m,
            categoryId
        });

        var report = await _client.GetFromJsonAsync<JsonElement>("/api/reports/monthly?year=2026&month=10");

        Assert.Equal(100m, report.GetProperty("expenses").GetDecimal());
        var cat = report.GetProperty("categories").EnumerateArray()
            .First(e => e.GetProperty("categoryName").GetString() == "Groceries");
        Assert.Equal(400m, cat.GetProperty("budgeted").GetDecimal());
        Assert.Equal(100m, cat.GetProperty("spent").GetDecimal());
        Assert.Equal(31, report.GetProperty("daily").GetArrayLength());
        Assert.NotEmpty(report.GetProperty("expenseBreakdown").EnumerateArray());
    }

    [Fact]
    public async Task Recurring_Run_GeneratesDueTransactions()
    {
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/categories");
        var categoryId = categories.EnumerateArray().First().GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync("/api/recurring", new
        {
            description = "Test subscription",
            amount = -9.99m,
            categoryId,
            frequency = "Monthly",
            nextRunDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd")
        });

        var run = await _client.PostAsync("/api/recurring/run", null);
        var body = JsonDocument.Parse(await run.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("generated").GetInt32() >= 1);
    }

    [Fact]
    public async Task IndexPage_Served()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ledgerly", html);
    }
}
