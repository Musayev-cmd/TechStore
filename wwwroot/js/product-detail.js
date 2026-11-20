// Product Detail Interactive Gallery, Fullscreen Lightbox, Quantity Stepper & SKU Copy
(() => {
  document.addEventListener("DOMContentLoaded", () => {
    const gallery = document.querySelector("[data-product-gallery]");
    const mainImage = document.querySelector("[data-gallery-main]");
    const thumbs = Array.from(document.querySelectorAll("[data-gallery-thumb]"));
    const prevBtn = document.querySelector("[data-gallery-prev]");
    const nextBtn = document.querySelector("[data-gallery-next]");
    const counterEl = document.querySelector("[data-gallery-counter]");

    const lightbox = document.querySelector("[data-lightbox]");
    const lightboxImg = document.querySelector("[data-lightbox-img]");
    const lightboxMedia = document.querySelector("[data-lightbox-media]");
    const lightboxCaption = document.querySelector("[data-lightbox-caption]");
    const lightboxCounter = document.querySelector("[data-lightbox-counter]");
    const lightboxZoomBtn = document.querySelector("[data-lightbox-zoom]");
    const lightboxPrevBtn = document.querySelector("[data-lightbox-prev]");
    const lightboxNextBtn = document.querySelector("[data-lightbox-next]");

    const images = thumbs.length > 0
      ? thumbs.map((btn) => ({
          src: btn.getAttribute("data-src") || "",
          alt: btn.getAttribute("data-alt") || ""
        }))
      : mainImage
        ? [{ src: mainImage.getAttribute("src") || "", alt: mainImage.getAttribute("alt") || "" }]
        : [];

    let currentIndex = 0;
    let lastFocusedBeforeLightbox = null;
    let isZoomed = false;

    function setZoom(zoomed) {
      isZoomed = Boolean(zoomed);
      if (lightboxMedia) lightboxMedia.classList.toggle("is-zoomed", isZoomed);
      if (lightboxZoomBtn) lightboxZoomBtn.setAttribute("aria-pressed", isZoomed ? "true" : "false");
    }

    function updateActiveImage(index) {
      if (images.length === 0) return;
      currentIndex = ((index % images.length) + images.length) % images.length;
      const active = images[currentIndex];

      if (mainImage) {
        mainImage.src = active.src;
        mainImage.alt = active.alt;
      }

      thumbs.forEach((thumb, idx) => {
        const isCurrent = idx === currentIndex;
        thumb.classList.toggle("is-active", isCurrent);
        thumb.setAttribute("aria-pressed", isCurrent ? "true" : "false");
      });

      const counterText = `${currentIndex + 1} / ${images.length}`;
      if (counterEl) counterEl.textContent = counterText;

      if (lightboxImg) {
        lightboxImg.src = active.src;
        lightboxImg.alt = active.alt;
      }
      if (lightboxCaption) lightboxCaption.textContent = active.alt;
      if (lightboxCounter) lightboxCounter.textContent = counterText;
      setZoom(false);
    }

    function openLightbox() {
      if (!lightbox || images.length === 0) return;
      lastFocusedBeforeLightbox = document.activeElement;
      updateActiveImage(currentIndex);
      lightbox.hidden = false;
      requestAnimationFrame(() => lightbox.classList.add("is-open"));
      document.body.classList.add("no-scroll");

      const closeBtn = lightbox.querySelector(".lightbox-topbar [data-lightbox-close]");
      if (closeBtn) closeBtn.focus();
    }

    function closeLightbox() {
      if (!lightbox || lightbox.hidden) return;
      lightbox.classList.remove("is-open");
      setZoom(false);
      document.body.classList.remove("no-scroll");
      window.setTimeout(() => {
        lightbox.hidden = true;
        if (lastFocusedBeforeLightbox && typeof lastFocusedBeforeLightbox.focus === "function") {
          lastFocusedBeforeLightbox.focus();
        }
      }, 180);
    }

    thumbs.forEach((thumb, idx) => {
      thumb.addEventListener("click", () => updateActiveImage(idx));
    });

    if (prevBtn) prevBtn.addEventListener("click", () => updateActiveImage(currentIndex - 1));
    if (nextBtn) nextBtn.addEventListener("click", () => updateActiveImage(currentIndex + 1));

    document.querySelectorAll("[data-lightbox-open]").forEach((trigger) => {
      trigger.addEventListener("click", openLightbox);
    });

    if (lightbox) {
      lightbox.querySelectorAll("[data-lightbox-close]").forEach((btn) => {
        btn.addEventListener("click", closeLightbox);
      });

      if (lightboxPrevBtn) lightboxPrevBtn.addEventListener("click", () => updateActiveImage(currentIndex - 1));
      if (lightboxNextBtn) lightboxNextBtn.addEventListener("click", () => updateActiveImage(currentIndex + 1));
      if (lightboxZoomBtn) lightboxZoomBtn.addEventListener("click", () => setZoom(!isZoomed));
      if (lightboxImg) lightboxImg.addEventListener("click", () => setZoom(!isZoomed));
    }

    document.addEventListener("keydown", (event) => {
      if (lightbox && !lightbox.hidden) {
        if (event.key === "Escape") {
          event.preventDefault();
          closeLightbox();
        } else if (event.key === "ArrowLeft" && images.length > 1) {
          event.preventDefault();
          updateActiveImage(currentIndex - 1);
        } else if (event.key === "ArrowRight" && images.length > 1) {
          event.preventDefault();
          updateActiveImage(currentIndex + 1);
        }
        return;
      }

      if (gallery && gallery.contains(document.activeElement) && images.length > 1) {
        if (event.key === "ArrowLeft") {
          event.preventDefault();
          updateActiveImage(currentIndex - 1);
        } else if (event.key === "ArrowRight") {
          event.preventDefault();
          updateActiveImage(currentIndex + 1);
        }
      }
    });

    // Quantity Stepper
    const stepper = document.querySelector("[data-qty-stepper]");
    const addToCartBtn = document.querySelector("[data-add-to-cart]");

    if (stepper) {
      const decBtn = stepper.querySelector("[data-qty-dec]");
      const incBtn = stepper.querySelector("[data-qty-inc]");
      const qtyInput = stepper.querySelector("[data-qty-input]");
      const maxStock = Math.max(1, Number(stepper.getAttribute("data-max")) || 1);

      function syncQuantity(rawVal, notifyMax = false) {
        if (!qtyInput || qtyInput.disabled) return;
        let next = parseInt(String(rawVal), 10);
        if (Number.isNaN(next) || next < 1) next = 1;
        if (next > maxStock) {
          next = maxStock;
          if (notifyMax && window.TechStore) {
            window.TechStore.showToast(window.TechStore.t("Js.MaxStockReached"), "info");
          }
        }

        qtyInput.value = String(next);
        if (decBtn) decBtn.disabled = next <= 1;
        if (incBtn) incBtn.disabled = next >= maxStock;
        if (addToCartBtn) addToCartBtn.setAttribute("data-quantity", String(next));
      }

      if (decBtn) {
        decBtn.addEventListener("click", () => syncQuantity((parseInt(qtyInput.value, 10) || 1) - 1));
      }

      if (incBtn) {
        incBtn.addEventListener("click", () => syncQuantity((parseInt(qtyInput.value, 10) || 1) + 1, true));
      }

      if (qtyInput) {
        qtyInput.addEventListener("change", () => syncQuantity(qtyInput.value, true));
        syncQuantity(qtyInput.value, false);
      }
    }

    if (addToCartBtn) {
      addToCartBtn.addEventListener("click", () => {
        if (addToCartBtn.disabled) return;
        const productId = Number(addToCartBtn.getAttribute("data-product-id"));
        const quantity = Math.max(1, Number(addToCartBtn.getAttribute("data-quantity")) || 1);
        document.dispatchEvent(
          new CustomEvent("techstore:add-to-cart", {
            bubbles: true,
            cancelable: true,
            detail: { productId, quantity, button: addToCartBtn }
          })
        );
      });
    }

    // SKU Copy-to-Clipboard
    document.querySelectorAll("[data-copy-sku]").forEach((btn) => {
      btn.addEventListener("click", async () => {
        const sku = btn.getAttribute("data-copy-sku") || "";
        if (!sku) return;

        try {
          if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(sku);
          } else {
            const temp = document.createElement("textarea");
            temp.value = sku;
            temp.style.position = "fixed";
            temp.style.opacity = "0";
            document.body.appendChild(temp);
            temp.select();
            document.execCommand("copy");
            temp.remove();
          }
          if (window.TechStore) {
            window.TechStore.showToast(window.TechStore.t("Product.SkuCopied"), "success");
          }
        } catch {
          // Clipboard access blocked by browser permissions
        }
      });
    });
  });
})();
