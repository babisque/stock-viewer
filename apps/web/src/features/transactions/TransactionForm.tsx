import { zodResolver } from "@hookform/resolvers/zod";
import {
  formatMoney,
  formatQuantity,
  todayIsoDate,
  type CreateTransaction,
  type Currency,
  type Market,
} from "@stock-viewer/shared";
import { useMemo } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Segmented } from "@/components/ui/segmented";
import { isApiError } from "@/lib/api";
import {
  emptyTransactionValues,
  transactionFormSchema,
  transactionTotal,
  type TransactionFormValues,
} from "./transactionForm";

type TransactionFormProps = {
  market: Market;
  currency: Currency;
  available: string; // quantity held now
  onSubmit: (values: CreateTransaction) => Promise<unknown>;
};

export function TransactionForm({
  market,
  currency,
  available,
  onSubmit,
}: TransactionFormProps) {
  const schema = useMemo(
    () => transactionFormSchema(market, available),
    [market, available],
  );
  const {
    control,
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<TransactionFormValues, unknown, CreateTransaction>({
    defaultValues: emptyTransactionValues(),
    resolver: zodResolver(schema),
  });
  const values = useWatch({ control });
  const type = values.type ?? "BUY";
  const total = transactionTotal({
    type,
    quantity: values.quantity ?? "",
    unitPrice: values.unitPrice ?? "",
    fees: values.fees ?? "",
  });

  const submit = handleSubmit(async (input) => {
    try {
      await onSubmit(input);
      // Keep type and date: several trades are often entered in a row.
      reset({
        ...emptyTransactionValues(),
        type: input.type,
        date: input.date,
      });
    } catch (error) {
      if (isApiError(error) && error.code === "INSUFFICIENT_QUANTITY") {
        setError("quantity", { message: error.message });
      } else {
        const message =
          error instanceof Error ? error.message : "Something went wrong";
        setError("root.server", { message });
      }
    }
  });

  return (
    <form
      onSubmit={submit}
      noValidate
      aria-label="Add transaction"
      className="grid gap-3"
    >
      {errors.root?.server && (
        <p role="alert" className="text-destructive text-sm">
          {errors.root.server.message}
        </p>
      )}

      <div className="flex flex-wrap items-end justify-between gap-3">
        <Controller
          control={control}
          name="type"
          render={({ field }) => (
            <Segmented
              label="Transaction type"
              options={[
                { value: "BUY", label: "Buy" },
                { value: "SELL", label: "Sell" },
              ]}
              value={field.value}
              onChange={field.onChange}
            />
          )}
        />
        {type === "SELL" && (
          <p className="text-muted-foreground text-xs">
            Available: {formatQuantity(available, market)}
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Field label="Date" htmlFor="tx-date" error={errors.date?.message}>
          <Input
            id="tx-date"
            type="date"
            max={todayIsoDate()}
            aria-invalid={errors.date ? true : undefined}
            {...register("date")}
          />
        </Field>
        <Field
          label="Quantity"
          htmlFor="tx-quantity"
          error={errors.quantity?.message}
        >
          <Input
            id="tx-quantity"
            inputMode="decimal"
            placeholder="100"
            aria-invalid={errors.quantity ? true : undefined}
            {...register("quantity")}
          />
        </Field>
        <Field
          label="Unit price"
          htmlFor="tx-unitPrice"
          error={errors.unitPrice?.message}
        >
          <Input
            id="tx-unitPrice"
            inputMode="decimal"
            placeholder="28,45"
            aria-invalid={errors.unitPrice ? true : undefined}
            {...register("unitPrice")}
          />
        </Field>
        <Field label="Fees" htmlFor="tx-fees" error={errors.fees?.message}>
          <Input
            id="tx-fees"
            inputMode="decimal"
            placeholder="0"
            aria-invalid={errors.fees ? true : undefined}
            {...register("fees")}
          />
        </Field>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm" aria-live="polite">
          {type === "BUY" ? "Total cost" : "Net proceeds"}:{" "}
          <span className="text-foreground font-medium tabular-nums">
            {total ? formatMoney(total, currency) : "—"}
          </span>
        </p>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting
            ? "Saving…"
            : type === "BUY"
              ? "Record buy"
              : "Record sell"}
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
    <div className="grid content-start gap-1.5">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {error && <p className="text-destructive text-xs">{error}</p>}
    </div>
  );
}
