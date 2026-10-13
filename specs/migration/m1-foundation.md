# M1 Spec: .NET Foundation & Shared Domain

## 1. Overview and Goal
* **Purpose:** Create the .NET 10 solution next to the Node app:
  * an Aspire AppHost that starts an (almost empty) API and Blazor app;
  * a `StockViewer.Shared` library with every pure money/validation function and every contract from `packages/shared`.
  * The EF Core data model is also proposed here (approval required before the migration).
* **Success Criteria:**
  * `aspire run` (or `dotnet run --project src/StockViewer.AppHost`) starts `api` and `web`. The Aspire dashboard shows both as healthy.
  * `GET /api/health` on the api returns `200 {"status":"ok"}`.
  * The web app shows an empty layout shell (header with the app name and a theme toggle placeholder) in light and dark themes.
  * `dotnet build -warnaserror` passes with zero warnings.
  * `dotnet test` passes. Every test case in `packages/shared/src/**/*.test.ts` has an equivalent in `tests/StockViewer.Shared.Tests`.
  * The EF Core schema below is approved by the user. Only then is the `Init` migration created and applied.
  * `npm test` still passes, because the Node app is untouched.

## 2. Tech Stack and Architecture
*(Pin exact versions in `Directory.Packages.props` at setup time, using the latest stable release of each item below.)*
* **SDK:** .NET 10 (LTS), pinned in `global.json` (`rollForward: latestFeature`).
* **Language settings (`Directory.Build.props`):** `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `InvariantGlobalization=false` (pt-BR / en-US formatting is required).
* **Orchestration:** Aspire (latest stable), with the AppHost using `Aspire.AppHost.Sdk` and a ServiceDefaults project from the Aspire template.
* **Database:** SQLite through EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`). The DB file is `src/StockViewer.Api/data/stockviewer.db` (git-ignored).
* **Tests:** xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`), `Microsoft.Extensions.TimeProvider.Testing`. Use the built-in `Assert` only.
* **Allowed NuGet packages in M1:** the ones listed above plus what the Aspire templates add (OpenTelemetry, `Microsoft.Extensions.ServiceDiscovery`, `Microsoft.Extensions.Http.Resilience`). Anything else needs approval.

### Aspire app model (`src/StockViewer.AppHost/AppHost.cs`)
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.StockViewer_Api>("api")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.StockViewer_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```
* No containers, because the DB is a file owned by the api.
* `BRAPI_TOKEN` (used from M2) is read by the api from configuration/user-secrets. The AppHost does not hold secrets.

### `StockViewer.Shared` (port of `packages/shared`)
Static classes with pure static functions are the C# form of the "small pure functions" rule. No instance state, no I/O, no ASP.NET references.

| TS file | C# | Contents |
|---|---|---|
| `schemas/market.ts` | `Market.cs` | `enum Market { B3, US }`, `enum Currency { BRL, USD }`, `Markets.CurrencyFor(Market)` |
| — (new) | `Money/Money.cs` | `readonly record struct Money(decimal Amount, Currency Currency)`. `+`/`-` throw `CurrencyMismatchException` when currencies differ |
| `money/invested.ts`, `money/profit.ts` | `Money/Profit.cs` | `Invested`, `Profit` (returns `invested, currentValue, amount, percent`), `PortfolioTotals`, `RealizedProfitTotal` |
| `money/convert.ts` | `Money/FxConversion.cs` | `FxConversion.Convert(amount, from, to, usdBrlRate)`. Throws on a rate ≤ 0. This is the only place currencies change. Named to avoid clashing with `System.Convert` |
| `money/average-price.ts` | `Money/Ledger.cs` | `LedgerTransaction`, `DerivedPosition`, `Compare`, `Sort`, `Apply`, `Derive`, `IsClosed`, `InsufficientQuantityException` (same message text) |
| `money/allocation.ts` | `Money/Allocation.cs` | `AllocationItem`, `AllocationSlice`, `Slices`, `GroupOther`, `OtherKey = "other"` |
| `money/sort.ts` | `Money/MoneySort.cs` | `SortKey`, `CompareDecimals` |
| `money/format.ts` | `Money/Format.cs` | `Money`, `SignedMoney`, `Percent`, `Share`, `Quantity`, `NormalizeDecimalInput`. Uses the typographic minus `−` |
| — (new) | `Money/DecimalText.cs` | `ToJson(decimal)`: normalized invariant string (no trailing zeros, no exponent). `TryParse(string, out decimal)`: invariant, pattern `^\d+(\.\d+)?$` |
| `schemas/decimal.ts`, `position.ts`, `transaction.ts` | `Validation/*.cs` | Rules and messages, see below |
| `schemas/*` (response shapes) | `Contracts/*.cs` | `Position`, `Transaction`, `Quote`, `QuoteSource`, `FxRate`, `Totals`, `PortfolioTotals`, `PortfolioPosition`, `Portfolio`, `ApiErrorBody`, `ErrorCode`, `Health` |
| — | `Json/StockViewerJson.cs` | One `JsonSerializerOptions` factory used by both Api and Web: camelCase, `JsonStringEnumConverter`, `DecimalStringJsonConverter` |

