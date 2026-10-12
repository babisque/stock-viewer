# Phase 3 Spec: Portfolio Dashboard

## 1. Overview and Goal
* **Purpose:** Turn the positions table into a real dashboard: summary cards, allocation chart, currency toggle, sorting/filtering and automatic refresh.
* **Success Criteria:**
  * Top of the page shows summary cards: Total invested, Current value, Profit (amount + %), USD/BRL rate.
  * A toggle switches all totals between BRL and USD; the choice is remembered on reload.
  * An allocation chart shows % of portfolio by position and by market (B3 vs US).
  * The table can be sorted by any column and filtered by market.
  * Prices refresh automatically every 5 minutes while the tab is visible, and on a "Refresh" button click.
  * The layout works on a 400px-wide phone screen.
  * Light and dark themes both work.

## 2. Tech Stack and Architecture
Everything from Phases 1–2, plus:
* **Charts:** Recharts.
* **Table:** TanStack Table 8 (sorting, filtering).
* **Theme:** Tailwind `dark:` variant + shadcn/ui theme tokens; follows the system preference with a manual override.
* **Preferences:** `localStorage` for currency toggle and theme (wrapped in try/catch).
* No backend changes expected — consumes `GET /portfolio` and `POST /portfolio/refresh` from Phase 2.

## 3. Executable Commands
Same as Phase 1.
* **Production build check:** `npm run build -w apps/web` (must succeed with no warnings about chunk size > 500 kB).

## 4. Project Structure
```
apps/web/src/
├── features/
│   ├── dashboard/
│   │   ├── summary-cards.tsx
│   │   ├── allocation-chart.tsx
│   │   └── use-portfolio.ts        # TanStack Query hook with refetchInterval
│   ├── positions/                   # table now uses TanStack Table
│   └── preferences/
│       └── use-preferences.ts      # currency + theme, persisted
└── components/ui/
```

## 5. Code Style and Conventions
* Same as previous phases.
* Components receive already-computed values; no money math in JSX — use helpers from `packages/shared`.
* Chart colors come from a single palette file; never hard-code colors in components.
* Auto-refresh uses TanStack Query `refetchInterval` + `refetchIntervalInBackground: false`.

## 6. Boundaries and Guardrails
* **Always do:**
  * Skeletons while loading; an error state with a "Try again" button.
  * Show "last updated at HH:MM" near the refresh button.
  * Accessible chart: legend with values and percentages, not color only.
* **Ask first:**
  * Before adding routing (multiple pages).
  * Before adding any new chart library or UI kit.
* **Never do:**
  * Never refresh more often than every 60 seconds.
  * Never block the UI if one quote is missing — show the rest.
