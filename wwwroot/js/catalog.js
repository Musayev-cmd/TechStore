// Async Hardware Catalog Controller (Live Search, Filters, Sorting, Pagination, Shimmer Skeletons, URL Sync)
(() => {
  document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-catalog-root]");
    if (!root || !window.TechStore) return;

    const { apiFetch, t, formatPrice } = window.TechStore;

    const form = root.querySelector("[data-catalog-form]");
    const searchInput = root.querySelector("[data-filter-search]");
    const searchClearBtn = root.querySelector("[data-search-clear]");
    const categoriesContainer = root.querySelector("[data-filter-categories]");
    const brandsContainer = root.querySelector("[data-filter-brands]");
    const minPriceInput = root.querySelector("[data-filter-min-price]");
    const maxPriceInput = root.querySelector("[data-filter-max-price]");
    const inStockInput = root.querySelector("[data-filter-instock]");
    const dealsInput = root.querySelector("[data-filter-deals]");
    const sortSelect = root.querySelector("[data-filter-sort]");
    const resultsRegion = root.querySelector("[data-catalog-results]");
    const resultsCountEl = root.querySelector("[data-results-count]");
    const activeFiltersEl = root.querySelector("[data-active-filters]");
    const paginationEl = root.querySelector("[data-catalog-pagination]");
    const mobileFilterCountEl = root.querySelector("[data-mobile-filter-count]");
    const sidebarClearBtn = root.querySelector(".filter-sidebar-actions [data-clear-filters]");
    const breadcrumbsEl = document.querySelector("[data-catalog-breadcrumbs]");
    const headerSearchInput = document.querySelector(".header-search-input");

    let initialState = {};
    try {
      initialState = JSON.parse(root.getAttribute("data-initial-state") || "{}");
    } catch {
      initialState = {};
    }

    const state = {
      search: initialState.search || "",
      category: initialState.category || "",
      categoryName: initialState.categoryName || "",
      brands: Array.isArray(initialState.brands) ? [...initialState.brands] : [],
      minPrice: initialState.minPrice != null ? String(initialState.minPrice) : "",
      maxPrice: initialState.maxPrice != null ? String(initialState.maxPrice) : "",
      inStock: Boolean(initialState.inStock),
      deals: Boolean(initialState.deals),
      featured: Boolean(initialState.featured),
      sort: initialState.sort || "featured",
      page: Number(initialState.page) || 1,
      pageSize: Number(initialState.pageSize) || 12
    };

    let activeAbortController = null;
    let searchTimer = null;
    let priceTimer = null;
    const MIN_SKELETON_MS = 140;

    function escapeHtml(value) {
      return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
    }

    function parseNullableNumber(raw) {
      const trimmed = String(raw ?? "").trim();
      if (!trimmed) return "";
      const parsed = Number(trimmed);
      if (!Number.isFinite(parsed) || parsed < 0) return "";
      return String(parsed);
    }

    function buildQueryParams(targetState = state) {
      const params = new URLSearchParams();
      if (targetState.search.trim()) params.set("q", targetState.search.trim());
      if (targetState.category) params.set("category", targetState.category);
      if (targetState.brands.length > 0) params.set("brand", targetState.brands.join(","));
      if (targetState.minPrice !== "") params.set("minPrice", targetState.minPrice);
      if (targetState.maxPrice !== "") params.set("maxPrice", targetState.maxPrice);
      if (targetState.inStock) params.set("inStock", "true");
      if (targetState.deals) params.set("deals", "true");
      if (targetState.featured) params.set("featured", "true");
      if (targetState.sort && targetState.sort !== "featured") params.set("sort", targetState.sort);
      if (targetState.page > 1) params.set("page", String(targetState.page));
      if (targetState.pageSize && targetState.pageSize !== 12) params.set("pageSize", String(targetState.pageSize));
      return params;
    }

    function buildPageUrl(pageNum) {
      const params = buildQueryParams({ ...state, page: pageNum });
      const qs = params.toString();
      return qs ? `/Catalog?${qs}` : "/Catalog";
    }

    function buildApiUrl() {
      const params = buildQueryParams(state);
      params.set("page", String(state.page));
      params.set("pageSize", String(state.pageSize));
      return `/api/products?${params.toString()}`;
    }

    function countActiveFilters() {
      let count = 0;
      if (state.search.trim()) count += 1;
      if (state.category) count += 1;
      count += state.brands.length;
      if (state.minPrice !== "" || state.maxPrice !== "") count += 1;
      if (state.inStock) count += 1;
      if (state.deals) count += 1;
      if (state.featured) count += 1;
      return count;
    }

    function renderSkeletons() {
      const count = Math.min(state.pageSize, 8);
      const cards = [];
      for (let i = 0; i < count; i += 1) {
        cards.push(`
          <div class="skeleton-card" aria-hidden="true">
            <div class="skeleton-top">
              <div class="skeleton skeleton-tag"></div>
              <div class="skeleton skeleton-tag" style="width:42px;"></div>
            </div>
            <div class="skeleton skeleton-media"></div>
            <div class="skeleton-body">
              <div class="skeleton skeleton-line is-sm"></div>
              <div class="skeleton skeleton-line is-lg"></div>
              <div class="skeleton skeleton-line is-md"></div>
              <div class="skeleton skeleton-price"></div>
            </div>
          </div>
        `);
      }

      resultsRegion.setAttribute("aria-busy", "true");
      resultsRegion.innerHTML = `
        <span class="sr-only">${escapeHtml(t("Catalog.LoadingResults"))}</span>
        <div class="product-grid catalog-grid" data-catalog-grid>${cards.join("")}</div>
      `;
    }

    function renderStockBadge(item) {
      if (item.stockState === "out_of_stock") {
        return `<span class="badge badge-danger"><span class="status-dot" aria-hidden="true"></span>${escapeHtml(t("Stock.OutOfStock"))}</span>`;
      }
      if (item.stockState === "low_stock") {
        return `<span class="badge badge-warning"><span class="status-dot" aria-hidden="true"></span>${escapeHtml(t("Stock.LowStock", item.stockQuantity))}</span>`;
      }
      return `<span class="badge badge-success"><span class="status-dot" aria-hidden="true"></span>${escapeHtml(t("Stock.InStock"))}</span>`;
    }

    function renderProductCard(item) {
      const discountBadge = item.hasDiscount
        ? `<span class="badge badge-accent">${escapeHtml(t("Product.SavePercent", item.discountPercent))}</span>`
        : "";

      const oldPrice = item.hasDiscount
        ? `<span class="price-old">${escapeHtml(item.formattedPrice || formatPrice(item.price))}</span>`
        : "";

      const currentPrice = item.formattedEffectivePrice || formatPrice(item.effectivePrice);

      const inStock = Number(item.stockQuantity) > 0;

      return `
        <article class="product-card is-entering" data-product-id="${Number(item.id)}">
          <div class="product-card-top">
            <div class="product-card-badges">
              ${renderStockBadge(item)}
              ${discountBadge}
            </div>
          </div>
          <a class="product-card-media" href="${escapeHtml(item.url)}">
            <img src="${escapeHtml(item.imageUrl)}" alt="${escapeHtml(item.imageAlt || item.name)}" loading="lazy" />
          </a>
          <div class="product-card-body">
            <div class="product-card-meta">
              <span>${escapeHtml(item.brand)}</span>
              <span>${escapeHtml(item.categoryName)}</span>
            </div>
            <h3 class="product-card-title">
              <a href="${escapeHtml(item.url)}">${escapeHtml(item.name)}</a>
            </h3>
            <p class="product-card-desc">${escapeHtml(item.shortDescription)}</p>
            <div class="product-card-footer">
              <div class="price-stack">
                ${oldPrice}
                <span class="price-current">${escapeHtml(currentPrice)}</span>
              </div>
              <div class="product-card-actions">
                <a class="btn btn-secondary btn-sm" href="${escapeHtml(item.url)}">
                  ${escapeHtml(t("Catalog.ViewDetails"))}
                </a>
                <button type="button"
                        class="btn-icon btn-card-cart"
                        data-quick-add-to-cart
                        data-product-id="${Number(item.id)}"
                        aria-label="${escapeHtml(t("Cart.QuickAdd"))}"
                        title="${escapeHtml(inStock ? t("Cart.QuickAdd") : t("Stock.OutOfStock"))}"
                        ${inStock ? "" : "disabled"}>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <circle cx="9" cy="21" r="1"></circle>
                    <circle cx="20" cy="21" r="1"></circle>
                    <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
                  </svg>
                </button>
              </div>
            </div>
          </div>
        </article>
      `;
    }

    function renderEmptyState() {
      return `
        <div class="catalog-empty" data-catalog-empty>
          <div class="catalog-empty-icon" aria-hidden="true">
            <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
              <line x1="8" y1="11" x2="14" y2="11"></line>
            </svg>
          </div>
          <h2 class="catalog-empty-title">${escapeHtml(t("Catalog.NoResultsTitle"))}</h2>
          <p class="catalog-empty-desc">${escapeHtml(t("Catalog.NoResultsDesc"))}</p>
          <button type="button" class="btn btn-primary btn-sm" data-clear-filters>
            ${escapeHtml(t("Catalog.ClearFilters"))}
          </button>
        </div>
      `;
    }

    function renderErrorState(message) {
      resultsRegion.setAttribute("aria-busy", "false");
      resultsRegion.innerHTML = `
        <div class="catalog-empty">
          <h2 class="catalog-empty-title">${escapeHtml(message || t("Js.NetworkError"))}</h2>
          <button type="button" class="btn btn-primary btn-sm" data-catalog-retry>
            ${escapeHtml(t("Catalog.Retry"))}
          </button>
        </div>
      `;
    }

    function renderResults(payload) {
      resultsRegion.setAttribute("aria-busy", "false");

      if (!payload.items || payload.items.length === 0) {
        resultsRegion.innerHTML = renderEmptyState();
      } else {
        const html = payload.items.map(renderProductCard).join("");
        resultsRegion.innerHTML = `<div class="product-grid catalog-grid" data-catalog-grid>${html}</div>`;
      }

      if (resultsCountEl) {
        resultsCountEl.textContent = t("Catalog.ResultsCount", payload.totalCount);
      }

      updateActiveFilterChips(payload.activeFilters);
      updateFacets(payload.facets, payload.activeFilters);
      renderPagination(payload);
      updateFilterBadges();
    }

    function updateFilterBadges() {
      const activeCount = countActiveFilters();
      if (mobileFilterCountEl) {
        mobileFilterCountEl.textContent = String(activeCount);
        mobileFilterCountEl.classList.toggle("is-empty", activeCount === 0);
      }
      if (sidebarClearBtn) {
        sidebarClearBtn.hidden = activeCount === 0;
      }
      if (searchClearBtn) {
        searchClearBtn.hidden = !state.search.trim();
      }
    }

    function updateActiveFilterChips(activeFilters) {
      if (!activeFiltersEl) return;

      const chips = [];
      const searchVal = (activeFilters && activeFilters.search) || state.search.trim();
      const catSlug = (activeFilters && activeFilters.category) || state.category;
      const catName = (activeFilters && activeFilters.categoryName) || state.categoryName || catSlug;
      const brands = (activeFilters && activeFilters.brands) || state.brands;
      const minP = activeFilters && activeFilters.minPrice != null ? activeFilters.minPrice : (state.minPrice !== "" ? Number(state.minPrice) : null);
      const maxP = activeFilters && activeFilters.maxPrice != null ? activeFilters.maxPrice : (state.maxPrice !== "" ? Number(state.maxPrice) : null);

      if (searchVal) {
        chips.push(`
          <button type="button" class="filter-chip" data-remove-filter="search" aria-label="${escapeHtml(t("Catalog.RemoveFilter"))}">
            <span>${escapeHtml(t("Catalog.SearchChip", searchVal))}</span>
            <span class="filter-chip-x" aria-hidden="true">×</span>
          </button>
        `);
      }

      if (catSlug) {
        chips.push(`
          <button type="button" class="filter-chip" data-remove-filter="category" aria-label="${escapeHtml(t("Catalog.RemoveFilter"))}">
            <span>${escapeHtml(catName)}</span>
            <span class="filter-chip-x" aria-hidden="true">×</span>
          </button>
        `);
      }

      brands.forEach((brand) => {
        chips.push(`
          <button type="button" class="filter-chip" data-remove-filter="brand" data-filter-value="${escapeHtml(brand)}" aria-label="${escapeHtml(t("Catalog.RemoveFilter"))}">
            <span>${escapeHtml(brand)}</span>
            <span class="filter-chip-x" aria-hidden="true">×</span>
          </button>
        `);
      });

      if (minP != null || maxP != null) {
        let priceLabel = "";
        if (minP != null && maxP != null) {
          priceLabel = t("Catalog.PriceRangeChip", formatPrice(minP), formatPrice(maxP));
        } else if (minP != null) {
          priceLabel = t("Catalog.PriceMinChip", formatPrice(minP));
        } else {
          priceLabel = t("Catalog.PriceMaxChip", formatPrice(maxP));
        }
        chips.push(`
          <button type="button" class="filter-chip" data-remove-filter="price" aria-label="${escapeHtml(t("Catalog.RemoveFilter"))}">
            <span>${escapeHtml(priceLabel)}</span>
            <span class="filter-chip-x" aria-hidden="true">×</span>
          </button>
        `);
      }

      if (state.inStock) {
        chips.push(`
          <button type="button" class="filter-chip" data-remove-filter="inStock" aria-label="${escapeHtml(t("Catalog.RemoveFilter"))}">
            <span>${escapeHtml(t("Catalog.InStockOnly"))}</span>
            <span class="filter-chip-x" aria-hidden="true">×</span>
          </button>
        `);
      }

      if (state.deals) {
        chips.push(`
          <button type="button" class="filter-chip" data-remove-filter="deals" aria-label="${escapeHtml(t("Catalog.RemoveFilter"))}">
            <span>${escapeHtml(t("Catalog.DealsOnly"))}</span>
            <span class="filter-chip-x" aria-hidden="true">×</span>
          </button>
        `);
      }

      if (chips.length > 0) {
        chips.push(`
          <button type="button" class="filter-chip-clear" data-clear-filters>
            ${escapeHtml(t("Catalog.ClearFilters"))}
          </button>
        `);
        activeFiltersEl.innerHTML = chips.join("");
        activeFiltersEl.hidden = false;
      } else {
        activeFiltersEl.innerHTML = "";
        activeFiltersEl.hidden = true;
      }
    }

    function updateFacets(facets, activeFilters) {
      if (!facets) return;

      if (categoriesContainer && Array.isArray(facets.categories)) {
        const totalCatCount = facets.categories.reduce((sum, c) => sum + (Number(c.productCount) || 0), 0);
        const allCountEl = categoriesContainer.querySelector("[data-category-all-count]");
        if (allCountEl) allCountEl.textContent = String(totalCatCount);

        facets.categories.forEach((cat) => {
          const el = categoriesContainer.querySelector(`[data-category-count="${CSS.escape(cat.slug)}"]`);
          if (el) el.textContent = String(cat.productCount);
        });

        categoriesContainer.querySelectorAll(".filter-option").forEach((label) => {
          const input = label.querySelector("input[type='radio']");
          if (input) {
            const selected = input.value === state.category;
            input.checked = selected;
            label.classList.toggle("is-selected", selected);
          }
        });
      }

      if (brandsContainer && Array.isArray(facets.brands)) {
        const selectedLower = new Set(state.brands.map((b) => b.toLowerCase()));
        brandsContainer.innerHTML = facets.brands
          .map((brand) => {
            const isChecked = selectedLower.has(brand.name.toLowerCase());
            return `
              <label class="filter-check ${isChecked ? "is-selected" : ""}">
                <input type="checkbox" name="brand" value="${escapeHtml(brand.name)}" ${isChecked ? "checked" : ""} data-filter-brand />
                <span class="filter-check-box" aria-hidden="true"></span>
                <span class="filter-option-label">${escapeHtml(brand.name)}</span>
                <span class="filter-option-count mono">${Number(brand.productCount)}</span>
              </label>
            `;
          })
          .join("");
      }

      if (facets.priceBounds) {
        if (minPriceInput && facets.priceBounds.min > 0) {
          minPriceInput.placeholder = String(Math.floor(facets.priceBounds.min));
        }
        if (maxPriceInput && facets.priceBounds.max > 0) {
          maxPriceInput.placeholder = String(Math.ceil(facets.priceBounds.max));
        }
      }

      if (activeFilters) {
        state.categoryName = activeFilters.categoryName || "";
        if (breadcrumbsEl) {
          const homeLabel = breadcrumbsEl.getAttribute("data-home-label") || "";
          const catalogLabel = breadcrumbsEl.getAttribute("data-catalog-label") || "";
          if (state.categoryName) {
            breadcrumbsEl.innerHTML = `
              <a href="/">${escapeHtml(homeLabel)}</a>
              <span class="breadcrumb-sep" aria-hidden="true">/</span>
              <a href="/Catalog">${escapeHtml(catalogLabel)}</a>
              <span class="breadcrumb-sep" aria-hidden="true">/</span>
              <span aria-current="page">${escapeHtml(state.categoryName)}</span>
            `;
          } else {
            breadcrumbsEl.innerHTML = `
              <a href="/">${escapeHtml(homeLabel)}</a>
              <span class="breadcrumb-sep" aria-hidden="true">/</span>
              <span aria-current="page">${escapeHtml(catalogLabel)}</span>
            `;
          }
        }
      }
    }

    function buildPageSequence(currentPage, totalPages) {
      if (totalPages <= 7) {
        return Array.from({ length: totalPages }, (_, i) => i + 1);
      }

      const pages = [1];
      const start = Math.max(2, currentPage - 1);
      const end = Math.min(totalPages - 1, currentPage + 1);

      if (start > 2) pages.push("...");
      for (let p = start; p <= end; p += 1) pages.push(p);
      if (end < totalPages - 1) pages.push("...");
      pages.push(totalPages);
      return pages;
    }

    function renderPagination(payload) {
      if (!paginationEl) return;

      const totalPages = Number(payload.totalPages) || 1;
      const currentPage = Number(payload.page) || 1;

      if (totalPages <= 1) {
        paginationEl.innerHTML = "";
        paginationEl.hidden = true;
        return;
      }

      const sequence = buildPageSequence(currentPage, totalPages);
      const pagesHtml = sequence
        .map((entry) => {
          if (entry === "...") {
            return `<span class="pagination-ellipsis" aria-hidden="true">…</span>`;
          }
          const isActive = entry === currentPage;
          return `
            <a class="pagination-page ${isActive ? "is-active" : ""}"
               href="${escapeHtml(buildPageUrl(entry))}"
               data-page="${entry}"
               ${isActive ? 'aria-current="page"' : ""}>
              ${entry}
            </a>
          `;
        })
        .join("");

      const prevHref = payload.hasPrevious ? buildPageUrl(currentPage - 1) : "#";
      const nextHref = payload.hasNext ? buildPageUrl(currentPage + 1) : "#";

      paginationEl.innerHTML = `
        <a class="btn btn-secondary btn-sm pagination-btn ${payload.hasPrevious ? "" : "is-disabled"}"
           href="${escapeHtml(prevHref)}"
           data-page="${currentPage - 1}"
           aria-disabled="${payload.hasPrevious ? "false" : "true"}">
          ← ${escapeHtml(t("Catalog.Previous"))}
        </a>
        <div class="pagination-pages">${pagesHtml}</div>
        <a class="btn btn-secondary btn-sm pagination-btn ${payload.hasNext ? "" : "is-disabled"}"
           href="${escapeHtml(nextHref)}"
           data-page="${currentPage + 1}"
           aria-disabled="${payload.hasNext ? "false" : "true"}">
          ${escapeHtml(t("Catalog.Next"))} →
        </a>
      `;
      paginationEl.hidden = false;
    }

    function syncInputsFromState() {
      if (searchInput) searchInput.value = state.search;
      if (headerSearchInput) headerSearchInput.value = state.search;
      if (minPriceInput) minPriceInput.value = state.minPrice;
      if (maxPriceInput) maxPriceInput.value = state.maxPrice;
      if (inStockInput) inStockInput.checked = state.inStock;
      if (dealsInput) dealsInput.checked = state.deals;
      if (sortSelect) sortSelect.value = state.sort;

      if (categoriesContainer) {
        categoriesContainer.querySelectorAll(".filter-option").forEach((label) => {
          const input = label.querySelector("input[type='radio']");
          if (input) {
            const selected = input.value === state.category;
            input.checked = selected;
            label.classList.toggle("is-selected", selected);
          }
        });
      }

      if (brandsContainer) {
        const lower = new Set(state.brands.map((b) => b.toLowerCase()));
        brandsContainer.querySelectorAll(".filter-check").forEach((label) => {
          const input = label.querySelector("input[type='checkbox']");
          if (input) {
            const checked = lower.has(input.value.toLowerCase());
            input.checked = checked;
            label.classList.toggle("is-selected", checked);
          }
        });
      }

      updateFilterBadges();
    }

    async function fetchCatalog({ pushHistory = true, scrollTop = false } = {}) {
      if (activeAbortController) {
        activeAbortController.abort();
      }

      const controller = new AbortController();
      activeAbortController = controller;

      const targetUrl = buildPageUrl(state.page);
      if (pushHistory) {
        window.history.pushState({ ...state }, "", targetUrl);
      }

      const startedAt = performance.now();
      renderSkeletons();

      try {
        const payload = await apiFetch(buildApiUrl(), { signal: controller.signal });
        const elapsed = performance.now() - startedAt;
        if (elapsed < MIN_SKELETON_MS) {
          await new Promise((resolve) => window.setTimeout(resolve, MIN_SKELETON_MS - elapsed));
        }

        if (controller.signal.aborted) return;

        state.page = Number(payload.page) || 1;
        state.sort = payload.sort || "featured";
        if (payload.activeFilters) {
          state.brands = Array.isArray(payload.activeFilters.brands) ? payload.activeFilters.brands : [];
          state.minPrice = payload.activeFilters.minPrice != null ? String(payload.activeFilters.minPrice) : "";
          state.maxPrice = payload.activeFilters.maxPrice != null ? String(payload.activeFilters.maxPrice) : "";
        }

        syncInputsFromState();
        renderResults(payload);

        if (scrollTop) {
          const toolbar = root.querySelector(".catalog-toolbar");
          if (toolbar) {
            toolbar.scrollIntoView({ behavior: "smooth", block: "start" });
          }
        }
      } catch (error) {
        if (error && error.name === "AbortError") return;
        renderErrorState(error && error.message ? error.message : t("Js.NetworkError"));
      }
    }

    function clearAllFilters() {
      state.search = "";
      state.category = "";
      state.categoryName = "";
      state.brands = [];
      state.minPrice = "";
      state.maxPrice = "";
      state.inStock = false;
      state.deals = false;
      state.featured = false;
      state.page = 1;
      syncInputsFromState();
      fetchCatalog({ pushHistory: true });
    }

    function readStateFromLocation() {
      const params = new URLSearchParams(window.location.search);
      state.search = (params.get("q") || params.get("search") || "").trim();
      state.category = (params.get("category") || "").trim().toLowerCase();
      const rawBrands = params.getAll("brand").flatMap((v) => v.split(",")).map((v) => v.trim()).filter(Boolean);
      state.brands = Array.from(new Set(rawBrands));
      state.minPrice = parseNullableNumber(params.get("minPrice"));
      state.maxPrice = parseNullableNumber(params.get("maxPrice"));
      state.inStock = params.get("inStock") === "true";
      state.deals = params.get("deals") === "true";
      state.featured = params.get("featured") === "true";
      state.sort = params.get("sort") || "featured";
      state.page = Math.max(1, Number(params.get("page")) || 1);
    }

    // Event bindings
    if (form) {
      form.addEventListener("submit", (event) => {
        event.preventDefault();
        window.clearTimeout(searchTimer);
        window.clearTimeout(priceTimer);
        if (searchInput) state.search = searchInput.value.trim();
        if (minPriceInput) state.minPrice = parseNullableNumber(minPriceInput.value);
        if (maxPriceInput) state.maxPrice = parseNullableNumber(maxPriceInput.value);
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      });
    }

    if (searchInput) {
      searchInput.addEventListener("input", () => {
        state.search = searchInput.value.trim();
        updateFilterBadges();
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => {
          state.page = 1;
          fetchCatalog({ pushHistory: true });
        }, 280);
      });
    }

    if (searchClearBtn) {
      searchClearBtn.addEventListener("click", () => {
        window.clearTimeout(searchTimer);
        state.search = "";
        if (searchInput) {
          searchInput.value = "";
          searchInput.focus();
        }
        state.page = 1;
        updateFilterBadges();
        fetchCatalog({ pushHistory: true });
      });
    }

    if (categoriesContainer) {
      categoriesContainer.addEventListener("change", (event) => {
        const input = event.target.closest("[data-filter-category]");
        if (!input) return;
        state.category = input.value;
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      });
    }

    if (brandsContainer) {
      brandsContainer.addEventListener("change", (event) => {
        const input = event.target.closest("[data-filter-brand]");
        if (!input) return;
        const checked = Array.from(brandsContainer.querySelectorAll("[data-filter-brand]:checked"))
          .map((el) => el.value);
        state.brands = checked;
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      });
    }

    const schedulePriceFetch = () => {
      window.clearTimeout(priceTimer);
      priceTimer = window.setTimeout(() => {
        state.minPrice = minPriceInput ? parseNullableNumber(minPriceInput.value) : "";
        state.maxPrice = maxPriceInput ? parseNullableNumber(maxPriceInput.value) : "";
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      }, 450);
    };

    if (minPriceInput) minPriceInput.addEventListener("input", schedulePriceFetch);
    if (maxPriceInput) maxPriceInput.addEventListener("input", schedulePriceFetch);

    if (inStockInput) {
      inStockInput.addEventListener("change", () => {
        state.inStock = inStockInput.checked;
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      });
    }

    if (dealsInput) {
      dealsInput.addEventListener("change", () => {
        state.deals = dealsInput.checked;
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      });
    }

    if (sortSelect) {
      sortSelect.addEventListener("change", () => {
        state.sort = sortSelect.value || "featured";
        state.page = 1;
        fetchCatalog({ pushHistory: true });
      });
    }

    // Delegated clicks for clear buttons, active chips, retry button, and pagination
    root.addEventListener("click", (event) => {
      const clearBtn = event.target.closest("[data-clear-filters]");
      if (clearBtn) {
        event.preventDefault();
        clearAllFilters();
        return;
      }

      const retryBtn = event.target.closest("[data-catalog-retry]");
      if (retryBtn) {
        event.preventDefault();
        fetchCatalog({ pushHistory: false });
        return;
      }

      const removeChip = event.target.closest("[data-remove-filter]");
      if (removeChip) {
        event.preventDefault();
        const type = removeChip.getAttribute("data-remove-filter");
        const val = removeChip.getAttribute("data-filter-value") || "";

        if (type === "search") state.search = "";
        else if (type === "category") {
          state.category = "";
          state.categoryName = "";
        } else if (type === "brand") {
          state.brands = state.brands.filter((b) => b.toLowerCase() !== val.toLowerCase());
        } else if (type === "price") {
          state.minPrice = "";
          state.maxPrice = "";
        } else if (type === "inStock") state.inStock = false;
        else if (type === "deals") state.deals = false;

        state.page = 1;
        syncInputsFromState();
        fetchCatalog({ pushHistory: true });
        return;
      }

      const pageLink = event.target.closest("[data-page]");
      if (pageLink) {
        event.preventDefault();
        if (pageLink.getAttribute("aria-disabled") === "true" || pageLink.classList.contains("is-disabled")) {
          return;
        }
        const targetPage = Number(pageLink.getAttribute("data-page"));
        if (!Number.isInteger(targetPage) || targetPage < 1 || targetPage === state.page) return;
        state.page = targetPage;
        fetchCatalog({ pushHistory: true, scrollTop: true });
      }
    });

    window.addEventListener("popstate", () => {
      readStateFromLocation();
      syncInputsFromState();
      fetchCatalog({ pushHistory: false });
    });

    // Store initial history state cleanly
    window.history.replaceState({ ...state }, "", window.location.href);
  });
})();
