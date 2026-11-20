// Header Scroll State, Mobile Navigation Drawer, Cart Drawer & Responsive Filter Drawer
(() => {
  document.addEventListener("DOMContentLoaded", () => {
    const header = document.querySelector("[data-site-header]");
    const backdrop = document.querySelector("[data-drawer-backdrop]");
    const drawers = Array.from(document.querySelectorAll("[data-drawer]"));
    const desktopMedia = window.matchMedia("(min-width: 960px)");

    function isDesktopOnlyVisible(drawer) {
      return drawer.getAttribute("data-drawer-mobile-only") === "true" && desktopMedia.matches;
    }

    function syncAriaStates() {
      drawers.forEach((drawer) => {
        if (isDesktopOnlyVisible(drawer)) {
          drawer.classList.remove("is-open");
          drawer.removeAttribute("aria-hidden");
        } else if (!drawer.classList.contains("is-open")) {
          drawer.setAttribute("aria-hidden", "true");
        }
      });
    }

    function closeAllDrawers() {
      drawers.forEach((drawer) => {
        drawer.classList.remove("is-open");
        if (isDesktopOnlyVisible(drawer)) {
          drawer.removeAttribute("aria-hidden");
        } else {
          drawer.setAttribute("aria-hidden", "true");
        }
      });

      if (backdrop) {
        backdrop.classList.remove("is-open");
      }

      document.body.classList.remove("no-scroll");
      document.querySelectorAll("[data-drawer-open]").forEach((trigger) => {
        trigger.setAttribute("aria-expanded", "false");
      });
    }

    function openDrawer(name) {
      const target = drawers.find((d) => d.getAttribute("data-drawer") === name);
      if (!target || isDesktopOnlyVisible(target)) return;

      closeAllDrawers();
      target.classList.add("is-open");
      target.setAttribute("aria-hidden", "false");

      if (backdrop) {
        backdrop.classList.add("is-open");
      }

      document.body.classList.add("no-scroll");
      document.querySelectorAll(`[data-drawer-open="${name}"]`).forEach((trigger) => {
        trigger.setAttribute("aria-expanded", "true");
      });
    }

    document.querySelectorAll("[data-drawer-open]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const name = btn.getAttribute("data-drawer-open");
        if (name) openDrawer(name);
      });
    });

    document.querySelectorAll("[data-drawer-close]").forEach((btn) => {
      btn.addEventListener("click", closeAllDrawers);
    });

    if (backdrop) {
      backdrop.addEventListener("click", closeAllDrawers);
    }

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        const anyOpen = drawers.some((d) => d.classList.contains("is-open"));
        if (anyOpen) {
          closeAllDrawers();
        }
      }
    });

    desktopMedia.addEventListener("change", () => {
      const anyMobileDrawerOpen = drawers.some(
        (d) => d.getAttribute("data-drawer-mobile-only") === "true" && d.classList.contains("is-open")
      );
      if (anyMobileDrawerOpen && desktopMedia.matches) {
        closeAllDrawers();
      } else {
        syncAriaStates();
      }
    });

    syncAriaStates();

    if (header) {
      const onScroll = () => {
        header.classList.toggle("is-scrolled", window.scrollY > 8);
      };
      window.addEventListener("scroll", onScroll, { passive: true });
      onScroll();
    }

    window.TechStoreDrawers = {
      open: openDrawer,
      closeAll: closeAllDrawers
    };
  });
})();