**Decimal semantics.**
* C# `decimal` replaces `decimal.js`, and division keeps full `decimal` precision (28–29 significant digits).
* `decimal.js` used 20, so repeating fractions (e.g. an average of `100/3`) differ in the far digits. That is accepted. Tests for those cases compare values rounded to 10 decimal places, and tests for terminating values compare exact strings.
* Display rounding uses `MidpointRounding.AwayFromZero` (= `ROUND_HALF_UP`).
* `DecimalStringJsonConverter` **writes** `DecimalText.ToJson(value)` (e.g. `28.450m` → `"28.45"`, `0m` → `"0"`). It **reads** only JSON strings, never JSON numbers, which matches the "never `number` for money" rule.

**Request vs. response contracts.**
* Request bodies are records with **string** fields (`CreatePositionRequest(string? Ticker, string? Market, string? Quantity, …)`) so that malformed input gets the same messages as today.
* The `Validation` functions turn a request into either a typed command with `decimal`/`Market`/`DateOnly` values or a list of `FieldError(string Field, string Message)`:
```csharp
public static Result<CreatePositionCommand> ValidateCreatePosition(CreatePositionRequest request)
```
* `Result<T>` is a tiny record in `Shared` with `Value` or `Errors`. No library.

**Validation rules and messages (copy the exact wording from the TS schemas):**
* Ticker: trim + upper-case. `"Ticker is required"`. Rejects `.SA` → `"Enter the ticker without the .SA suffix"`. Pattern `^[A-Z0-9][A-Z0-9.-]{0,11}$` → `"Ticker may only contain letters, digits, '.' and '-' (max 12 characters)"`.
* Positive decimal (`Quantity`, `Average price`, `Unit price`):
  * required
  * `"<Label> must be a positive number"`
  * `"> 0"`
  * at most 8 decimals
  * at most 15 significant digits. This limit came from Prisma's float storage. It is **kept for parity** even though EF stores TEXT, and relaxing it needs a question first.
* Fees: zero or positive, the same limits, default `"0"`.
* B3 quantity must be whole → `"B3 quantity must be a whole number"`. US allows fractions.
* Notes: trimmed, max 500 → `"Notes can have at most 500 characters"`. Empty becomes `null`.
* Transaction date:
  * `"Date is required"`
  * `"Date must be in YYYY-MM-DD format"`
  * `"Date is not a valid date"`
  * `"Date can't be in the future"`. "Today" comes from an injected `TimeProvider` using local time, the same as `todayIsoDate()`.
* Transaction type: `BUY` | `SELL`.

### Data model (proposal — requires approval before creating the migration)
Same tables, columns and constraints as `apps/api/prisma/schema.prisma`. EF entities live in the Api project (`src/StockViewer.Api/Data/`), not in `Shared`.

```csharp
public sealed class PositionRow
{
    public required string Id { get; set; }          // existing cuid ids are kept; new: Guid.CreateVersion7().ToString("N")
    public required string Ticker { get; set; }      // uppercase, no suffix
    public Market Market { get; set; }               // stored as TEXT ("B3"/"US")
    public decimal Quantity { get; set; }            // derived mirror, TEXT
    public decimal AveragePrice { get; set; }        // derived mirror, TEXT
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }          // UTC
    public DateTime UpdatedAt { get; set; }          // UTC, set in SaveChanges
    public List<TransactionRow> Transactions { get; set; } = [];
}

public sealed class TransactionRow
{
    public required string Id { get; set; }
    public required string PositionId { get; set; }
    public TransactionType Type { get; set; }        // TEXT ("BUY"/"SELL")
    public DateOnly Date { get; set; }               // TEXT "yyyy-MM-dd" (was DateTime at UTC midnight)
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Fees { get; set; }                // default 0
    public DateTime CreatedAt { get; set; }          // UTC
}
```
* Unique index `(Ticker, Market)`. Index `(PositionId, Date)`. FK `Transaction → Position` with `ON DELETE CASCADE`.
* Enums use `HasConversion<string>()`. A value converter sets `DateTimeKind.Utc` on read.
* Decimals use the provider's default TEXT mapping, so values are exact and keep full precision.
* Table names stay `Position` and `Transaction`, which keeps the M4 import a straight copy.
* In Development the API applies pending migrations at startup, so the first `aspire run` creates the SQLite file (`Api/Data/DevelopmentMigrations.cs`). In any other environment they are applied explicitly with `dotnet ef database update`. Creating a migration still needs approval.

