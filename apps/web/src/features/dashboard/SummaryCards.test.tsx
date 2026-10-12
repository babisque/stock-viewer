import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { makePortfolio, normalizeSpaces } from "@/test/fixtures";
import { SummaryCards } from "./SummaryCards";

// Map of card title -> card text, e.g. { "Total invested": "R$ 2.845,00" }.
function cards() {
  const terms = screen.getAllByRole("term");
  return Object.fromEntries(
    terms.map((term) => [
      term.textContent,
      normalizeSpaces(term.parentElement!.textContent).slice(
        term.textContent!.length,
      ),
    ]),
  );
}

describe("SummaryCards", () => {
  it("shows the BRL totals and the rate", () => {
    const { totals, fx } = makePortfolio();
    render(<SummaryCards totals={totals} fx={fx} currency="BRL" />);
    expect(cards()).toEqual({
      "Total invested": "R$ 2.845,00",
      "Current value": "R$ 3.000,00",
      "Unrealized profit": "+R$ 155,00+5.45%",
      "Realized profit": "R$ 0,00",
      "USD/BRL": expect.stringMatching(/^R\$ 5,00AwesomeAPI · \d{2}:\d{2}$/),
    });
    expect(screen.getByText("+R$ 155,00")).toHaveClass("text-gain");
  });

  it("shows the USD totals when USD is selected", () => {
    const { totals, fx } = makePortfolio();
    render(<SummaryCards totals={totals} fx={fx} currency="USD" />);
    expect(cards()).toMatchObject({
      "Total invested": "$569.00",
      "Current value": "$600.00",
      "Unrealized profit": "+$31.00+5.45%",
      "Realized profit": "$0.00",
    });
  });

  it("shows a loss with a minus sign in the loss colour", () => {
    const { fx } = makePortfolio();
    const loss = {
      invested: "1000",
      currentValue: "900",
      profit: "-100",
      profitPercent: "-10",
      realizedProfit: "-50",
    };
    render(
      <SummaryCards totals={{ BRL: loss, USD: loss }} fx={fx} currency="BRL" />,
    );
    expect(screen.getByText("−R$ 100,00")).toHaveClass("text-loss");
    expect(screen.getByText("−10.00%")).toHaveClass("text-loss");
    expect(screen.getByText("−R$ 50,00")).toHaveClass("text-loss");
  });

  it("shows realized profit separately, signed and coloured", () => {
    const { totals, fx } = makePortfolio();
    render(
      <SummaryCards
        totals={{
          BRL: { ...totals!.BRL, realizedProfit: "1234.5" },
          USD: { ...totals!.USD, realizedProfit: "246.9" },
        }}
        fx={fx}
        currency="BRL"
      />,
    );
    expect(cards()).toMatchObject({
      "Unrealized profit": "+R$ 155,00+5.45%",
      "Realized profit": "+R$ 1.234,50",
    });
    expect(screen.getByText("+R$ 1.234,50")).toHaveClass("text-gain");
  });

  it("explains missing totals when there is no rate", () => {
    render(<SummaryCards totals={null} fx={null} currency="BRL" />);
    expect(cards()).toEqual({
      "Total invested": "—",
      "Current value": "—",
      "Unrealized profit": "—",
      "Realized profit": "—",
      "USD/BRL": "—unavailable",
    });
    expect(
      screen.getByText("Totals are unavailable without the USD/BRL rate."),
    ).toBeInTheDocument();
  });
});
