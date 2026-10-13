// Opens and closes a native <dialog>. showModal() gives the focus trap, the backdrop and Esc-to-close;
// the dialog's "close" event tells Blazor when the user closed it.

export function show(dialog) {
  if (dialog && !dialog.open) {
    dialog.showModal();
  }
}

export function close(dialog) {
  if (dialog && dialog.open) {
    dialog.close();
  }
}
