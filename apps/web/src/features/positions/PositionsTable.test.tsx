import type { Currency, PortfolioPosition } from "@stock-viewer/shared";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import {
  makeClosedPosition,
  makePortfolioPosition,
  makeUnpricedPosition,
  makeUsPortfolioPosition,
  normalizeSpaces,
} from "@/test/fixtures";
import { PositionsTable, PositionsTableSkeleton } from "./PositionsTable";

// PETR4 R$ 3.000, AAPL $180 (= R$ 900 at 5), XXXX3 without a price.
const petr4 = makePortfolioPosition();
const aapl = makeUsPortfolioPosition();
const unpriced = makeUnpricedPosition();

function cellsOf(row: HTMLElement) {
  return within(row)
    .getAllByRole("cell")
    .map((cell) => normalizeSpaces(cell.textContent));
}

function renderTable({
  positions = [petr4, aapl, unpriced],
  currency = "BRL",
  usdBrlRate = "5",
}: {
  positions?: PortfolioPosition[];
  currency?: Currency;
  usdBrlRate?: string | null;
} = {}) {
  render(
    <PositionsTable
      positions={positions}
      currency={currency}
      usdBrlRate={usdBrlRate}
      onTransactions={vi.fn()}
      onEdit={vi.fn()}
      onDelete={vi.fn()}
    />,
  );
}

function bodyRows() {
  const [, ...rows] = screen.getAllByRole("row");
  return rows;
}

function tickers() {
  return bodyRows().map(
    (row) => within(row).getAllByRole("cell")[0]!.textContent,
  );
}

async function clickHeader(name: string) {
  await userEvent.setup().click(screen.getByRole("button", { name }));
}

