# M2 Spec: Minimal API Parity

## 1. Overview and Goal
* **Purpose:** Port the whole Node API (`apps/api`) to `StockViewer.Api` using minimal APIs: positions, transactions, price providers with fallback and cache, USD/BRL rate, portfolio. The REST contract stays **identical** (see the contract table in [README.md](README.md#contract-parity-applies-to-every-phase)), so the M3 UI, or the current React app, could run against either backend.
* **Success Criteria:**
  * Every route in the contract table returns the same status codes, error codes and JSON shape as the Node API.
  * Every test case in `apps/api/src/**/*.test.ts` has an equivalent in `tests/StockViewer.Api.Tests`, and `dotnet test` passes.
  * If Yahoo fails for a B3 ticker, brapi is used. If no provider prices a ticker, the position has `quote: null`, it is left out of totals, and a warning is returned. The API never crashes because of a provider.
  * Repeated `GET /api/portfolio` calls within the TTL (quotes 5 min, FX 15 min) don't hit the providers. This is verified by tests with `FakeTimeProvider` and a stub handler that counts requests.
  * `POST /api/portfolio/refresh` clears both caches. The FX stale value is kept as a fallback.
  * With the app running under Aspire, `curl -s <api-url>/api/portfolio | jq` returns the same payload shape as `curl -s localhost:3001/api/portfolio | jq` against the Node API on the same data.
  * Provider failures appear as structured warnings (provider name + tickers) in the Aspire dashboard logs.

## 2. Tech Stack and Architecture
Everything from M1, plus:
* **HTTP:** typed `HttpClient`s registered with `AddHttpClient`. Provider clients **remove** the standard resilience handler from ServiceDefaults (`RemoveAllResilienceHandlers()`) and set `Timeout = 5 s`, **with no retries**. Parity: the Yahoo → brapi fallback chain is the retry, and we must not hammer free sources.
* **Allowed new packages:** `Microsoft.AspNetCore.Mvc.Testing` (tests) and `Microsoft.AspNetCore.OpenApi` (OpenAPI document mapped **only in Development**). Nothing else.

### Program layout
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDbContext<StockViewerDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("StockViewer")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(o => StockViewerJson.Configure(o.SerializerOptions));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddPrices(builder.Configuration);   // providers, QuoteService, FxService

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapPositionEndpoints();
api.MapTransactionEndpoints();
api.MapPortfolioEndpoints();
app.MapFallback("/api/{**path}", ApiErrors.RouteNotFound);
app.Run();

public partial class Program;   // for WebApplicationFactory
```
* Handlers are thin: validate the request with `Shared` rules, call a static domain function, and return `TypedResults`. There is no business logic in `Endpoints/`.

### Errors (`Api/Errors/`)
* `AppException(int StatusCode, ErrorCode Code, string Message)` is the port of `appError`.
* `ApiExceptionHandler : IExceptionHandler` maps:
  * `AppException` → its status/code
  * `BadHttpRequestException` (malformed JSON, wrong content type) → `400 VALIDATION_ERROR`
  * anything else → log the error and return `500 INTERNAL_ERROR "Something went wrong"`.
* Validation failures return `400 VALIDATION_ERROR` with the **first** error, formatted `"<field>: <message>"`, which is the same format as the Fastify handler.
* Unknown `/api/*` routes return `404 NOT_FOUND "Route <METHOD> <path> not found"`.
* The body is always `ApiErrorBody`, and `ProblemDetails` is never returned.

### Domain + data (ports, one-to-one)
| Node | .NET | Notes |
|---|---|---|
| `transactions/ledger.ts` | `Api/Transactions/LedgerRows.cs` | `ToTransactionDto`, `DeriveFromRow` (maps `InsufficientQuantityException` → `409 INSUFFICIENT_QUANTITY`), `SyncPosition(db, positionId)` |
| `positions/positions.ts` | `Api/Positions/Positions.cs` | `List`, `ListLedger`, `Create` (creates the first BUY dated *today*, fees 0), `Update` (notes only; quantity/average price only if equal to the derived values, else `409 DERIVED_VALUE_READ_ONLY`), `Delete`. Sorting: B3 before US, then ticker (ordinal) |
| `transactions/transactions.ts` | `Api/Transactions/Transactions.cs` | `List` (application order), `Create` (B3 whole-quantity check after loading the position → `400 "quantity: B3 quantity must be a whole number"`), `Delete` (`409 "Can't delete this transaction. …"` if a later sell depends on it) |
| `portfolio/build-portfolio.ts` | `Api/Portfolio/BuildPortfolio.cs` | Pure `Build(positions, quotes, rateResult)` + `GetAsync(db, quotes, fx)`. Closed positions are not quoted. Warning strings are copied verbatim |

* Create/delete transaction and `SyncPosition` run inside **one** DB transaction (`BeginTransactionAsync`). An invalid history rolls back.
* `DbUpdateException` whose inner `SqliteException.SqliteErrorCode == 19` (UNIQUE constraint) on create maps to `409 DUPLICATE_POSITION "A position for {ticker} ({market}) already exists"`.
* Position DTO quantity/average price come from **deriving the transactions**, never from the mirrored columns (parity with `toDto`).
* `UpdatedAt` is set in `SaveChangesAsync` from the injected `TimeProvider`.

### Prices (`Api/Prices/`)
```csharp
public interface IPriceProvider
{
    QuoteSource Name { get; }
    bool Supports(Market market);
    // Returns quotes for the tickers it could price; unknown tickers are left out.
    // Throws ProviderException when the whole call fails.
    Task<IReadOnlyList<Quote>> GetQuotesAsync(IReadOnlyList<QuoteRequest> items, CancellationToken ct);
}
```
* **`TtlCache<T>`** (port of `cache.ts`): `Get`, `GetStale`, `Set`, `ExpireAll`, with time taken from `TimeProvider`.
* **`QuoteService`** (singleton, port of `get-quotes.ts`):
  * de-duplicates requests and serves cached quotes first;
  * then asks providers **in order** (`[yahoo, brapi]`), each only for the markets it supports and only for the tickers still missing;
  * logs `"Price provider failed"` / `"Price provider returned no quote"` with provider + tickers;
  * never throws because of a provider.
  * `Clear()` expires all entries.
* **`YahooProvider`:**
  * Uses Yahoo's public chart endpoint `GET https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=1d&interval=1d` and reads `chart.result[0].meta.regularMarketPrice` and `meta.currency`. This endpoint needs no cookie/crumb.
  * Symbols: `PETR4` + B3 → `PETR4.SA`.
  * It accepts one symbol per call, so it runs **at most 4 requests in parallel** per refresh.
  * **Deviation from Phase 2 ("one call per provider per refresh"):** this is accepted because the cache bounds the traffic. If a batch endpoint that works without a crumb is confirmed during implementation, prefer it and update this spec.
  * Sends a browser-like `User-Agent`.
  * Skips quotes whose currency doesn't match the market currency.
* **`BrapiProvider`:**
  * B3 only. One batch call `GET https://brapi.dev/api/quote/{t1,t2,…}`.
  * Sends `Authorization: Bearer {token}` only if `Brapi:Token` (env `BRAPI_TOKEN`, user-secrets in dev) is set.
  * Skips non-BRL results.
* **Parsing:** read provider JSON with `System.Text.Json`, and read prices with `JsonElement.GetDecimal()` so they **never go through `double`**. A missing, non-positive or unexpected shape counts as "no quote" for that symbol, or `ProviderException` for the whole response.

### FX (`Api/Fx/UsdBrl.cs`)
* `FxService` (singleton) calls `GET https://economia.awesomeapi.com.br/json/last/USD-BRL` and reads `USDBRL.bid`, which must match `^\d+(\.\d+)?$` and be > 0.
* TTL is 15 min. On failure it returns the last known rate with `Stale = true` and logs a warning. `Fx` is `null` only if no rate was ever fetched. `Clear()` expires the entry and keeps the stale value.
* `GET /api/fx/usd-brl` returns `503 FX_UNAVAILABLE "USD/BRL rate is unavailable"` when `Fx` is null.

## 3. Executable Commands
Same as M1, plus:
* **API tests only:** `dotnet test tests/StockViewer.Api.Tests`
* **Provider tests only:** `dotnet test tests/StockViewer.Api.Tests --filter "FullyQualifiedName~Prices"`
* **Manual smoke test:** `aspire run`, copy the api URL from the dashboard, then `curl -s <api-url>/api/portfolio | jq`
* **Parity check:** run the Node API (`npm run dev -w apps/api`) and the .NET API against copies of the same data, then diff `jq -S` output (ignoring `fetchedAt` and far decimal digits).

## 4. Project Structure
```
src/StockViewer.Api/
├── Program.cs
├── Data/                    # from M1
├── Errors/                  # AppException, ApiExceptionHandler, ApiErrors
├── Endpoints/               # Health, Position, Transaction, Portfolio (thin)
├── Positions/Positions.cs
├── Transactions/            # LedgerRows.cs, Transactions.cs
├── Prices/                  # IPriceProvider, QuoteRequest, ProviderException, TtlCache, QuoteService, YahooProvider, BrapiProvider, PricesRegistration
├── Fx/UsdBrl.cs
└── Portfolio/BuildPortfolio.cs
tests/StockViewer.Api.Tests/
├── Support/                 # ApiFactory (WebApplicationFactory<Program>), StubHttpMessageHandler, TestData
├── Endpoints/               # ports of routes/*.test.ts
├── Positions/, Transactions/, Portfolio/, Prices/, Fx/, Errors/
```
* `ApiFactory` points to a fresh temp SQLite file per test class. It runs `Database.Migrate()` (allowed in tests only), swaps `TimeProvider` for `FakeTimeProvider`, and replaces every provider's primary handler with `StubHttpMessageHandler`.

## 5. Code Style and Conventions
* Same as M1.
* `QuoteService`, `FxService` and the providers are classes because they hold state (cache) or dependencies (`HttpClient`). Everything else is static functions that take their dependencies as parameters (`Positions.Create(StockViewerDb db, TimeProvider time, CreatePositionCommand command)`).
* `CancellationToken` flows from the request through every async call.
* Log with structured templates: `logger.LogWarning("Price provider failed {Provider} {Tickers}", …)`.

Example of the expected endpoint style:
```csharp
group.MapPost("/positions", async (CreatePositionRequest request, StockViewerDb db, TimeProvider time, CancellationToken ct) =>
{
    var command = PositionRules.ValidateCreate(request).OrThrowValidation();
    var position = await Positions.Create(db, time, command, ct);
    return TypedResults.Created($"/api/positions/{position.Id}", position);
});
```

## 6. Boundaries and Guardrails
* **Always do:**
  * Port the Node test tables first, then add .NET-specific tests (malformed JSON, JSON numbers sent for money → 400, SQLite unique violation).
  * Show the provider source and `fetchedAt` in every quote (`"yahoo"`/`"brapi"`, ISO 8601 UTC).
  * Log provider failures with ticker and provider name.
* **Ask first:**
  * Before any change to the contract table, error codes or warning texts.
  * Before adding a provider that needs a paid plan or an API key, or persisting quotes to the DB.
  * Before using Yahoo's crumb/cookie flow or any unofficial NuGet wrapper for Yahoo.
  * Before any EF migration beyond `Init`.
* **Never do:**
  * Never call real providers in tests.
  * Never sum BRL and USD without `FxConversion.Convert`.
  * Never parse provider prices through `double`.
  * Never put business logic in endpoint lambdas.
  * Never modify the Node app.
