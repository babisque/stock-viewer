import type { Portfolio, PortfolioPosition } from "@stock-viewer/shared";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CURRENCY_KEY } from "@/features/preferences/usePreferences";
import {
  makePortfolio,
  makePortfolioPosition,
  makePosition,
  makeTransaction,
  makeUsPortfolioPosition,
  normalizeSpaces,
} from "@/test/fixtures";
import { DashboardPage } from "./DashboardPage";

// In-memory fake of the API: GET /portfolio, POST /portfolio/refresh and the
// position mutations. New positions come back without a quote, like an
// unknown ticker would. `api.down = true` makes every request fail.
function fakeApi(initial: Portfolio) {
  let portfolio = initial;
  const api = { down: false, fetch: vi.fn() };
  api.fetch.mockImplementation(async (url: string, init?: RequestInit) => {
    const method = init?.method ?? "GET";
    const body = init?.body ? JSON.parse(String(init.body)) : undefined;
    const id = url.split("/")[3];

    if (api.down) throw new TypeError("offline");
    if (method === "GET" && url === "/api/portfolio") {
      return Response.json(portfolio);
    }
    if (method === "POST" && url === "/api/portfolio/refresh") {
      return Response.json(portfolio);
    }
    if (method === "POST" && url === "/api/positions") {
      const created: PortfolioPosition = {
        ...makePosition({
          ...body,
          id: `pos-${portfolio.positions.length + 1}`,
          currency: body.market === "B3" ? "BRL" : "USD",
        }),
        quote: null,
        currentValue: null,
        profit: null,
        profitPercent: null,
        realizedProfit: "0",
      };
      portfolio = {
        ...portfolio,
        positions: [...portfolio.positions, created],
        warnings: [`${created.ticker} (${created.market}): price unavailable`],
      };
      return Response.json(created, { status: 201 });
    }
    if (method === "GET" && url.endsWith("/transactions")) {
      return Response.json([makeTransaction({ positionId: id })]);
    }
    if (method === "DELETE") {
      portfolio = {
        ...portfolio,
        positions: portfolio.positions.filter((p) => p.id !== id),
      };
      return new Response(null, { status: 204 });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  });
  vi.stubGlobal("fetch", api.fetch);
  return api;
}

function calls(api: ReturnType<typeof fakeApi>, method: string, url: string) {
  return api.fetch.mock.calls.filter(
    ([callUrl, init]) => callUrl === url && (init?.method ?? "GET") === method,
  ).length;
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <DashboardPage />
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

// A cell in the positions table (tickers also appear in the chart legend).
async function findInTable(text: string) {
  const table = await screen.findByRole("region", { name: "Positions" });
  return within(table).findByText(text);
}

// The value shown on a summary card, e.g. "R$ 2.845,00".
function cardText(title: string) {
  return normalizeSpaces(
    screen.getByText(title, { selector: "dt" }).nextElementSibling?.textContent,
  );
}

describe("DashboardPage", () => {
  it("shows a skeleton while loading, then the empty state", async () => {
    fakeApi(makePortfolio({ positions: [] }));
    renderPage();
    expect(
      screen.getByRole("table", { name: "Loading portfolio" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("No positions yet — add your first stock"),
    ).toBeInTheDocument();
  });

  it("shows the summary cards, the allocation charts and the positions", async () => {
    fakeApi(
      makePortfolio({
        positions: [makePortfolioPosition(), makeUsPortfolioPosition()],
      }),
    );
    renderPage();
    const table = await screen.findByRole("region", { name: "Positions" });
    const row = within(table).getByText("PETR4").closest("tr")!;
    expect(normalizeSpaces(row.textContent)).toContain("+R$ 155,00");

    expect(cardText("Total invested")).toBe("R$ 2.845,00");
    expect(
      screen.getByRole("region", { name: "Portfolio summary" }),
    ).toHaveTextContent("Prices may be delayed ~15 min");
    expect(
      await screen.findByRole("region", { name: "By market" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("region", { name: "By position" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/^Last updated at \d{2}:\d{2}$/),
    ).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("switches the totals to USD and remembers the choice", async () => {
    fakeApi(makePortfolio());
    const { unmount } = renderPage();
    await findInTable("PETR4");

    await userEvent.setup().click(screen.getByRole("button", { name: "USD" }));
    expect(cardText("Total invested")).toBe("$569.00");
    expect(localStorage.getItem(CURRENCY_KEY)).toBe("USD");

    unmount();
    renderPage();
    await findInTable("PETR4");
    expect(cardText("Total invested")).toBe("$569.00");
  });

  it("disables the currency toggle and hides the market chart without a rate", async () => {
    fakeApi(makePortfolio({ totals: null, fx: null }));
    renderPage();
    await screen.findByRole("region", { name: "By position" });
    expect(screen.getByRole("button", { name: "USD" })).toBeDisabled();
    expect(
      screen.queryByRole("region", { name: "By market" }),
    ).not.toBeInTheDocument();
    expect(cardText("Total invested")).toBe("—");
  });

  it("refreshes on click, then waits 60 seconds before allowing it again", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const api = fakeApi(makePortfolio());
    renderPage();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await findInTable("PETR4");

    // Right after loading, the cooldown is already running.
    expect(
      screen.getByRole("button", { name: /^Refresh in \d+s$/ }),
    ).toBeDisabled();
    await act(() => vi.advanceTimersByTimeAsync(60_000));
    await user.click(screen.getByRole("button", { name: "Refresh" }));

    await waitFor(() =>
      expect(calls(api, "POST", "/api/portfolio/refresh")).toBe(1),
    );
    expect(
      await screen.findByRole("button", { name: "Refresh in 60s" }),
    ).toBeDisabled();

    await act(() => vi.advanceTimersByTimeAsync(30_000));
    expect(
      screen.getByRole("button", { name: "Refresh in 30s" }),
    ).toBeDisabled();
    await act(() => vi.advanceTimersByTimeAsync(30_000));
    expect(screen.getByRole("button", { name: "Refresh" })).toBeEnabled();
  });

  it("refetches automatically every 5 minutes", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const api = fakeApi(makePortfolio());
    renderPage();
    await findInTable("PETR4");
    expect(calls(api, "GET", "/api/portfolio")).toBe(1);

    await act(() => vi.advanceTimersByTimeAsync(4 * 60_000));
    expect(calls(api, "GET", "/api/portfolio")).toBe(1);
    await act(() => vi.advanceTimersByTimeAsync(60_000));
    await waitFor(() => expect(calls(api, "GET", "/api/portfolio")).toBe(2));
  });

  it("keeps the data on screen when a refresh fails", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const api = fakeApi(makePortfolio());
    renderPage();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await findInTable("PETR4");
    await act(() => vi.advanceTimersByTimeAsync(60_000));

    api.down = true;
    await user.click(screen.getByRole("button", { name: "Refresh" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      /Couldn.t refresh — showing data from \d{2}:\d{2}/,
    );
    expect(await findInTable("PETR4")).toBeInTheDocument();

    api.down = false;
    await user.click(screen.getByRole("button", { name: "Try again" }));
    await waitFor(() =>
      expect(screen.queryByRole("alert")).not.toBeInTheDocument(),
    );
  });

  it("shows the warnings from the API", async () => {
    fakeApi(
      makePortfolio({
        positions: [
          makePortfolioPosition({
            quote: null,
            currentValue: null,
            profit: null,
            profitPercent: null,
          }),
        ],
        warnings: ["PETR4 (B3): price unavailable — excluded from totals"],
      }),
    );
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "PETR4 (B3): price unavailable — excluded from totals",
    );
    expect(screen.getByText("price unavailable")).toBeInTheDocument();
  });

  it("shows an error with a working Try again button", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.reject(new TypeError("offline"))),
    );
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Could not reach the server",
    );

    fakeApi(makePortfolio());
    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Try again" }));
    expect(await findInTable("PETR4")).toBeInTheDocument();
  });

  it("reloads the portfolio after adding a position", async () => {
    fakeApi(makePortfolio());
    renderPage();
    const user = userEvent.setup();
    await findInTable("PETR4");
    await user.click(screen.getByRole("button", { name: "Add position" }));

    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText("Ticker"), "vale3");
    await user.type(within(dialog).getByLabelText("Quantity"), "10");
    await user.type(within(dialog).getByLabelText("Average price"), "60");
    await user.click(
      within(dialog).getByRole("button", { name: "Add position" }),
    );

    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    expect(await findInTable("VALE3")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent(
      "VALE3 (B3): price unavailable",
    );
  });

  it("reloads the portfolio after deleting a position", async () => {
    fakeApi(makePortfolio());
    renderPage();
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole("button", { name: "Delete PETR4 (B3)" }),
    );
    await user.click(
      within(await screen.findByRole("alertdialog")).getByRole("button", {
        name: "Delete",
      }),
    );
    expect(
      await screen.findByText("No positions yet — add your first stock"),
    ).toBeInTheDocument();
  });

  it("opens a position's transactions from the table", async () => {
    fakeApi(
      makePortfolio({
        positions: [makePortfolioPosition({ realizedProfit: "42" })],
      }),
    );
    renderPage();
    const user = userEvent.setup();
    await user.click(
      await screen.findByRole("button", { name: "Transactions of PETR4 (B3)" }),
    );

    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByRole("heading", { name: "PETR4 (B3) transactions" }),
    ).toBeInTheDocument();
    expect(normalizeSpaces(dialog.textContent)).toContain(
      "Realized profit+R$ 42,00",
    );
    expect(
      await within(dialog).findByRole("table", { name: "Transactions" }),
    ).toHaveTextContent("01/10/2026");
  });

  it("shows the realized profit card", async () => {
    fakeApi(makePortfolio());
    renderPage();
    await findInTable("PETR4");
    expect(cardText("Realized profit")).toBe("R$ 0,00");
  });
});
