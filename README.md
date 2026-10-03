# Ledgerly

![Ledgerly banner](docs/banner.png)

A personal finance dashboard: track transactions, import bank CSV exports, set monthly budgets,
schedule recurring bills, and see it all on a zero-dependency dashboard served by the API itself.

## Features

- **Transactions** — full CRUD with income/expense sign convention and category tagging
- **CSV import** — upload a bank export (`date,description,amount,category`); handles quoted commas,
  currency symbols, and parenthesized negatives; unknown categories are created automatically
- **Budgets** — per-category monthly budgets with spend-vs-budget tracking (spent, remaining, % used)
- **Recurring transactions** — daily/weekly/monthly/yearly schedules; due occurrences are generated
  automatically on startup and via `POST /api/recurring/run`, with catch-up for missed runs
- **Monthly reports** — one chart-ready JSON endpoint: totals, per-category spend vs. budget,
  a daily income/expense series, and an expense breakdown
- **Dashboard UI** — a minimal static HTML/JS frontend served by the API (no build step, no CDN):
  summary cards, a hand-drawn canvas cash-flow chart, budget progress bars, add/import forms

## Tech stack

- ASP.NET Core 8 Web API, EF Core + SQLite (database file created and seeded at startup)
- Vanilla HTML/CSS/JS frontend in `wwwroot/` — zero dependencies, zero network calls
- xUnit + `WebApplicationFactory` integration tests (EF Core InMemory)

## How to run

```bash
dotnet run --project src/Ledgerly.Api
```

Open `http://localhost:5000` in a browser for the dashboard. API highlights:

```bash
# monthly summary (chart-ready JSON)
curl "http://localhost:5000/api/reports/monthly?year=2026&month=10"

# set a budget
curl -X POST http://localhost:5000/api/budgets \
  -H "Content-Type: application/json" \
  -d '{"categoryId":"<guid>","year":2026,"month":10,"amount":400}'

# generate due recurring transactions
curl -X POST http://localhost:5000/api/recurring/run
```

## How to test

```bash
dotnet test
```

## Project layout

```
src/Ledgerly.Api/
  Models/        Category, Transaction, Budget, RecurringTransaction
  Data/          EF Core DbContext + seed categories
  Services/      CsvImporter, RecurringService, SummaryService
  Controllers/   Transactions, Categories, Budgets, Recurring, Reports
  wwwroot/       index.html, style.css, app.js (no-build dashboard)
tests/Ledgerly.Tests/
  CsvImporterTests, RecurringServiceTests, SummaryServiceTests, LedgerlyApiTests
```