describe("PositionsTable", () => {
  it("shows price with source and time, value and a signed gain", () => {
    renderTable();
    const cells = cellsOf(bodyRows()[0]!);

    expect(cells.slice(0, 4)).toEqual(["PETR4", "B3", "100", "R$ 28,45"]);
    expect(cells[4]).toMatch(/^R\$ 30,00Yahoo · \d{2}:\d{2}$/);
    expect(cells.slice(5, 9)).toEqual([
      "R$ 2.845,00",
      "R$ 3.000,00",
      "+R$ 155,00",
      "+5.45%",
    ]);
    expect(screen.getByText("+R$ 155,00")).toHaveClass("text-gain");
  });

  it("shows a loss with a minus sign in the loss colour", () => {
    renderTable();
    const cells = cellsOf(bodyRows()[1]!);
    expect(cells.slice(6, 9)).toEqual(["$180.00", "−$20.00", "−10.00%"]);
    expect(screen.getByText("−10.00%")).toHaveClass("text-loss");
  });

  it("shows 'price unavailable' for positions without a quote", () => {
    renderTable();
    const cells = cellsOf(bodyRows()[2]!);
    expect(cells[4]).toBe("price unavailable");
    expect(cells[5]).toBe("R$ 2.845,00");
    expect(cells.slice(6, 9)).toEqual(["—", "—", "—"]);
  });

  it("shows realized profit for every position, signed", () => {
    renderTable({
      positions: [
        makePortfolioPosition({ realizedProfit: "120.5" }),
        makeUnpricedPosition({ realizedProfit: "-30" }),
      ],
    });
    expect(cellsOf(bodyRows()[0]!)[9]).toBe("+R$ 120,50");
    expect(cellsOf(bodyRows()[1]!)[9]).toBe("−R$ 30,00");
    expect(screen.getByText("+R$ 120,50")).toHaveClass("text-gain");
  });

  it("tags closed positions and mutes their row", () => {
    renderTable({ positions: [petr4, makeClosedPosition()] });
    const row = bodyRows()[1]!;
    expect(cellsOf(row).slice(0, 3)).toEqual(["BBAS3 closed", "B3", "0"]);
    expect(cellsOf(row).slice(6, 10)).toEqual(["—", "—", "—", "+R$ 300,00"]);
    expect(row).toHaveClass("text-muted-foreground");
    expect(bodyRows()[0]).not.toHaveClass("text-muted-foreground");
  });

  it("sorts by realized profit", async () => {
    renderTable({
      positions: [
        makePortfolioPosition({ realizedProfit: "200" }),
        makeUsPortfolioPosition({ realizedProfit: "20" }), // R$ 100 at 5
      ],
    });
    await clickHeader("Realized");
    expect(tickers()).toEqual(["AAPL", "PETR4"]);
  });

  it("keeps the API order until a column is sorted", () => {
    renderTable();
    expect(tickers()).toEqual(["PETR4", "AAPL", "XXXX3"]);
  });

  it("sorts money by the value converted to the selected currency", async () => {
    // AAPL is worth R$ 900 at 5 BRL/USD; a $1000 position would be R$ 5.000.
    const bigUs = makeUsPortfolioPosition({
      id: "pos-4",
      ticker: "MSFT",
      currentValue: "1000",
    });
    renderTable({ positions: [petr4, aapl, bigUs] });

    await clickHeader("Current value");
    expect(tickers()).toEqual(["AAPL", "PETR4", "MSFT"]);
    expect(
      screen.getByRole("columnheader", { name: "Current value" }),
    ).toHaveAttribute("aria-sort", "ascending");

    await clickHeader("Current value");
    expect(tickers()).toEqual(["MSFT", "PETR4", "AAPL"]);
  });

  it("puts positions without a value last in both directions", async () => {
    renderTable();
    await clickHeader("Profit");
    expect(tickers()).toEqual(["AAPL", "PETR4", "XXXX3"]);
    await clickHeader("Profit");
    expect(tickers()).toEqual(["PETR4", "AAPL", "XXXX3"]);
  });

  it("without a rate, sorts other-currency money last", async () => {
    renderTable({ usdBrlRate: null });
    await clickHeader("Invested");
    expect(tickers()).toEqual(["PETR4", "XXXX3", "AAPL"]);
  });

  it("sorts quantities as exact decimals, not as text", async () => {
    renderTable({
      positions: [
        makePortfolioPosition({ id: "a", ticker: "A", quantity: "9" }),
        makePortfolioPosition({ id: "b", ticker: "B", quantity: "10" }),
      ],
    });
    await clickHeader("Quantity");
    expect(tickers()).toEqual(["A", "B"]);
  });

  it("sorts text columns alphabetically", async () => {
    renderTable();
    await clickHeader("Ticker");
    expect(tickers()).toEqual(["AAPL", "PETR4", "XXXX3"]);
  });

  it("filters by market", async () => {
    renderTable();
    const user = userEvent.setup();
    const filter = screen.getByRole("group", { name: "Filter by market" });

    await user.click(within(filter).getByRole("button", { name: "US" }));
    expect(tickers()).toEqual(["AAPL"]);
    await user.click(within(filter).getByRole("button", { name: "B3" }));
    expect(tickers()).toEqual(["PETR4", "XXXX3"]);
    await user.click(within(filter).getByRole("button", { name: "All" }));
    expect(tickers()).toHaveLength(3);
  });

  it("says so when the filter leaves no rows", async () => {
    renderTable({ positions: [petr4] });
    await userEvent.setup().click(screen.getByRole("button", { name: "US" }));
    expect(screen.getByText("No US positions.")).toBeInTheDocument();
  });

  it("calls onTransactions, onEdit and onDelete with the row's position", async () => {
    const onTransactions = vi.fn();
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    render(
      <PositionsTable
        positions={[petr4]}
        currency="BRL"
        usdBrlRate="5"
        onTransactions={onTransactions}
        onEdit={onEdit}
        onDelete={onDelete}
      />,
    );
    const user = userEvent.setup();

    await user.click(
      screen.getByRole("button", { name: "Transactions of PETR4 (B3)" }),
    );
    expect(onTransactions).toHaveBeenCalledWith(petr4);
    await user.click(screen.getByRole("button", { name: "Edit PETR4 (B3)" }));
    expect(onEdit).toHaveBeenCalledWith(petr4);
    await user.click(screen.getByRole("button", { name: "Delete PETR4 (B3)" }));
    expect(onDelete).toHaveBeenCalledWith(petr4);
  });
});

describe("PositionsTableSkeleton", () => {
  it("renders a busy table with placeholder rows", () => {
    render(<PositionsTableSkeleton rows={2} />);
    const table = screen.getByRole("table", { name: "Loading portfolio" });
    expect(table).toHaveAttribute("aria-busy", "true");
    expect(within(table).getAllByRole("row")).toHaveLength(3);
  });
});
