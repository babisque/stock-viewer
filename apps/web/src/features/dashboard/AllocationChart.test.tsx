import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  makePortfolio,
  makePortfolioPosition,
  makeUnpricedPosition,
  makeUsPortfolioPosition,
  normalizeSpaces,
} from "@/test/fixtures";
import { marketAllocation } from "./allocation-data";
import { AllocationChart } from "./AllocationChart";

const portfolio = makePortfolio({
  positions: [
    makePortfolioPosition(),
    makeUsPortfolioPosition(),
    makeUnpricedPosition(),
  ],
});

function renderChart() {
  const { slices, note } = marketAllocation(portfolio, "BRL")!;
  render(
    <AllocationChart
      title="By market"
      slices={slices}
      note={note}
      currency="BRL"
    />,
  );
  return screen.getByRole("region", { name: "By market" });
}

describe("AllocationChart", () => {
  it("lists every slice with its value and share", () => {
    const region = renderChart();
    const rows = within(region)
      .getAllByRole("row")
      .slice(1)
      .map((row) => normalizeSpaces(row.textContent));
    expect(rows).toEqual(["B3R$ 3.000,0076.92%", "USR$ 900,0023.08%"]);
  });

  it("describes the chart for screen readers, not by colour", () => {
    const region = renderChart();
    expect(within(region).getByRole("img")).toHaveAccessibleName(
      "By market: B3 76.92%, US 23.08%",
    );
  });

  it("says which positions are left out", () => {
    const region = renderChart();
    expect(region).toHaveTextContent(
      "1 position without a price not included.",
    );
  });

  it("shows an empty state when nothing is priced", () => {
    render(
      <AllocationChart
        title="By position"
        slices={[]}
        note={null}
        currency="BRL"
      />,
    );
    expect(
      screen.getByText("No priced positions to show."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("img")).not.toBeInTheDocument();
  });
});
