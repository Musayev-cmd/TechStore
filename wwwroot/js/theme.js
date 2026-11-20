// Instant Light/Dark Theme Controller (synced with inline <head> bootstrap script)
(() => {
  const STORAGE_KEY = "techstore-theme";
  const root = document.documentElement;

  function getCurrentTheme() {
    return root.getAttribute("data-theme") === "dark" ? "dark" : "light";
  }

  function applyTheme(theme, persist = false) {
    const normalized = theme === "dark" ? "dark" : "light";
    root.setAttribute("data-theme", normalized);

    if (persist) {
      try {
        window.localStorage.setItem(STORAGE_KEY, normalized);
      } catch {
        // Ignore storage quota / private-mode restrictions
      }
    }

    document.querySelectorAll("[data-theme-toggle]").forEach((btn) => {
      btn.setAttribute("aria-pressed", normalized === "dark" ? "true" : "false");
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    applyTheme(getCurrentTheme(), false);

    document.querySelectorAll("[data-theme-toggle]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const next = getCurrentTheme() === "dark" ? "light" : "dark";
        applyTheme(next, true);
      });
    });

    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    mediaQuery.addEventListener("change", (event) => {
      try {
        if (!window.localStorage.getItem(STORAGE_KEY)) {
          applyTheme(event.matches ? "dark" : "light", false);
        }
      } catch {
        applyTheme(event.matches ? "dark" : "light", false);
      }
    });
  });
})();
