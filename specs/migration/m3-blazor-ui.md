# M3 Spec: Blazor UI Parity

## 1. Overview and Goal
* **Purpose:** Rebuild the React SPA (`apps/web`) as a Blazor Web App in Interactive Server mode (`StockViewer.Web`), with the same screen, features and UX rules from Phases 1–4. It talks only to `StockViewer.Api` through Aspire service discovery.
* **Success Criteria:**
  * **Positions:**
    * The user can add a position (e.g. `PETR4`, B3, 100, `28,45`), edit its notes and delete it.
    * Invalid input shows the same messages as the React app, both client-side (shared rules) and from API errors (`409 DUPLICATE_POSITION`, etc.).
  * **Transactions:**
    * Per position, a dialog lists the transactions in application order and lets the user add a BUY/SELL (date, quantity, unit price, fees) or delete one.
    * `409 INSUFFICIENT_QUANTITY` is shown inline.
  * **Dashboard:**
    * Summary cards: Total invested, Current value, Profit (amount + %), Realized profit, USD/BRL rate.
    * A BRL/USD toggle switches every total.
    * An allocation chart shows allocation by position (max 8 slices incl. "Other") and by market.
    * The positions table can be sorted by any column and filtered by market.
    * Portfolio warnings are shown.
  * **Prices:**
    * Each row shows price, current value, profit, profit %, realized profit and the quote's "Yahoo · 14:32".
    * Unpriced rows show "price unavailable" and the rest still render.
  * **Refresh:**
    * Automatic every 5 min while the tab is visible, plus a "Refresh" button (`POST /api/portfolio/refresh`).
    * "Last updated at HH:MM" and a note that prices may be delayed ~15 min.
    * Never more often than every 60 s.
  * **Preferences:** theme (system/light/dark) and currency survive a reload, with no theme flash on first paint.
  * **Layout/a11y:** works at 400 px width, light and dark themes, profit uses green/red **and** a `+`/`−` sign, the chart legend lists values and percentages, and dialogs trap focus and close on Esc.
  * **States:** skeletons while loading, an error state with "Try again", and the empty state "No positions yet — add your first stock".
  * `dotnet test` passes, including bUnit ports of every web test in `apps/web/src/**/*.test.ts(x)`.

## 2. Tech Stack and Architecture
Everything from M1–M2, plus:
* **Blazor Web App**, global Interactive Server render mode (`<Routes @rendermode="InteractiveServer" />`, `<HeadOutlet @rendermode="InteractiveServer" />`). It is a single page at `/`, the same as today. Adding more pages needs approval (Phase 3 rule).
* **Charts:** `Blazor-ApexCharts` (donut). This is the only UI dependency.
* **Tests:** `bunit` (+ xUnit v3).
* **Allowed new packages:** `Blazor-ApexCharts`, `bunit`. Nothing else, so no MudBlazor/Fluent/Bootstrap or icon packages. The few icons needed (plus, refresh, pencil, trash, list, sun/moon) are inline SVG in one `Icons.razor`.

### API access (`Web/Api/ApiClient.cs`)
* A typed `HttpClient` with `BaseAddress = "https+http://api"` (Aspire service discovery). It uses `StockViewerJson` options from `Shared`, so the DTOs are the same records the API serializes.
* The standard resilience handler stays on but with **retries disabled for POST/PUT/DELETE** (`DisableForUnsafeHttpMethods()`), so a create is never sent twice.
* Methods: `GetPortfolio`, `RefreshPortfolio`, `CreatePosition`, `UpdatePosition`, `DeletePosition`, `GetTransactions`, `CreateTransaction`, `DeleteTransaction`. Non-2xx responses throw `ApiException(ErrorCode Code, string Message, int Status)` read from `ApiErrorBody`. Network failures throw `ApiException(INTERNAL_ERROR, "Could not reach the server")`.
* Because the client runs on the server, the browser never sees the API, and external price providers are still called only by the API.

