// Preferences live in cookies so the server can read them while prerendering (no theme flash).
// An interactive Blazor circuit can't set HTTP cookies, so the browser writes them here.

const ONE_YEAR_SECONDS = 365 * 24 * 60 * 60;
const DARK_QUERY = "(prefers-color-scheme: dark)";

let colorSchemeListener = null;

export function setCookie(name, value) {
  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax`;
}

// "system" removes the attribute, so CSS follows prefers-color-scheme.
export function applyTheme(theme) {
  if (theme === "system") {
    delete document.documentElement.dataset.theme;
  } else {
    document.documentElement.dataset.theme = theme;
  }
}

// Tells .NET when the OS setting changes (the chart palette needs it). Returns the current value.
export function watchColorScheme(dotNet) {
  unwatchColorScheme();
  const query = window.matchMedia(DARK_QUERY);
  colorSchemeListener = () => dotNet.invokeMethodAsync("OnColorSchemeChanged", query.matches);
  query.addEventListener("change", colorSchemeListener);
  return query.matches;
}

export function unwatchColorScheme() {
  if (colorSchemeListener) {
    window.matchMedia(DARK_QUERY).removeEventListener("change", colorSchemeListener);
    colorSchemeListener = null;
  }
}
