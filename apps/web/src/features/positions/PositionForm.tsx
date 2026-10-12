import { zodResolver } from "@hookform/resolvers/zod";
import {
  currencyForMarket,
  formatMoney,
  formatQuantity,
  MarketSchema,
  type CreatePosition,
} from "@stock-viewer/shared";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { isApiError } from "@/lib/api";
import {
  editPositionFormSchema,
  positionFormSchema,
  type PositionFormValues,
} from "./positionForm";

type PositionFormProps = {
  defaultValues: PositionFormValues;
  // Ticker and market are immutable once the position exists, and quantity
  // and average price are derived from its transactions: only notes change.
  isEdit: boolean;
  onSubmit: (values: CreatePosition) => Promise<unknown>;
  onCancel: () => void;
};

export function PositionForm({
  defaultValues,
  isEdit,
  onSubmit,
  onCancel,
}: PositionFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PositionFormValues, unknown, CreatePosition>({
    defaultValues,
    resolver: zodResolver(isEdit ? editPositionFormSchema : positionFormSchema),
  });

  const submit = handleSubmit(async (values) => {
    try {
      await onSubmit(values);
    } catch (error) {
      if (isApiError(error) && error.code === "DUPLICATE_POSITION") {
        setError("ticker", { message: error.message });
      } else {
        const message =
          error instanceof Error ? error.message : "Something went wrong";
        setError("root.server", { message });
      }
    }
  });

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      {errors.root?.server && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.server.message}
        </p>
      )}

      <div className="grid grid-cols-2 gap-4">
        <Field label="Ticker" htmlFor="ticker" error={errors.ticker?.message}>
          <Input
            id="ticker"
            placeholder="PETR4"
            autoComplete="off"
            readOnly={isEdit}
            aria-invalid={errors.ticker ? true : undefined}
            className="uppercase"
            {...register("ticker")}
          />
        </Field>

        <Field label="Market" htmlFor="market" error={errors.market?.message}>
          <NativeSelect
            id="market"
            disabled={isEdit}
            aria-invalid={errors.market ? true : undefined}
            {...register("market")}
          >
            {MarketSchema.options.map((market) => (
              <option key={market} value={market}>
                {market}
              </option>
            ))}
          </NativeSelect>
        </Field>

        {isEdit ? (
          <DerivedValues values={defaultValues} />
        ) : (
          <>
            <Field
              label="Quantity"
              htmlFor="quantity"
              error={errors.quantity?.message}
            >
              <Input
                id="quantity"
                inputMode="decimal"
                placeholder="100"
                aria-invalid={errors.quantity ? true : undefined}
                {...register("quantity")}
              />
            </Field>

            <Field
              label="Average price"
              htmlFor="averagePrice"
              error={errors.averagePrice?.message}
            >
              <Input
                id="averagePrice"
                inputMode="decimal"
                placeholder="28,45"
                aria-invalid={errors.averagePrice ? true : undefined}
                {...register("averagePrice")}
              />
            </Field>
          </>
        )}
      </div>

      <Field label="Notes" htmlFor="notes" error={errors.notes?.message}>
        <Textarea
          id="notes"
          rows={2}
          aria-invalid={errors.notes ? true : undefined}
          {...register("notes")}
        />
      </Field>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : isEdit ? "Save changes" : "Add position"}
        </Button>
      </div>
    </form>
  );
}

type FieldProps = {
  label: string;
  htmlFor: string;
  error: string | undefined;
  children: React.ReactNode;
};

function Field({ label, htmlFor, error, children }: FieldProps) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {error && <p className="text-destructive text-xs">{error}</p>}
    </div>
  );
}

// Read-only, formatted: these come from the transactions.
function DerivedValues({ values }: { values: PositionFormValues }) {
  const hint = "Calculated from the transactions";
  return (
    <>
      <Field label="Quantity" htmlFor="quantity" error={undefined}>
        <Input
          id="quantity"
          readOnly
          value={formatQuantity(values.quantity, values.market)}
          aria-describedby="derived-hint"
        />
      </Field>
      <Field label="Average price" htmlFor="averagePrice" error={undefined}>
        <Input
          id="averagePrice"
          readOnly
          value={formatMoney(
            values.averagePrice,
            currencyForMarket(values.market),
          )}
          aria-describedby="derived-hint"
        />
      </Field>
      <p id="derived-hint" className="text-muted-foreground col-span-2 text-xs">
        {hint}. Record a buy or sell to change them.
      </p>
    </>
  );
}
