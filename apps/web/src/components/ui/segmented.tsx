import { cn } from "@/lib/utils";

type SegmentedOption<T extends string> = { value: T; label: string };

type SegmentedProps<T extends string> = {
  label: string; // accessible name of the group
  options: SegmentedOption<T>[];
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
  className?: string;
};

// A small group of toggle buttons where exactly one is pressed.
function Segmented<T extends string>({
  label,
  options,
  value,
  onChange,
  disabled,
  className,
}: SegmentedProps<T>) {
  return (
    <div
      role="group"
      aria-label={label}
      className={cn(
        "bg-muted inline-flex rounded-md p-0.5 text-sm",
        disabled && "opacity-50",
        className,
      )}
    >
      {options.map((option) => {
        const pressed = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={pressed}
            disabled={disabled}
            onClick={() => onChange(option.value)}
            className={cn(
              "rounded-sm px-3 py-1 font-medium transition-colors outline-none",
              "focus-visible:ring-ring/50 focus-visible:ring-[3px]",
              "disabled:cursor-not-allowed",
              pressed
                ? "bg-background text-foreground shadow-xs"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}

export { Segmented, type SegmentedOption };
