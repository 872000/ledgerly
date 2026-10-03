using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ledgerly.Api.Data;
using Ledgerly.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LedgerlyContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<CsvImporter>();
builder.Services.AddScoped<RecurringService>();
builder.Services.AddScoped<SummaryService>();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LedgerlyContext>();
    db.Database.EnsureCreated();
    SeedData.Seed(db);

    // Generate any recurring transactions that came due while the app was down.
    var recurring = scope.ServiceProvider.GetRequiredService<RecurringService>();
    await recurring.GenerateDueAsync(db, DateTime.Today);
}

app.Run();

// Exposed for WebApplicationFactory integration tests.
public partial class Program
{
}
