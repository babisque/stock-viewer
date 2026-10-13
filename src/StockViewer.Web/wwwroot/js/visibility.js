// Reports document.visibilitychange to .NET, so the dashboard only auto-refreshes while the tab is visible.

let listener = null;

// Returns whether the tab is visible right now.
export function watch(dotNet) {
  unwatch();
  listener = () => dotNet.invokeMethodAsync("OnVisibilityChanged", document.visibilityState === "visible");
  document.addEventListener("visibilitychange", listener);
  return document.visibilityState === "visible";
}

export function unwatch() {
  if (listener) {
    document.removeEventListener("visibilitychange", listener);
    listener = null;
  }
}
