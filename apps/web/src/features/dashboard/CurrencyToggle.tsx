import type { Currency } from "@stock-viewer/shared";
import { Segmented } from "@/components/ui/segmented";

type CurrencyToggleProps = {
  value: Currency;
  onChange: (currency: Currency) => void;
  disabled?: boolean;
};

export function CurrencyToggle({
  value,
  onChange,
  disabled,
}: CurrencyToggleProps) {
  return (
    <Segmented
      label="Show totals in"
      options={[
        { value: "BRL", label: "BRL" },
        { value: "USD", label: "USD" },
      ]}
      value={value}
      onChange={onChange}
      disabled={disabled}
    />
  );
}
