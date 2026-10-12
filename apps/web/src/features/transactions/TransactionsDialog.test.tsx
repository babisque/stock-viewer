import type { PortfolioPosition, Transaction } from "@stock-viewer/shared";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  makePortfolioPosition,
  makeTransaction,
  normalizeSpaces,
} from "@/test/fixtures";
import { TransactionsDialog } from "./TransactionsDialog";

// In-memory fake of the transaction endpoints for one position.
function fakeApi(initial: Transaction[]) {
  let transactions = initial;
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const method = init?.method ?? "GET";
    if (method === "GET" && url === "/api/positions/pos-1/transactions") {
      return Response.json(transactions);
    }
    if (method === "POST" && url === "/api/positions/pos-1/transactions") {
      const body = JSON.parse(String(init!.body));
      transactions = [
        ...transactions,
        makeTransaction({ ...body, id: `tx-${transactions.length + 1}` }),
      ];
      return Response.json(makePortfolioPosition(), { status: 201 });
    }
    if (method === "DELETE" && url === "/api/transactions/tx-1") {
      return Response.json(
        {
          error: {
            code: "INSUFFICIENT_QUANTITY",
            message:
              "Can't delete this transaction. Can't sell 40 on 2026-10-05: only 0 held at that point",
          },
        },
        { status: 409 },
      );
    }
    if (method === "DELETE") {
      const id = url.split("/").pop();
      transactions = transactions.filter((t) => t.id !== id);
      return new Response(null, { status: 204 });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  });
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

function renderDialog(position: PortfolioPosition | null) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const onClose = vi.fn();
  render(
    <QueryClientProvider client={queryClient}>
      <TransactionsDialog position={position} onClose={onClose} />
    </QueryClientProvider>,
  );
  return { onClose, user: userEvent.setup() };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

const history = [
  makeTransaction({ id: "tx-1", date: "2026-10-01" }),
  makeTransaction({
    id: "tx-2",
    type: "SELL",
    date: "2026-10-05",
    quantity: "40",
    unitPrice: "30",
    fees: "4.90",
  }),
];

function rowsText() {
  const table = screen.getByRole("table", { name: "Transactions" });
  const [, ...rows] = within(table).getAllByRole("row");
  return rows.map((row) => normalizeSpaces(row.textContent));
}

describe("TransactionsDialog", () => {
  it("shows the position summary and the history, newest first", async () => {
    fakeApi(history);
    renderDialog(
      makePortfolioPosition({ quantity: "60", realizedProfit: "57.1" }),
    );

    expect(
      screen.getByRole("heading", { name: "PETR4 (B3) transactions" }),
    ).toBeInTheDocument();
    const summary = normalizeSpaces(
      screen.getByText("Realized profit").closest("dl")!.textContent,
    );
    expect(summary).toBe("Quantity60Avg priceR$ 28,45Realized profit+R$ 57,10");

    await screen.findByRole("table", { name: "Transactions" });
    expect(rowsText()).toEqual([
      "05/10/2026Sell40R$ 30,00R$ 4,90R$ 1.195,10",
      "01/10/2026Buy100R$ 28,45R$ 0,00R$ 2.845,00",
    ]);
  });

  it("marks a closed position", async () => {
    fakeApi([]);
    renderDialog(makePortfolioPosition({ quantity: "0" }));
    expect(screen.getByText("(closed)")).toBeInTheDocument();
    expect(await screen.findByText("No transactions.")).toBeInTheDocument();
  });

  it("records a transaction and reloads the history", async () => {
    const fetch = fakeApi(history);
    const { user } = renderDialog(makePortfolioPosition());
    await screen.findByRole("table", { name: "Transactions" });

    await user.type(screen.getByLabelText("Quantity"), "10");
    await user.type(screen.getByLabelText("Unit price"), "31");
    await user.click(screen.getByRole("button", { name: "Record buy" }));

    await waitFor(() => expect(rowsText()).toHaveLength(3));
    const post = fetch.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post![1]!.body))).toMatchObject({
      type: "BUY",
      quantity: "10",
      unitPrice: "31",
      fees: "0",
    });
  });

  it("deletes a transaction after an inline confirmation", async () => {
    fakeApi(history);
    const { user } = renderDialog(makePortfolioPosition());
    await screen.findByRole("table", { name: "Transactions" });

    await user.click(
      screen.getByRole("button", { name: "Delete Sell of 05/10/2026" }),
    );
    expect(screen.getByText("Delete?")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Keep" }));
    expect(screen.queryByText("Delete?")).not.toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "Delete Sell of 05/10/2026" }),
    );
    await user.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(rowsText()).toHaveLength(1));
  });

  it("explains why a transaction can't be deleted", async () => {
    fakeApi(history);
    const { user } = renderDialog(makePortfolioPosition());
    await screen.findByRole("table", { name: "Transactions" });

    await user.click(
      screen.getByRole("button", { name: "Delete Buy of 01/10/2026" }),
    );
    await user.click(screen.getByRole("button", { name: "Delete" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Can't delete this transaction. Can't sell 40 on 2026-10-05",
    );
    expect(rowsText()).toHaveLength(2);
  });

  it("renders nothing when no position is selected", () => {
    fakeApi([]);
    renderDialog(null);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
