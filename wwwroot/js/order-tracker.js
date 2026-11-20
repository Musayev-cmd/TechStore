// Live Order Status Polling for Checkout Success & Customer Order Details
(() => {
  function getBadgeClass(status) {
    if (status === "Paid" || status === "Delivered") return "badge-success";
    if (status === "Processing" || status === "Shipped") return "badge-accent";
    if (status === "Cancelled") return "badge-danger";
    return "badge-warning";
  }

  function formatUtcTimestamp(isoString) {
    if (!isoString) return "";
    const d = new Date(isoString);
    if (Number.isNaN(d.getTime())) return "";
    const pad = (n) => String(n).padStart(2, "0");
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} ${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())} UTC`;
  }

  document.addEventListener("DOMContentLoaded", () => {
    if (!window.TechStore) return;
    const { apiFetch, t, showToast } = window.TechStore;

    // 1. Checkout Success Pending Confirmation Polling
    const successRoot = document.querySelector("[data-checkout-success]");
    if (successRoot) {
      const pollUrl = successRoot.getAttribute("data-poll-url") || "";
      if (pollUrl) {
        const badgeEl = successRoot.querySelector("[data-order-status-badge]");
        const statusTextEl = successRoot.querySelector("[data-order-status-text]");
        const titleEl = successRoot.querySelector("[data-success-title]");
        const subtitleEl = successRoot.querySelector("[data-success-subtitle]");
        const iconEl = successRoot.querySelector("[data-success-icon]");
        const pollIndicator = successRoot.querySelector("[data-poll-indicator]");

        let attempts = 0;
        const maxAttempts = 30;
        let timerId = null;

        async function pollCheckoutStatus() {
          attempts += 1;
          try {
            const data = await apiFetch(pollUrl);
            if (!data) return;

            if (badgeEl) {
              badgeEl.className = `badge ${getBadgeClass(data.status)}`;
            }
            if (statusTextEl && data.statusLabel) {
              statusTextEl.textContent = data.statusLabel;
            }

            if (data.isPaid) {
              if (titleEl) titleEl.textContent = t("Checkout.SuccessTitle");
              if (subtitleEl) subtitleEl.textContent = t("Checkout.SuccessSubtitle");
              if (iconEl) {
                iconEl.className = "status-icon is-success";
                iconEl.innerHTML = `
                  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="20 6 9 17 4 12"></polyline>
                  </svg>
                `;
              }
              if (pollIndicator) pollIndicator.hidden = true;

              if (window.TechStoreCart) {
                window.TechStoreCart.clearLocalSnapshot();
                window.TechStoreCart.refresh();
              }
              showToast(t("Checkout.ConfirmedLive"), "success");
              return;
            }

            if (data.isCancelled) {
              if (pollIndicator) pollIndicator.hidden = true;
              return;
            }
          } catch {
            // Silently retry transient polling error
          }

          if (attempts < maxAttempts) {
            timerId = window.setTimeout(pollCheckoutStatus, 2500);
          } else if (pollIndicator) {
            pollIndicator.hidden = true;
          }
        }

        timerId = window.setTimeout(pollCheckoutStatus, 2500);
        window.addEventListener("beforeunload", () => {
          if (timerId) window.clearTimeout(timerId);
        });
      }
    }

    // 2. Customer Account Order Details Live Status Tracker
    const trackerRoot = document.querySelector("[data-order-tracker]");
    if (trackerRoot) {
      const pollUrl = trackerRoot.getAttribute("data-poll-url") || "";
      let currentStatus = trackerRoot.getAttribute("data-current-status") || "";
      let isTerminal = trackerRoot.getAttribute("data-is-terminal") === "true";

      const badgeEl = trackerRoot.querySelector("[data-order-status-badge]");
      const statusTextEl = trackerRoot.querySelector("[data-order-status-text]");
      const updatedEl = trackerRoot.querySelector("[data-tracker-updated]");
      const stepperEl = trackerRoot.querySelector("[data-order-stepper]");
      const cancelledEl = trackerRoot.querySelector("[data-tracker-cancelled]");
      const pendingNoteEl = trackerRoot.querySelector("[data-tracker-pending-note]");
      const refreshBtn = trackerRoot.querySelector("[data-tracker-refresh]");
      const pollIndicator = trackerRoot.querySelector("[data-poll-indicator]");

      let intervalId = null;

      function applyStatusUpdate(data, notifyOnChange) {
        const changed = data.status && data.status !== currentStatus;
        currentStatus = data.status || currentStatus;
        isTerminal = Boolean(data.isTerminal);

        if (badgeEl) {
          badgeEl.className = `badge ${getBadgeClass(data.status)}`;
        }
        if (statusTextEl && data.statusLabel) {
          statusTextEl.textContent = data.statusLabel;
        }
        if (updatedEl && data.updatedAtUtc) {
          const formatted = formatUtcTimestamp(data.updatedAtUtc);
          if (formatted) updatedEl.textContent = formatted;
        }

        if (data.isCancelled) {
          if (stepperEl) stepperEl.hidden = true;
          if (cancelledEl) cancelledEl.hidden = false;
          if (pendingNoteEl) pendingNoteEl.hidden = true;
        } else if (stepperEl) {
          const stepIndex = Number(data.statusStep);
          stepperEl.querySelectorAll("[data-step-index]").forEach((node) => {
            const idx = Number(node.getAttribute("data-step-index"));
            node.classList.toggle("is-done", idx < stepIndex);
            node.classList.toggle("is-current", idx === stepIndex);
          });
          if (pendingNoteEl) {
            pendingNoteEl.hidden = data.status !== "Pending";
          }
        }

        if (isTerminal) {
          if (intervalId) {
            window.clearInterval(intervalId);
            intervalId = null;
          }
          if (refreshBtn) refreshBtn.hidden = true;
          if (pollIndicator) pollIndicator.hidden = true;
        }

        if (changed && notifyOnChange) {
          showToast(t("Orders.StatusUpdated", data.statusLabel || data.status), "success");
          if (data.isPaid && window.TechStoreCart) {
            window.TechStoreCart.clearLocalSnapshot();
            window.TechStoreCart.refresh();
          }
        }
      }

      async function checkOrderStatus(manual = false) {
        if (!pollUrl) return;
        if (manual && refreshBtn) refreshBtn.disabled = true;

        try {
          const data = await apiFetch(pollUrl);
          if (data) {
            applyStatusUpdate(data, true);
          }
        } catch {
          if (manual) {
            showToast(t("Js.NetworkError"), "error");
          }
        } finally {
          if (manual && refreshBtn && !isTerminal) refreshBtn.disabled = false;
        }
      }

      if (refreshBtn) {
        refreshBtn.addEventListener("click", () => checkOrderStatus(true));
      }

      if (!isTerminal && pollUrl) {
        intervalId = window.setInterval(() => {
          if (!document.hidden && !isTerminal) {
            checkOrderStatus(false);
          }
        }, 10000);

        window.addEventListener("beforeunload", () => {
          if (intervalId) window.clearInterval(intervalId);
        });
      }
    }
  });
})();
