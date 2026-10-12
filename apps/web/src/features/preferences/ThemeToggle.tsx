import { Segmented } from "@/components/ui/segmented";
import type { Theme } from "./usePreferences";

type ThemeToggleProps = {
  value: Theme;
  onChange: (theme: Theme) => void;
};

export function ThemeToggle({ value, onChange }: ThemeToggleProps) {
  return (
    <Segmented
      label="Theme"
      options={[
        { value: "system", label: "System" },
        { value: "light", label: "Light" },
        { value: "dark", label: "Dark" },
      ]}
      value={value}
      onChange={onChange}
    />
  );
}
