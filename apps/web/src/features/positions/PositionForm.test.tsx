import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { apiError } from "@/lib/api";
import { PositionForm } from "./PositionForm";
import { makePosition, normalizeSpaces } from "@/test/fixtures";
import { emptyFormValues, formValuesFromPosition } from "./positionForm";

function renderForm(
  props: Partial<React.ComponentProps<typeof PositionForm>> = {},
) {
  const onSubmit = vi.fn(() => Promise.resolve());
  const onCancel = vi.fn();
  render(
    <PositionForm
      defaultValues={emptyFormValues}
      isEdit={false}
      onSubmit={onSubmit}
      onCancel={onCancel}
      {...props}
    />,
  );
  return { onSubmit: props.onSubmit ?? onSubmit, onCancel };
}

async function fill(fields: {
  ticker?: string;
  quantity?: string;
  averagePrice?: string;
}) {
  const user = userEvent.setup();
  if (fields.ticker)
    await user.type(screen.getByLabelText("Ticker"), fields.ticker);
  if (fields.quantity)
    await user.type(screen.getByLabelText("Quantity"), fields.quantity);
  if (fields.averagePrice)
    await user.type(
      screen.getByLabelText("Average price"),
      fields.averagePrice,
    );
  return user;
}

describe("PositionForm", () => {
  it("submits normalized values (uppercase ticker, comma decimal)", async () => {
    const { onSubmit } = renderForm();
    const user = await fill({
      ticker: "petr4",
      quantity: "100",
      averagePrice: "28,45",
    });
    await user.click(screen.getByRole("button", { name: "Add position" }));

    expect(onSubmit).toHaveBeenCalledWith({
      ticker: "PETR4",
      market: "B3",
      quantity: "100",
      averagePrice: "28.45",
      notes: null,
    });
  });

  it("shows inline errors for an empty ticker and a negative quantity", async () => {
    const { onSubmit } = renderForm();
    const user = await fill({ quantity: "-1", averagePrice: "10" });
    await user.click(screen.getByRole("button", { name: "Add position" }));

    expect(await screen.findByText("Ticker is required")).toBeInTheDocument();
    expect(
      screen.getByText("Quantity must be a positive number"),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Quantity")).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("rejects fractional quantity for B3 but accepts it for US", async () => {
    const { onSubmit } = renderForm();
    const user = await fill({
      ticker: "AAPL",
      quantity: "1,5",
      averagePrice: "190",
    });
    await user.click(screen.getByRole("button", { name: "Add position" }));
    expect(
      await screen.findByText("B3 quantity must be a whole number"),
    ).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText("Market"), "US");
    await user.click(screen.getByRole("button", { name: "Add position" }));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ market: "US", quantity: "1.5" }),
    );
  });

  it("maps a duplicate (409) server error onto the ticker field", async () => {
    const onSubmit = vi.fn(() =>
      Promise.reject(
        apiError(
          409,
          "DUPLICATE_POSITION",
          "A position for PETR4 (B3) already exists",
        ),
      ),
    );
    renderForm({ onSubmit });
    const user = await fill({
      ticker: "PETR4",
      quantity: "1",
      averagePrice: "1",
    });
    await user.click(screen.getByRole("button", { name: "Add position" }));

    expect(
      await screen.findByText("A position for PETR4 (B3) already exists"),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Ticker")).toHaveAttribute(
      "aria-invalid",
      "true",
    );
  });

  it("shows other server errors at the top of the form", async () => {
    const onSubmit = vi.fn(() =>
      Promise.reject(
        apiError(0, "NETWORK_ERROR", "Could not reach the server"),
      ),
    );
    renderForm({ onSubmit });
    const user = await fill({
      ticker: "PETR4",
      quantity: "1",
      averagePrice: "1",
    });
    await user.click(screen.getByRole("button", { name: "Add position" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Could not reach the server",
    );
  });

  it("locks ticker and market in edit mode", () => {
    renderForm({
      isEdit: true,
      defaultValues: formValuesFromPosition(makePosition()),
    });
    expect(screen.getByLabelText("Ticker")).toHaveAttribute("readonly");
    expect(screen.getByLabelText("Market")).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "Save changes" }),
    ).toBeInTheDocument();
  });

  it("shows derived quantity and average price read-only and formatted", () => {
    renderForm({
      isEdit: true,
      defaultValues: formValuesFromPosition(
        makePosition({
          quantity: "1000",
          averagePrice: "13.333333333333333333",
        }),
      ),
    });
    expect(screen.getByLabelText("Quantity")).toHaveAttribute("readonly");
    expect(screen.getByLabelText("Quantity")).toHaveValue("1.000");
    expect(
      normalizeSpaces(
        (screen.getByLabelText("Average price") as HTMLInputElement).value,
      ),
    ).toBe("R$ 13,33");
    expect(
      screen.getByText(/Calculated from the transactions/),
    ).toBeInTheDocument();
  });

  it("saves notes for a closed position", async () => {
    const { onSubmit } = renderForm({
      isEdit: true,
      defaultValues: formValuesFromPosition(
        makePosition({ quantity: "0", averagePrice: "13.333333333333333333" }),
      ),
    });
    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Notes"), "sold it all");
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ notes: "sold it all" }),
    );
  });

  it("calls onCancel", async () => {
    const { onCancel } = renderForm();
    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Cancel" }));
    expect(onCancel).toHaveBeenCalled();
  });
});
