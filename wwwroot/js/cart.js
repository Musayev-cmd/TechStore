// Persistent Shopping Cart Controller (DB + Guest Cookie + localStorage Cross-Tab Sync + Slide-Over Drawer + Full Cart Page)
(() => {
  const STORAGE_KEY = "techstore-cart-v1";

  document.addEventListener("DOMContentLoaded", () => {
    if (!window.TechStore) return;

    const { apiFetch, t, formatPrice, showToast, config } = window.TechStore;
    const isAuthenticated = Boolean(config && config.isAuthenticated);
    const ownerMode = isAuthenticated ? "auth" : "guest";

    const badgeEls = Array.from(document.querySelectorAll("[data-cart-badge]"));
    const drawerCountEl = document.querySelector("[data-cart-drawer-count]");
    const drawerShippingStrip = document.querySelector("[data-cart-shipping-strip]");
    const drawerShippingText = document.querySelector("[data-cart-shipping-text]");
    const drawerShippingFill = document.querySelector("[data-cart-shipping-fill]");
    const drawerBody = document.querySelector("[data-cart-drawer-body]");
    const drawerFooter = document.querySelector("[data-cart-drawer-footer]");
    const drawerSubtotalEl = document.querySelector("[data-cart-subtotal]");
    const drawerShippingEl = document.querySelector("[data-cart-shipping]");
    const drawerTotalEl = document.querySelector("[data-cart-total]");

    const cartPageRoot = document.querySelector("[data-cart-page]");
    const cartPageContent = document.querySelector("[data-cart-page-content]");
    const cartClearBtn = document.querySelector("[data-cart-clear]");

    let currentCart = null;
    let isBusy = false;

    function escapeHtml(value) {
      return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
    }

    function readLocalSnapshot() {
      try {
        const raw = window.localStorage.getItem(STORAGE_KEY);
        if (!raw) return null;
        const parsed = JSON.parse(raw);
        if (!parsed || typeof parsed !== "object" || !parsed.cart) return null;
        return parsed;
      } catch {
        return null;
      }
    }

    function saveLocalSnapshot(cart) {
      try {
        const payload = {
          owner: cart.isAuthenticated ? "auth" : "guest",
          culture: (config && config.culture) || "en",
          updatedAt: Date.now(),
          cart
        };
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
      } catch {
        // Ignore storage quota or private browsing errors
      }
    }

    function clearLocalSnapshot() {
      try {
        window.localStorage.removeItem(STORAGE_KEY);
      } catch {
        // Ignore storage errors
      }
    }

    function renderDrawerEmpty() {
      return `
        <div class="cart-empty">
          <div class="cart-empty-icon" aria-hidden="true">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="9" cy="21" r="1"></circle>
              <circle cx="20" cy="21" r="1"></circle>
              <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
            </svg>
          </div>
          <h3 class="cart-empty-title">${escapeHtml(t("Cart.EmptyTitle"))}</h3>
          <p class="cart-empty-desc">${escapeHtml(t("Cart.EmptyDesc"))}</p>
          <a class="btn btn-primary btn-sm" href="/Catalog" data-drawer-close>${escapeHtml(t("Cart.ContinueShopping"))}</a>
        </div>
      `;
    }

    function renderDrawerItems(cart) {
      const rows = cart.items.map((item) => {
        const atMax = Number(item.quantity) >= Number(item.maxStock);
        const unitPrice = item.formattedUnitPrice || formatPrice(item.unitPrice);
        const lineTotal = item.formattedLineTotal || formatPrice(item.lineTotal);
        return `
          <div class="drawer-cart-item" data-cart-item-id="${Number(item.productId)}">
            <a class="drawer-cart-thumb" href="${escapeHtml(item.productUrl)}">
              <img src="${escapeHtml(item.imageUrl)}" alt="${escapeHtml(item.name)}" loading="lazy" />
            </a>
            <div class="drawer-cart-info">
              <div class="drawer-cart-top">
                <a class="drawer-cart-name" href="${escapeHtml(item.productUrl)}">${escapeHtml(item.name)}</a>
                <button type="button"
                        class="drawer-cart-remove"
                        data-cart-remove="${Number(item.productId)}"
                        aria-label="${escapeHtml(t("Cart.Remove"))}"
                        title="${escapeHtml(t("Cart.Remove"))}">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                  </svg>
                </button>
              </div>
              <div class="drawer-cart-meta mono">${escapeHtml(item.brand)} · ${escapeHtml(unitPrice)}</div>
              <div class="drawer-cart-bottom">
                <div class="qty-control qty-control-xs">
                  <button type="button"
                          class="qty-btn"
                          data-cart-qty-dec="${Number(item.productId)}"
                          aria-label="${escapeHtml(t("Product.DecreaseQty"))}">−</button>
                  <span class="qty-value mono">${Number(item.quantity)}</span>
                  <button type="button"
                          class="qty-btn"
                          data-cart-qty-inc="${Number(item.productId)}"
                          ${atMax ? "disabled" : ""}
                          aria-label="${escapeHtml(t("Product.IncreaseQty"))}">+</button>
                </div>
                <strong class="drawer-cart-total mono">${escapeHtml(lineTotal)}</strong>
              </div>
            </div>
          </div>
        `;
      });

      return `<div class="drawer-cart-list">${rows.join("")}</div>`;
    }

    function renderFullCartPage(cart) {
      if (!cartPageRoot || !cartPageContent) return;

      if (cartClearBtn) {
        cartClearBtn.hidden = !cart.items || cart.items.length === 0;
      }

      if (!cart.items || cart.items.length === 0) {
        cartPageContent.innerHTML = `
          <div class="catalog-empty cart-page-empty">
            <div class="catalog-empty-icon" aria-hidden="true">
              <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="9" cy="21" r="1"></circle>
                <circle cx="20" cy="21" r="1"></circle>
                <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
              </svg>
            </div>
            <h2 class="catalog-empty-title">${escapeHtml(t("Cart.EmptyTitle"))}</h2>
            <p class="catalog-empty-desc">${escapeHtml(t("Cart.EmptyDesc"))}</p>
            <a class="btn btn-primary" href="/Catalog">${escapeHtml(t("Cart.ContinueShopping"))}</a>
          </div>
        `;
        return;
      }

      const shippingBannerText = cart.qualifiesForFreeShipping
        ? t("Cart.FreeShippingUnlocked")
        : t("Cart.FreeShippingRemaining", cart.formattedFreeShippingRemaining || formatPrice(cart.freeShippingRemaining));

      const itemsHtml = cart.items.map((item) => {
        const atMax = Number(item.quantity) >= Number(item.maxStock);
        const oldPrice = item.hasDiscount
          ? `<span class="price-old">${escapeHtml(item.formattedOriginalPrice || formatPrice(item.originalPrice))}</span>`
          : "";
        const lowStockBadge = item.stockState === "low_stock"
          ? `<span class="badge badge-warning" style="width:fit-content;margin-top:0.25rem;">${escapeHtml(t("Stock.LowStock", item.maxStock))}</span>`
          : "";

        return `
          <article class="cart-page-item" data-cart-item-id="${Number(item.productId)}">
            <a class="cart-item-thumb" href="${escapeHtml(item.productUrl)}">
              <img src="${escapeHtml(item.imageUrl)}" alt="${escapeHtml(item.name)}" loading="lazy" />
            </a>
            <div class="cart-item-details">
              <div class="cart-item-meta mono">
                <span>${escapeHtml(item.brand)}</span>
                <span>·</span>
                <span>${escapeHtml(item.sku)}</span>
              </div>
              <h2 class="cart-item-title">
                <a href="${escapeHtml(item.productUrl)}">${escapeHtml(item.name)}</a>
              </h2>
              <div class="cart-item-unit mono">
                <span>${escapeHtml(item.formattedUnitPrice || formatPrice(item.unitPrice))}</span>
                ${oldPrice}
              </div>
              ${lowStockBadge}
            </div>
            <div class="cart-item-controls">
              <div class="qty-control qty-control-sm">
                <button type="button"
                        class="qty-btn"
                        data-cart-qty-dec="${Number(item.productId)}"
                        aria-label="${escapeHtml(t("Product.DecreaseQty"))}">−</button>
                <input type="number"
                       class="qty-input mono"
                       value="${Number(item.quantity)}"
                       min="1"
                       max="${Number(item.maxStock)}"
                       inputmode="numeric"
                       data-cart-qty-input="${Number(item.productId)}"
                       aria-label="${escapeHtml(t("Product.Quantity"))}" />
                <button type="button"
                        class="qty-btn"
                        data-cart-qty-inc="${Number(item.productId)}"
                        ${atMax ? "disabled" : ""}
                        aria-label="${escapeHtml(t("Product.IncreaseQty"))}">+</button>
              </div>
              <div class="cart-item-total mono">${escapeHtml(item.formattedLineTotal || formatPrice(item.lineTotal))}</div>
              <button type="button"
                      class="btn btn-ghost btn-sm cart-remove-btn"
                      data-cart-remove="${Number(item.productId)}">
                ${escapeHtml(t("Cart.Remove"))}
              </button>
            </div>
          </article>
        `;
      }).join("");

      const shippingIsFree = Number(cart.shippingAmount) === 0;
      const shippingDisplay = cart.formattedShipping || (shippingIsFree ? t("Cart.FreeShipping") : formatPrice(cart.shippingAmount));

      cartPageContent.innerHTML = `
        <div class="cart-page-grid">
          <div class="cart-page-items">
            <div class="cart-shipping-card ${cart.qualifiesForFreeShipping ? "is-unlocked" : ""}">
              <div class="cart-shipping-card-row">
                <span>${escapeHtml(shippingBannerText)}</span>
                <span class="mono">${Number(cart.freeShippingProgressPercent) || 0}%</span>
              </div>
              <div class="cart-shipping-bar" aria-hidden="true">
                <div class="cart-shipping-fill" style="width: ${Number(cart.freeShippingProgressPercent) || 0}%;"></div>
              </div>
            </div>
            <div class="cart-items-card">${itemsHtml}</div>
          </div>

          <aside class="summary-card">
            <h2 class="summary-card-title">${escapeHtml(t("Cart.OrderSummary"))}</h2>
            <div class="summary-rows">
              <div class="summary-row">
                <span>${escapeHtml(t("Cart.Subtotal"))} (${escapeHtml(t("Cart.ItemsCount", cart.itemCount))})</span>
                <span class="mono">${escapeHtml(cart.formattedSubtotal || formatPrice(cart.subtotal))}</span>
              </div>
              <div class="summary-row">
                <span>${escapeHtml(t("Cart.Shipping"))}</span>
                <span class="mono ${shippingIsFree ? "text-success" : ""}">${escapeHtml(shippingDisplay)}</span>
              </div>
              <div class="summary-row is-total">
                <span>${escapeHtml(t("Cart.Total"))}</span>
                <strong class="mono">${escapeHtml(cart.formattedTotal || formatPrice(cart.total))}</strong>
              </div>
            </div>
            <a class="btn btn-primary btn-lg summary-cta" href="/Checkout">
              ${escapeHtml(t("Cart.Checkout"))} →
            </a>
            <p class="summary-note">${escapeHtml(t("Checkout.SecureNotice"))}</p>
          </aside>
        </div>
      `;
    }

    function renderCart(cart, { persist = true } = {}) {
      if (!cart) return;
      currentCart = cart;
      const count = Number(cart.itemCount) || 0;
      const hasItems = Array.isArray(cart.items) && cart.items.length > 0;

      badgeEls.forEach((badge) => {
        badge.textContent = count > 99 ? "99+" : String(count);
        badge.classList.toggle("is-empty", count <= 0);
      });

      if (drawerCountEl) {
        drawerCountEl.textContent = t("Cart.ItemsCount", count);
        drawerCountEl.hidden = !hasItems;
      }

      if (drawerShippingStrip && drawerShippingText && drawerShippingFill) {
        drawerShippingStrip.hidden = !hasItems;
        drawerShippingStrip.classList.toggle("is-unlocked", Boolean(cart.qualifiesForFreeShipping));
        if (hasItems) {
          drawerShippingText.textContent = cart.qualifiesForFreeShipping
            ? t("Cart.FreeShippingUnlocked")
            : t("Cart.FreeShippingRemaining", cart.formattedFreeShippingRemaining || formatPrice(cart.freeShippingRemaining));
          drawerShippingFill.style.width = `${Math.min(100, Math.max(0, Number(cart.freeShippingProgressPercent) || 0))}%`;
        }
      }

      if (drawerBody) {
        drawerBody.innerHTML = hasItems ? renderDrawerItems(cart) : renderDrawerEmpty();
      }

      if (drawerFooter) {
        drawerFooter.hidden = !hasItems;
      }

      if (drawerSubtotalEl) {
        drawerSubtotalEl.textContent = cart.formattedSubtotal || formatPrice(cart.subtotal);
      }

      if (drawerShippingEl) {
        const isFree = Number(cart.shippingAmount) === 0;
        drawerShippingEl.textContent = cart.formattedShipping || (isFree ? t("Cart.FreeShipping") : formatPrice(cart.shippingAmount));
        drawerShippingEl.classList.toggle("text-success", isFree);
      }

      if (drawerTotalEl) {
        drawerTotalEl.textContent = cart.formattedTotal || formatPrice(cart.total);
      }

      renderFullCartPage(cart);

      if (persist) {
        saveLocalSnapshot(cart);
      }
    }

    function findItemQuantity(productId) {
      if (!currentCart || !Array.isArray(currentCart.items)) return 0;
      const found = currentCart.items.find((i) => Number(i.productId) === Number(productId));
      return found ? Number(found.quantity) : 0;
    }

    async function refresh() {
      try {
        const serverCart = await apiFetch("/api/cart");
        const snapshot = readLocalSnapshot();

        // If a returning guest has localStorage items but no DB guest session cookie yet, restore them once
        if (
          !serverCart.isAuthenticated &&
          !serverCart.hasServerSession &&
          (!serverCart.items || serverCart.items.length === 0) &&
          snapshot &&
          snapshot.owner === "guest" &&
          snapshot.cart &&
          Array.isArray(snapshot.cart.items) &&
          snapshot.cart.items.length > 0
        ) {
          const syncItems = snapshot.cart.items
            .filter((i) => Number(i.productId) > 0 && Number(i.quantity) > 0)
            .map((i) => ({ productId: Number(i.productId), quantity: Number(i.quantity) }));

          if (syncItems.length > 0) {
            const synced = await apiFetch("/api/cart/sync", {
              method: "POST",
              body: JSON.stringify({ items: syncItems })
            });
            if (synced && synced.cart) {
              renderCart(synced.cart);
              return synced.cart;
            }
          }
        }

        renderCart(serverCart);
        if (serverCart.stockNotice) {
          showToast(serverCart.stockNotice, "info");
        }
        return serverCart;
      } catch {
        // Keep hydrated UI if transient network issue occurs
        return currentCart;
      }
    }

    async function addItem(productId, quantity = 1, triggerButton = null, { openDrawer = true } = {}) {
      if (!productId || isBusy) return;
      isBusy = true;
      if (triggerButton) triggerButton.classList.add("is-loading");

      try {
        const payload = await apiFetch("/api/cart/items", {
          method: "POST",
          body: JSON.stringify({ productId: Number(productId), quantity: Math.max(1, Number(quantity) || 1) })
        });

        if (payload && payload.cart) {
          renderCart(payload.cart);
        }

        showToast(
          (payload && payload.message) || t("Js.AddedToCart"),
          payload && payload.clampedToStock ? "info" : "success"
        );

        if (openDrawer && window.TechStoreDrawers) {
          window.TechStoreDrawers.open("cart");
        }
      } catch (error) {
        if (error && error.payload && error.payload.cart) {
          renderCart(error.payload.cart);
        }
        showToast((error && error.message) || t("Cart.ErrorGeneric"), "error");
      } finally {
        isBusy = false;
        if (triggerButton) triggerButton.classList.remove("is-loading");
      }
    }

    async function updateItem(productId, quantity) {
      if (!productId || isBusy) return;
      const nextQty = Math.max(0, parseInt(String(quantity), 10) || 0);
      isBusy = true;

      try {
        const payload = await apiFetch(`/api/cart/items/${Number(productId)}`, {
          method: "PUT",
          body: JSON.stringify({ quantity: nextQty })
        });

        if (payload && payload.cart) {
          renderCart(payload.cart);
        }

        if (payload && payload.clampedToStock) {
          showToast(payload.message || t("Js.MaxStockReached"), "info");
        } else if (nextQty === 0) {
          showToast((payload && payload.message) || t("Cart.ItemRemoved"), "info");
        }
      } catch (error) {
        showToast((error && error.message) || t("Cart.ErrorGeneric"), "error");
        await refresh();
      } finally {
        isBusy = false;
      }
    }

    async function removeItem(productId) {
      if (!productId || isBusy) return;
      isBusy = true;

      try {
        const payload = await apiFetch(`/api/cart/items/${Number(productId)}`, {
          method: "DELETE"
        });
        if (payload && payload.cart) {
          renderCart(payload.cart);
        }
        showToast((payload && payload.message) || t("Cart.ItemRemoved"), "info");
      } catch (error) {
        showToast((error && error.message) || t("Cart.ErrorGeneric"), "error");
      } finally {
        isBusy = false;
      }
    }

    async function clearCart() {
      if (isBusy) return;
      isBusy = true;

      try {
        const payload = await apiFetch("/api/cart", { method: "DELETE" });
        if (payload && payload.cart) {
          renderCart(payload.cart);
        }
        showToast((payload && payload.message) || t("Cart.Cleared"), "info");
      } catch (error) {
        showToast((error && error.message) || t("Cart.ErrorGeneric"), "error");
      } finally {
        isBusy = false;
      }
    }

    // Listen for Product Detail Page add-to-cart events
    document.addEventListener("techstore:add-to-cart", (event) => {
      const detail = event.detail || {};
      if (!detail.productId) return;
      addItem(detail.productId, detail.quantity || 1, detail.button || null, { openDrawer: true });
    });

    // Delegated clicks for quick-add buttons, drawer controls, and full cart page controls
    document.addEventListener("click", (event) => {
      const quickAddBtn = event.target.closest("[data-quick-add-to-cart]");
      if (quickAddBtn && !quickAddBtn.disabled) {
        event.preventDefault();
        const productId = Number(quickAddBtn.getAttribute("data-product-id"));
        if (productId > 0) {
          addItem(productId, 1, quickAddBtn, { openDrawer: true });
        }
        return;
      }

      const decBtn = event.target.closest("[data-cart-qty-dec]");
      if (decBtn) {
        event.preventDefault();
        const productId = Number(decBtn.getAttribute("data-cart-qty-dec"));
        const currentQty = findItemQuantity(productId);
        updateItem(productId, Math.max(0, currentQty - 1));
        return;
      }

      const incBtn = event.target.closest("[data-cart-qty-inc]");
      if (incBtn && !incBtn.disabled) {
        event.preventDefault();
        const productId = Number(incBtn.getAttribute("data-cart-qty-inc"));
        const currentQty = findItemQuantity(productId);
        updateItem(productId, currentQty + 1);
        return;
      }

      const removeBtn = event.target.closest("[data-cart-remove]");
      if (removeBtn) {
        event.preventDefault();
        const productId = Number(removeBtn.getAttribute("data-cart-remove"));
        removeItem(productId);
        return;
      }

      const clearBtn = event.target.closest("[data-cart-clear]");
      if (clearBtn) {
        event.preventDefault();
        clearCart();
      }
    });

    document.addEventListener("change", (event) => {
      const qtyInput = event.target.closest("[data-cart-qty-input]");
      if (!qtyInput) return;
      const productId = Number(qtyInput.getAttribute("data-cart-qty-input"));
      const parsed = parseInt(qtyInput.value, 10);
      if (Number.isNaN(parsed) || parsed < 1) {
        updateItem(productId, 1);
      } else {
        updateItem(productId, parsed);
      }
    });

    // Clear cached localStorage snapshot when signing out
    document.querySelectorAll('form[action="/Account/Logout"]').forEach((form) => {
      form.addEventListener("submit", () => {
        clearLocalSnapshot();
      });
    });

    // Cross-tab cart synchronization via localStorage storage events
    window.addEventListener("storage", (event) => {
      if (event.key !== STORAGE_KEY) return;
      if (!event.newValue) {
        refresh();
        return;
      }

      try {
        const parsed = JSON.parse(event.newValue);
        if (parsed && parsed.cart && parsed.owner === ownerMode) {
          renderCart(parsed.cart, { persist: false });
        }
      } catch {
        // Ignore malformed storage payload
      }
    });

    // Initial hydration: clear snapshot on paid checkout return, else hydrate fast from localStorage then sync with server
    const shouldClearLocal = Boolean(document.querySelector('[data-clear-local-cart="true"]'));
    if (shouldClearLocal) {
      clearLocalSnapshot();
    } else {
      const cached = readLocalSnapshot();
      if (cached && cached.cart && cached.owner === ownerMode && cached.culture === ((config && config.culture) || "en")) {
        renderCart(cached.cart, { persist: false });
      }
    }

    refresh();

    window.TechStoreCart = {
      refresh,
      addItem,
      updateItem,
      removeItem,
      clearCart,
      clearLocalSnapshot
    };
  });
})();
