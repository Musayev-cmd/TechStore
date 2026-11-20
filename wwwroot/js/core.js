// Core TechStore client utilities (Vanilla ES6+, zero external libraries)
(() => {
  const i18n = window.TechStoreI18n || {};
  const config = window.TechStoreConfig || { currencySymbol: "₼", currencyCode: "AZN" };

  function getCsrfToken() {
    const meta = document.querySelector('meta[name="csrf-token"]');
    return meta ? meta.getAttribute("content") || "" : "";
  }

  function t(key, ...args) {
    let text = Object.prototype.hasOwnProperty.call(i18n, key) ? i18n[key] : key;
    args.forEach((arg, idx) => {
      text = text.replace(`{${idx}}`, String(arg));
    });
    return text;
  }

  function formatPrice(amount) {
    const num = Number(amount) || 0;
    const parts = num.toFixed(2).split(".");
    const intPart = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, " ");
    return `${intPart}.${parts[1]} ${config.currencySymbol}`;
  }

  async function apiFetch(url, options = {}) {
    const method = (options.method || "GET").toUpperCase();
    const headers = new Headers(options.headers || {});
    headers.set("Accept", "application/json");

    if (method !== "GET" && method !== "HEAD") {
      const token = getCsrfToken();
      if (token) {
        headers.set("RequestVerificationToken", token);
      }
      if (options.body && !(options.body instanceof FormData) && !headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
      }
    }

    const response = await fetch(url, {
      ...options,
      method,
      headers,
      credentials: "same-origin"
    });

    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.includes("application/json")
      ? await response.json()
      : await response.text();

    if (!response.ok) {
      const message = typeof payload === "object" && payload && payload.error
        ? payload.error
        : t("Js.NetworkError");
      const error = new Error(message);
      error.status = response.status;
      error.payload = payload;
      throw error;
    }

    return payload;
  }

  function showToast(message, type = "info") {
    let container = document.getElementById("toast-container");
    if (!container) {
      container = document.createElement("div");
      container.id = "toast-container";
      container.className = "toast-container";
      container.setAttribute("aria-live", "polite");
      document.body.appendChild(container);
    }

    const toast = document.createElement("div");
    toast.className = `toast is-${type}`;
    toast.textContent = message;
    container.appendChild(toast);

    requestAnimationFrame(() => toast.classList.add("is-visible"));

    window.setTimeout(() => {
      toast.classList.remove("is-visible");
      window.setTimeout(() => toast.remove(), 260);
    }, 3200);
  }

  window.TechStore = {
    getCsrfToken,
    t,
    formatPrice,
    apiFetch,
    showToast,
    config
  };
})();