### Prerendering
* Pages prerender on the server and then become interactive. The portfolio fetched during prerender is carried over with persistent component state (`[PersistentState]` on the dashboard's state property), so the API is **not** called twice on first load.

### Preferences (`Web/Features/Preferences/`)
* Cookies `sv-theme` (`system|light|dark`) and `sv-currency` (`BRL|USD`), each 1 year, `SameSite=Lax`. Unknown values fall back to `system` / `BRL`.
* `App.razor` reads `sv-theme` from `HttpContext` during SSR and writes `data-theme="light|dark"` on `<html>`. For `system` it writes no attribute and CSS uses `@media (prefers-color-scheme: dark)`. Result: no flash.
* Changing a preference updates the component state and calls a small JS module (`preferences.js`) that sets the cookie and the `data-theme` attribute. Interactive circuits can't set HTTP cookies.

### Auto-refresh (`Web/Features/Dashboard/`)
* `PortfolioRefresher` (plain C# class, unit-testable with `FakeTimeProvider`) owns a `PeriodicTimer(5 min)`. It knows the last successful fetch time and enforces the 60 s minimum between automatic refreshes.
* `visibility.js` reports `document.visibilitychange` to .NET (`DotNetObjectReference`):
  * Ticks are skipped while the tab is hidden.
  * When the tab becomes visible and the data is older than 5 min, it fetches immediately.
* The timer is disposed with the component (`IAsyncDisposable`).
* A failed refresh keeps the last data on screen and shows the "refresh failed" banner. This is the port of `RefreshFailedBanner`.

### Components
| React | Blazor | Notes |
|---|---|---|
| `components/ui/*` (button, input, label, native-select, textarea, segmented, skeleton, table, dialog, alert-dialog) | `Web/Components/Ui/*.razor` + `.razor.css` | `Dialog`/`ConfirmDialog` wrap the native `<dialog>` element (`showModal()` via JS), so focus trap and Esc come free |
| `features/dashboard/*` | `Web/Features/Dashboard/` | `DashboardPage`, `SummaryCards`, `CurrencyToggle`, `AllocationChart`, `PortfolioWarnings`, `RefreshControl`, `AllocationData.cs` (port of `allocation-data.ts`), `ChartPalette.cs` |
| `features/positions/*` | `Web/Features/Positions/` | `PositionsTable` (sort + market filter, using `Shared` `MoneySort`), `PositionDialog`, `PositionForm`, `DeletePositionDialog`, `PositionFormModel.cs` (port of `positionForm.ts`) |
| `features/transactions/*` | `Web/Features/Transactions/` | `TransactionsDialog`, `TransactionsTable`, `TransactionForm`, `TransactionFormModel.cs` (port of `transactionForm.ts`) |
| `features/preferences/*` | `Web/Features/Preferences/` | `ThemeToggle`, `Preferences.cs` (cookie read/parse) |
| `lib/display.ts` | `Web/Display/Display.cs` | `Time` ("14:32", pt-BR), `SourceAndTime`, `SignClass`, `TradeDate` ("12/10/2026") |

* **Forms:**
  * `EditForm` bound to a string-based form model.
  * A `SharedRulesValidator` component runs the same `Shared/Validation` functions the API uses and puts each `FieldError` on its field.
  * Inputs accept `28,45` via `Format.NormalizeDecimalInput`.
  * On submit, API errors are mapped back to the field when the message starts with `"<field>: "`, otherwise they're shown as a form-level error.
  * The quantity and average price of an existing position are read-only, and only notes can be edited (Phase 4 rule).
* **Chart colours:** ApexCharts sets SVG `fill` attributes, which can't use CSS variables. So `ChartPalette.cs` is the single place that holds two validated hex palettes (light/dark), 7 slots + "Other", and the dashboard passes the resolved theme. No colour literals anywhere else.

### Styling
* `wwwroot/app.css` holds the design tokens on `:root` (`--background`, `--foreground`, `--card`, `--muted-foreground`, `--border`, `--gain`, `--loss`, `--chart-1…7`, radius, spacing), redefined for dark under `[data-theme="dark"]` and `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { … } }`. Port the values from `apps/web/src/index.css`.
* Component styles go in scoped `*.razor.css`. There's no CSS framework and no build step.
* Mobile first, single column below 640 px. The table scrolls horizontally inside its card, never the page.

## 3. Executable Commands
Same as M1–M2, plus:
* **Web tests only:** `dotnet test tests/StockViewer.Web.Tests`
* **Manual UI check:** `aspire run`, open the `web` endpoint from the dashboard. Check at 400 px width (DevTools device toolbar) and in light and dark themes.
* **Publish check:** `dotnet publish src/StockViewer.Web -c Release` must succeed with no warnings.

## 4. Project Structure
```
src/StockViewer.Web/
├── Program.cs                 # AddServiceDefaults, AddRazorComponents().AddInteractiveServerComponents(), ApiClient
├── Api/ApiClient.cs, ApiException.cs
├── Display/Display.cs
├── Components/
│   ├── App.razor              # <html data-theme=…>, reads the theme cookie
│   ├── Routes.razor
│   ├── Layout/MainLayout.razor
│   └── Ui/                    # Button, Input, Select, Textarea, Segmented, Skeleton, DataTable, Dialog, ConfirmDialog, Icons
├── Features/
│   ├── Dashboard/
│   ├── Positions/
│   ├── Transactions/
│   └── Preferences/
└── wwwroot/
    ├── app.css
    └── js/preferences.js, visibility.js, dialog.js
tests/StockViewer.Web.Tests/
├── Support/                   # FakeApiClient (or stub handler), bUnit TestContext setup with JSInterop in loose mode
└── Features/…                 # one test class per ported React test file
```
* Test ports required: `AllocationChart`, `allocation-data`, `DashboardPage`, `SummaryCards`, `usePortfolio` (→ `PortfolioRefresher`), `PositionForm`, `positionForm`, `PositionsTable`, `usePreferences` (→ `Preferences`), `TransactionForm`, `TransactionsDialog`, `transactionForm`, `lib/api` (→ `ApiClient`), `lib/display` (→ `Display`).

## 5. Code Style and Conventions
* Same as M1–M2.
* **No money math in markup.** Components receive already-computed values or call `Shared/Money` and `Display` helpers from `@code`/C# files. Razor only renders strings.
* Components take parameters and raise `EventCallback`s. Only `DashboardPage` talks to `ApiClient`, and other components get data and callbacks (same data flow as the React version).
* Use `@key` on list rows. Components that hold timers or JS references implement `IAsyncDisposable`.
* JS interop is limited to the three small modules above. No JS frameworks.

Example of the expected style:
```razor
@* SummaryCards.razor — values are already computed; only formatting here *@
<section class="cards">
    <Card Label="Profit">
        <span class="@Display.SignClass(Totals.Profit)">@Format.SignedMoney(Totals.Profit, Currency)</span>
        <small>@Format.Percent(Totals.ProfitPercent)</small>
    </Card>
</section>

@code {
    [Parameter, EditorRequired] public required Totals Totals { get; init; }
    [Parameter, EditorRequired] public Currency Currency { get; init; }
}
```

## 6. Boundaries and Guardrails
* **Always do:**
  * Port the React test cases before adding new ones.
  * Skeletons while loading, an error state with "Try again", and the empty state text.
  * "Last updated at HH:MM" near the refresh button, and the ~15 min delay note.
  * An accessible chart legend (label, value, percentage), not colour alone.
* **Ask first:**
  * Before adding routing or more pages.
  * Before adding any UI kit, chart library, icon package or CSS framework.
  * Before switching any component to WebAssembly/Auto render mode.
* **Never do:**
  * Never call external price providers or the API from the browser. All HTTP goes through `ApiClient` on the server.
  * Never refresh more often than every 60 s.
  * Never block the page because one quote is missing.
  * Never do money math in `.razor` markup.
  * Never modify the React app.
