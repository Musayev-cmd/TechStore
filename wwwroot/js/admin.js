// TechStore Admin Console Interactive Module (Vanilla ES6+, zero external libraries)
(() => {
  const ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp"];
  const ALLOWED_MIMES = ["image/jpeg", "image/pjpeg", "image/png", "image/webp"];

  function t(key, ...args) {
    if (window.TechStore && typeof window.TechStore.t === "function") {
      return window.TechStore.t(key, ...args);
    }
    return key;
  }

  function toast(message, type = "info") {
    if (window.TechStore && typeof window.TechStore.showToast === "function") {
      window.TechStore.showToast(message, type);
    }
  }

  function slugify(value) {
    return String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/-{2,}/g, "-")
      .replace(/^-+|-+$/g, "");
  }

  function initSidebar() {
    const sidebar = document.querySelector("[data-admin-sidebar]");
    const backdrop = document.querySelector("[data-admin-backdrop]");
    const openButtons = document.querySelectorAll("[data-admin-sidebar-open]");
    const closeButtons = document.querySelectorAll("[data-admin-sidebar-close]");
    const desktopMedia = window.matchMedia("(min-width: 1024px)");

    if (!sidebar) return;

    function close() {
      sidebar.classList.remove("is-open");
      if (backdrop) backdrop.classList.remove("is-open");
      document.body.classList.remove("no-scroll");
      openButtons.forEach((btn) => btn.setAttribute("aria-expanded", "false"));
    }

    function open() {
      if (desktopMedia.matches) return;
      sidebar.classList.add("is-open");
      if (backdrop) backdrop.classList.add("is-open");
      document.body.classList.add("no-scroll");
      openButtons.forEach((btn) => btn.setAttribute("aria-expanded", "true"));
    }

    openButtons.forEach((btn) => btn.addEventListener("click", open));
    closeButtons.forEach((btn) => btn.addEventListener("click", close));
    if (backdrop) backdrop.addEventListener("click", close);

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && sidebar.classList.contains("is-open")) {
        close();
      }
    });

    desktopMedia.addEventListener("change", () => {
      if (desktopMedia.matches) close();
    });
  }

  function initLangTabs() {
    document.querySelectorAll("[data-lang-tabs]").forEach((container) => {
      const tabs = Array.from(container.querySelectorAll("[data-lang-tab]"));
      const panels = Array.from(container.querySelectorAll("[data-lang-panel]"));

      function activate(lang) {
        tabs.forEach((tab) => {
          const isMatch = tab.getAttribute("data-lang-tab") === lang;
          tab.classList.toggle("is-active", isMatch);
          tab.setAttribute("aria-selected", isMatch ? "true" : "false");
        });
        panels.forEach((panel) => {
          const isMatch = panel.getAttribute("data-lang-panel") === lang;
          panel.hidden = !isMatch;
        });
      }

      tabs.forEach((tab) => {
        tab.addEventListener("click", () => {
          const lang = tab.getAttribute("data-lang-tab");
          if (lang) activate(lang);
        });
      });

      // Highlight tabs containing server-side validation errors and focus the first one
      let firstErrorLang = null;
      panels.forEach((panel) => {
        const lang = panel.getAttribute("data-lang-panel");
        const hasError = Array.from(panel.querySelectorAll(".field-error")).some(
          (el) => el.textContent && el.textContent.trim().length > 0
        );
        const dot = container.querySelector(`[data-lang-tab-error="${lang}"]`);
        if (dot) dot.hidden = !hasError;
        if (hasError && !firstErrorLang) {
          firstErrorLang = lang;
        }
      });

      if (firstErrorLang) {
        activate(firstErrorLang);
      }
    });
  }

  function initSlugAutoFill() {
    const source = document.querySelector('[data-slug-source="true"]');
    const target = document.querySelector("[data-slug-target]");
    if (!source || !target) return;

    let manuallyEdited = target.value.trim().length > 0;

    target.addEventListener("input", () => {
      manuallyEdited = target.value.trim().length > 0;
    });

    source.addEventListener("input", () => {
      if (!manuallyEdited) {
        target.value = slugify(source.value);
      }
    });
  }

  function initSpecsManager() {
    const manager = document.querySelector("[data-specs-manager]");
    if (!manager) return;

    const list = manager.querySelector("[data-specs-list]");
    const addBtn = manager.querySelector("[data-spec-add]");
    const template = manager.querySelector("[data-spec-row-template]");
    if (!list || !addBtn || !template) return;

    function reindex() {
      const rows = Array.from(list.querySelectorAll("[data-spec-row]"));
      rows.forEach((row, idx) => {
        row.querySelectorAll("[data-spec-field]").forEach((input) => {
          const field = input.getAttribute("data-spec-field");
          if (field) {
            input.setAttribute("name", `Specifications[${idx}].${field}`);
          }
        });
      });
    }

    function addRow() {
      const fragment = template.content.cloneNode(true);
      list.appendChild(fragment);
      reindex();
      const rows = list.querySelectorAll("[data-spec-row]");
      const last = rows[rows.length - 1];
      const firstInput = last ? last.querySelector("input") : null;
      if (firstInput) firstInput.focus();
    }

    addBtn.addEventListener("click", addRow);

    list.addEventListener("click", (event) => {
      const removeBtn = event.target.closest("[data-spec-remove]");
      if (!removeBtn) return;

      const row = removeBtn.closest("[data-spec-row]");
      if (!row) return;

      const rows = list.querySelectorAll("[data-spec-row]");
      if (rows.length <= 1) {
        row.querySelectorAll("input").forEach((input) => {
          input.value = "";
        });
      } else {
        row.remove();
      }
      reindex();
    });

    const form = manager.closest("form");
    if (form) {
      form.addEventListener("submit", reindex);
    }

    reindex();
  }

  function initImageUploader() {
    // Existing image removal visual feedback
    document.querySelectorAll("[data-remove-image-toggle]").forEach((checkbox) => {
      const card = checkbox.closest("[data-existing-image-card]");
      if (!card) return;
      const sync = () => card.classList.toggle("is-marked-remove", checkbox.checked);
      checkbox.addEventListener("change", sync);
      sync();
    });

    const dropzone = document.querySelector("[data-upload-dropzone]");
    const fileInput = document.querySelector("[data-upload-input]");
    const previews = document.querySelector("[data-upload-previews]");
    const statusEl = document.querySelector("[data-upload-status]");
    if (!dropzone || !fileInput || !previews) return;

    const maxBytes = (window.TechStoreConfig && window.TechStoreConfig.maxUploadBytes) || 5 * 1024 * 1024;
    let previewUrls = [];

    function clearPreviews() {
      previewUrls.forEach((url) => URL.revokeObjectURL(url));
      previewUrls = [];
      previews.innerHTML = "";
      if (statusEl) {
        statusEl.hidden = true;
        statusEl.textContent = "";
      }
    }

    function hasAllowedExt(name) {
      const lower = String(name || "").toLowerCase();
      return ALLOWED_EXTENSIONS.some((ext) => lower.endsWith(ext));
    }

    function renderSelectedFiles() {
      clearPreviews();
      const files = Array.from(fileInput.files || []);
      if (files.length === 0) return;

      const validFiles = [];
      files.forEach((file) => {
        const mimeOk = !file.type || ALLOWED_MIMES.includes(file.type.toLowerCase());
        if (!hasAllowedExt(file.name) || !mimeOk) {
          toast(t("AdminJs.InvalidFileType", file.name), "error");
          return;
        }
        if (file.size > maxBytes) {
          toast(t("AdminJs.FileTooLarge", file.name), "error");
          return;
        }
        validFiles.push(file);
      });

      // Keep only client-valid files in the input when DataTransfer is available
      if (typeof DataTransfer !== "undefined" && validFiles.length !== files.length) {
        const dt = new DataTransfer();
        validFiles.forEach((f) => dt.items.add(f));
        fileInput.files = dt.files;
      }

      if (statusEl && validFiles.length > 0) {
        statusEl.textContent = t("AdminJs.SelectedFilesCount", validFiles.length);
        statusEl.hidden = false;
      }

      validFiles.forEach((file) => {
        const url = URL.createObjectURL(file);
        previewUrls.push(url);

        const card = document.createElement("div");
        card.className = "upload-preview-card";

        const img = document.createElement("img");
        img.src = url;
        img.alt = file.name;

        const meta = document.createElement("div");
        meta.className = "upload-preview-meta mono";
        const kb = Math.max(1, Math.round(file.size / 1024));
        meta.textContent = `${file.name} (${kb} KB)`;
        meta.title = file.name;

        card.appendChild(img);
        card.appendChild(meta);
        previews.appendChild(card);
      });
    }

    fileInput.addEventListener("change", renderSelectedFiles);

    ["dragenter", "dragover"].forEach((evt) => {
      dropzone.addEventListener(evt, (event) => {
        event.preventDefault();
        dropzone.classList.add("is-dragover");
      });
    });

    ["dragleave", "drop"].forEach((evt) => {
      dropzone.addEventListener(evt, (event) => {
        event.preventDefault();
        dropzone.classList.remove("is-dragover");
      });
    });

    dropzone.addEventListener("drop", (event) => {
      const dropped = event.dataTransfer && event.dataTransfer.files;
      if (!dropped || dropped.length === 0) return;

      if (typeof DataTransfer !== "undefined") {
        const dt = new DataTransfer();
        Array.from(dropped).forEach((f) => dt.items.add(f));
        fileInput.files = dt.files;
        renderSelectedFiles();
      }
    });
  }

  function initConfirmModal() {
    const modal = document.querySelector("[data-confirm-modal]");
    if (!modal) return;

    const messageEl = modal.querySelector("[data-confirm-message]");
    const acceptBtn = modal.querySelector("[data-confirm-accept]");
    const cancelBtns = modal.querySelectorAll("[data-confirm-cancel]");
    let pendingForm = null;

    function close() {
      modal.hidden = true;
      document.body.classList.remove("no-scroll");
      pendingForm = null;
    }

    function open(form, message) {
      pendingForm = form;
      if (messageEl) {
        messageEl.textContent = message || t("AdminJs.ConfirmDeleteDefault");
      }
      modal.hidden = false;
      document.body.classList.add("no-scroll");
      if (acceptBtn) acceptBtn.focus();
    }

    document.addEventListener("submit", (event) => {
      const form = event.target.closest("[data-confirm-form]");
      if (!form) return;

      if (form.getAttribute("data-confirmed") === "true") {
        form.removeAttribute("data-confirmed");
        return;
      }

      event.preventDefault();
      open(form, form.getAttribute("data-confirm-message"));
    });

    if (acceptBtn) {
      acceptBtn.addEventListener("click", () => {
        if (!pendingForm) return;
        const formToSubmit = pendingForm;
        close();
        formToSubmit.setAttribute("data-confirmed", "true");
        formToSubmit.submit();
      });
    }

    cancelBtns.forEach((btn) => btn.addEventListener("click", close));

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && !modal.hidden) {
        close();
      }
    });
  }

  document.addEventListener("DOMContentLoaded", () => {
    initSidebar();
    initLangTabs();
    initSlugAutoFill();
    initSpecsManager();
    initImageUploader();
    initConfirmModal();
  });
})();
