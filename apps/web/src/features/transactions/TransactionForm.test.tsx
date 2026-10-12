import { todayIsoDate } from "@stock-viewer/shared";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { apiError } from "@/lib/api";
import { normalizeSpaces } from "@/test/fixtures";
import { TransactionForm } from "./TransactionForm";

function renderForm(
  props: Partial<React.ComponentProps<typeof TransactionForm>> = {},
) {
  const onSubmit = vi.fn(() => Promise.resolve());
  render(
    <TransactionForm
      market="B3"
      currency="BRL"
      available="100"
      onSubmit={onSubmit}
      {...props}
    />,
  );
  return { onSubmit: props.onSubmit ?? onSubmit, user: userEvent.setup() };
}

async function fill(
  user: ReturnType<typeof userEvent.setup>,
  fields: { quantity?: string; unitPrice?: string; fees?: string },
) {
  if (fields.quantity)
    await user.type(screen.getByLabelText("Quantity"), fields.quantity);
  if (fields.unitPrice)
    await user.type(screen.getByLabelText("Unit price"), fields.unitPrice);
  if (fields.fees) {
    await user.clear(screen.getByLabelText("Fees"));
    await user.type(screen.getByLabelText("Fees"), fields.fees);
  }
}

function totalText() {
  return normalizeSpaces(
    screen.getByText(/^(Total cost|Net proceeds)/).textContent,
  );
}

describe("TransactionForm", () => {
  it("defaults to a buy dated today without fees", () => {
    renderForm();
    expect(screen.getByRole("button", { name: "Buy" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(screen.getByLabelText("Date")).toHaveValue(todayIsoDate());
    expect(screen.getByLabelText("Fees")).toHaveValue("0");
  });

  it("submits a buy with normalized values and previews the total cost", async () => {
    const { onSubmit, user } = renderForm();
    await fill(user, { quantity: "10", unitPrice: "28,45", fees: "4,90" });
    expect(totalText()).toBe("Total cost: R$ 289,40");

    await user.click(screen.getByRole("button", { name: "Record buy" }));
    expect(onSubmit).toHaveBeenCalledWith({
      type: "BUY",
      date: todayIsoDate(),
      quantity: "10",
      unitPrice: "28.45",
      fees: "4.90",
    });
    // Cleared for the next entry.
    expect(screen.getByLabelText("Quantity")).toHaveValue("");
  });

  it("switches to sell, shows what's available and the net proceeds", async () => {
    const { onSubmit, user } = renderForm();
    await user.click(screen.getByRole("button", { name: "Sell" }));
    expect(screen.getByText("Available: 100")).toBeInTheDocument();

    await fill(user, { quantity: "40", unitPrice: "15", fees: "1" });
    expect(totalText()).toBe("Net proceeds: R$ 599,00");
    await user.click(screen.getByRole("button", { name: "Record sell" }));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ type: "SELL", quantity: "40" }),
    );
    // The type is kept for the next entry.
    expect(screen.getByRole("button", { name: "Sell" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("blocks selling more than is held", async () => {
    const { onSubmit, user } = renderForm();
    await user.click(screen.getByRole("button", { name: "Sell" }));
    await fill(user, { quantity: "101", unitPrice: "15" });
    await user.click(screen.getByRole("button", { name: "Record sell" }));
    expect(screen.getByText("You can sell at most 100")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("shows inline errors for missing values", async () => {
    const { onSubmit, user } = renderForm();
    await user.click(screen.getByRole("button", { name: "Record buy" }));
    expect(screen.getByText("Quantity is required")).toBeInTheDocument();
    expect(screen.getByText("Unit price is required")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("maps an insufficient-quantity (409) error onto the quantity field", async () => {
    const onSubmit = vi.fn(() =>
      Promise.reject(
        apiError(
          409,
          "INSUFFICIENT_QUANTITY",
          "Can't sell 50 on 2026-01-05: only 0 held at that point",
        ),
      ),
    );
    const { user } = renderForm({ onSubmit });
    await user.click(screen.getByRole("button", { name: "Sell" }));
    await fill(user, { quantity: "50", unitPrice: "10" });
    await user.click(screen.getByRole("button", { name: "Record sell" }));
    expect(
      await screen.findByText(
        "Can't sell 50 on 2026-01-05: only 0 held at that point",
      ),
    ).toBeInTheDocument();
    // Values are kept so the user can fix the date or quantity.
    expect(screen.getByLabelText("Quantity")).toHaveValue("50");
  });

  it("shows other server errors at the top of the form", async () => {
    const onSubmit = vi.fn(() =>
      Promise.reject(apiError(500, "INTERNAL_ERROR", "Something went wrong")),
    );
    const { user } = renderForm({ onSubmit });
    await fill(user, { quantity: "1", unitPrice: "1" });
    await user.click(screen.getByRole("button", { name: "Record buy" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Something went wrong",
    );
  });
});
