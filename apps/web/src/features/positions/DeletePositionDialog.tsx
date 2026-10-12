import type { Position } from "@stock-viewer/shared";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { useDeletePosition } from "./usePositions";

type DeletePositionDialogProps = {
  position: Position | null;
  onClose: () => void;
};

export function DeletePositionDialog({
  position,
  onClose,
}: DeletePositionDialogProps) {
  const deletePosition = useDeletePosition();

  async function confirm() {
    if (!position) return;
    try {
      await deletePosition.mutateAsync(position);
      onClose();
    } catch {
      // The error message is rendered below from the mutation state.
    }
  }

  return (
    <AlertDialog
      open={position !== null}
      onOpenChange={(open) => {
        if (!open) {
          deletePosition.reset();
          onClose();
        }
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            Delete {position?.ticker} ({position?.market})?
          </AlertDialogTitle>
          <AlertDialogDescription>
            This position will be permanently removed.
          </AlertDialogDescription>
        </AlertDialogHeader>
        {deletePosition.error && (
          <p role="alert" className="text-destructive text-sm">
            {deletePosition.error.message}
          </p>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          {/* A plain button (not AlertDialogAction) so the dialog stays open until the delete succeeds. */}
          <Button
            variant="destructive"
            onClick={confirm}
            disabled={deletePosition.isPending}
          >
            {deletePosition.isPending ? "Deleting…" : "Delete"}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
