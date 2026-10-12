import type { Position } from "@stock-viewer/shared";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { PositionForm } from "./PositionForm";
import { emptyFormValues, formValuesFromPosition } from "./positionForm";
import { useCreatePosition, useUpdatePosition } from "./usePositions";

type PositionDialogProps = {
  open: boolean;
  // null = add a new position; otherwise edit this one.
  position: Position | null;
  onOpenChange: (open: boolean) => void;
};

export function PositionDialog({
  open,
  position,
  onOpenChange,
}: PositionDialogProps) {
  const createPosition = useCreatePosition();
  const updatePosition = useUpdatePosition();
  const close = () => onOpenChange(false);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {position
              ? `Edit ${position.ticker} (${position.market})`
              : "Add position"}
          </DialogTitle>
          <DialogDescription>
            {position
              ? "Quantity and average price come from the transactions."
              : "Average price is in the market's currency (BRL for B3, USD for US). It is recorded as the first buy, dated today."}
          </DialogDescription>
        </DialogHeader>
        {open && (
          <PositionForm
            key={position?.id ?? "new"}
            isEdit={position !== null}
            defaultValues={
              position ? formValuesFromPosition(position) : emptyFormValues
            }
            onCancel={close}
            onSubmit={async ({
              quantity,
              averagePrice,
              notes,
              ...identity
            }) => {
              if (position) {
                await updatePosition.mutateAsync({
                  id: position.id,
                  input: { notes },
                });
              } else {
                await createPosition.mutateAsync({
                  ...identity,
                  quantity,
                  averagePrice,
                  notes,
                });
              }
              close();
            }}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