## 3. Executable Commands
* **Prerequisites:** .NET 10 SDK, Aspire CLI (`dotnet tool install -g aspire.cli` or the official install script), `dotnet tool install -g dotnet-ef`.
* **Restore/build:** `dotnet build -warnaserror`
* **Run everything:** `aspire run` (fallback: `dotnet run --project src/StockViewer.AppHost`)
* **Tests:** `dotnet test`
* **Single project tests:** `dotnet test tests/StockViewer.Shared.Tests`
* **Format check:** `dotnet format --verify-no-changes`
* **Migration (only after approval):** `dotnet ef migrations add Init --project src/StockViewer.Api` then `dotnet ef database update --project src/StockViewer.Api`

## 4. Project Structure
```
global.json
StockViewer.slnx
Directory.Build.props
Directory.Packages.props
.editorconfig
src/
├── StockViewer.AppHost/AppHost.cs
├── StockViewer.ServiceDefaults/Extensions.cs
├── StockViewer.Shared/
│   ├── Market.cs
│   ├── Result.cs
│   ├── Contracts/          # response + request records, ErrorCode
│   ├── Json/               # DecimalStringJsonConverter, StockViewerJson
│   ├── Money/              # Money, Profit, FxConversion, Ledger, Allocation, MoneySort, Format, DecimalText
│   └── Validation/         # DecimalRules, PositionRules, TransactionRules
├── StockViewer.Api/
│   ├── Program.cs          # AddServiceDefaults, DbContext, MapDefaultEndpoints, /api/health
│   ├── Data/               # StockViewerDb, PositionRow, TransactionRow, Migrations/
│   └── Endpoints/HealthEndpoints.cs
└── StockViewer.Web/
    ├── Program.cs          # AddServiceDefaults, Razor components, Interactive Server
    ├── Components/App.razor, Routes.razor, Layout/MainLayout.razor
    └── wwwroot/app.css     # design tokens (light + dark)
tests/
├── StockViewer.Shared.Tests/   # one test class per Shared file
└── StockViewer.Api.Tests/      # HealthEndpointTests only, in M1
```
* Tests mirror the source path: `Money/Ledger.cs` → `tests/StockViewer.Shared.Tests/Money/LedgerTests.cs`.
* `.gitignore` gains `bin/`, `obj/`, `*.user`, `.vs/`, `src/StockViewer.Api/data/`, `TestResults/`.

## 5. Code Style and Conventions
* File-scoped namespaces. `record`/`record struct` for data, `static class` for functions. No inheritance hierarchies, no repository/unit-of-work layers, no MediatR, no AutoMapper.
* Never use `double`/`float` for money or quantity. Analyzer rule: treat any `double` in `Shared/Money` as a review blocker.
* Never add two `Money` values with different currencies. Convert through `FxConversion.Convert` first.
* Table-driven tests with `[Theory]` + `[InlineData]`/`[MemberData]`, mirroring the TS test tables.
* Time is always injected through `TimeProvider` and never read from `DateTime.Now`.

Example of the expected style:
```csharp
namespace StockViewer.Shared.Money;

public static class Profit
{
    public static decimal Invested(decimal quantity, decimal averagePrice) => quantity * averagePrice;

    public static ProfitResult Of(decimal quantity, decimal averagePrice, decimal currentPrice)
    {
        var invested = Invested(quantity, averagePrice);
        var currentValue = quantity * currentPrice;
        var amount = currentValue - invested;
        var percent = invested == 0 ? 0 : amount / invested * 100;
        return new ProfitResult(invested, currentValue, amount, percent);
    }
}
```

## 6. Boundaries and Guardrails
* **Always do:**
  * Port every test case from `packages/shared/src/**/*.test.ts`, including zero, losses, fractional US quantities, full sell then rebuy, fees, and insufficient quantity.
  * Test `DecimalStringJsonConverter` round-trips (`"28.45"`, `"0"`, `"0.00000001"`, trailing zeros, rejects JSON numbers).
  * Test `Money` arithmetic throws on mismatched currencies.
* **Ask first:**
  * Before running `dotnet ef migrations add` (present the schema above and wait for approval).
  * Before adding any NuGet package not listed in section 2.
  * Before changing any validation message or rule (including the 15-significant-digit limit).
* **Never do:**
  * Never modify anything under `apps/` or `packages/` in this phase.
  * Never apply migrations automatically at app startup outside the Development environment.
  * Never reference ASP.NET Core or EF Core from `StockViewer.Shared`.
  * Never commit `*.db` files or secrets.
