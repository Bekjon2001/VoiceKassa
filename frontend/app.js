// Sahifa qaysi manzildan ochildi — API ham shu joyga ulanadi.
const API_BASE = location.protocol.startsWith("http")
  ? `${location.protocol}//${location.host}`
  : "http://localhost:55983";
// Demo rejim uchun haqiqiy biznes: "Milliy Taomlar Restorani" (bazada id=44).
// Bu biznesda stollar va mahsulotlar seed qilingan, shuning uchun buyurtma
// zanjiri (oshxona -> start -> close) haqiqiy ma'lumot bilan ishlaydi.
const DEFAULT_BUSINESS_ID = "44";
const DEFAULT_RESTAURANT_NAME = "Milliy Taomlar Restorani";
const DEFAULT_TABLES = [
  { id: 1, name: "Stol 1", status: 0, image: "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=500&q=80" },
  { id: 2, name: "Stol 2", status: 0, image: "https://images.unsplash.com/photo-1552566626-52f8b828add9?auto=format&fit=crop&w=500&q=80" },
  { id: 3, name: "Stol 3", status: 1, image: "https://images.unsplash.com/photo-1514933651103-005eec06c04b?auto=format&fit=crop&w=500&q=80" },
  { id: 4, name: "Stol 4", status: 0, image: "https://images.unsplash.com/photo-1515003197210-e0cd71810b5f?auto=format&fit=crop&w=500&q=80" },
  { id: 5, name: "VIP stol", status: 0, image: "https://images.unsplash.com/photo-1550966871-3ed3cdb5ed0c?auto=format&fit=crop&w=500&q=80" },
];
const DEFAULT_PRODUCTS = [
  { name: "Lag'mon", price: 32000, isAvailable: true, kind: "food", image: "https://images.unsplash.com/photo-1625398407796-82650a8c135f?auto=format&fit=crop&w=700&q=80" },
  { name: "Shashlik", price: 25000, isAvailable: true, kind: "food", image: "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=700&q=80" },
  { name: "Choy", price: 8000, isAvailable: true, kind: "drink", image: "https://images.unsplash.com/photo-1544787219-7f47ccb76574?auto=format&fit=crop&w=700&q=80" },
  { name: "Coca Cola", price: 12000, isAvailable: true, kind: "drink", image: "https://images.unsplash.com/photo-1554866585-cd94860890b7?auto=format&fit=crop&w=700&q=80" },
];

// Menyu bo'limlari va ularning ko'rsatilish tartibi. Mahsulot turi (kind)
// quyidagi tartibda aniqlanadi: ichimlik -> meva -> shirinlik -> ovqat.
const MENU_CATEGORIES = [
  { key: "food", title: "Ovqatlar" },
  { key: "drink", title: "Ichimliklar" },
  { key: "fruit", title: "Mevalar" },
  { key: "dessert", title: "Shirinliklar" },
];

// Mahsulot turini aniqlaydi. API'dan kelgan mahsulotda bu "aliases" ro'yxatida
// saqlanadi (admin paneldagi mantiq bilan bir xil), demo mahsulotlarda "kind".
function productKind(product) {
  if (product.kind) return product.kind;
  const aliases = Array.isArray(product.aliases) ? product.aliases : [];
  if (aliases.includes("drink")) return "drink";
  if (aliases.includes("fruit")) return "fruit";
  if (aliases.includes("dessert")) return "dessert";
  return "food";
}

// Menyuda bosib tanlangan taomlar: nom -> { id (API'dan kelsa ), name }.
// id orqali backendga to'g'ridan-to'g'ri yuboriladi — AI kerak emas.
const selectedMenuItems = new Map();
// Buyurtma oshxonaga yuborilgach "Stolni almashtirish" back-link yashiriladi.
// Stol band bo'lgani uchun buyurtma yuborilgandan KEYIN ro'yxatga qaytib bo'lmaydi.
// Buyurtma hali yuborilmagan bo'lsa (taom tanlangan bo'lsa ham) — link ko'rinadi.
let orderSent = false;

document.addEventListener("DOMContentLoaded", init);

// ---------- Kun/Tun (light/dark) rejimi ----------
// Mijoz sahifalari (index/tables/menu) admin panellar bilan BIR XIL
// localStorage kalitidan (vk_theme) foydalanadi — bir paneldan boshqasiga
// o'tganda tanlangan rejim saqlanib qoladi.
function applyTheme(theme) {
  document.documentElement.dataset.theme = theme === "dark" ? "dark" : "light";
  try { localStorage.setItem("vk_theme", theme === "dark" ? "dark" : "light"); } catch { /* ignore */ }
  const btn = document.getElementById("theme-toggle");
  if (btn) btn.textContent = theme === "dark" ? "☀️" : "🌙";
}

function initTheme() {
  // head'dagi inline skript allaqachon rejimni qo'llagan — tugmani moslaymiz.
  applyTheme(document.documentElement.dataset.theme);
  const btn = document.getElementById("theme-toggle");
  if (btn) {
    btn.addEventListener("click", () => {
      applyTheme(document.documentElement.dataset.theme === "dark" ? "light" : "dark");
    });
  }
}
initTheme();

async function init() {
  // Har bir qadam — alohida sahifa:
  //   index.html  → restoran tanlash (qidiruv)
  //   tables.html → stol tanlash
  //   menu.html   → menyu + buyurtma
  const path = window.location.pathname;
  if (path.endsWith("/menu.html")) {
    await initMenuPage();
    return;
  }
  if (path.endsWith("/tables.html")) {
    await initTablesPage();
    return;
  }

  // ---------- index.html: restoran tanlash ----------
  const params = new URLSearchParams(window.location.search);
  const businessId = params.get("businessId") || params.get("business");
  const scanBox = document.getElementById("scan-box");
  const searchBox = document.getElementById("search-box");
  const locationNote = document.getElementById("location-note");
  const businessInput = document.getElementById("business-id");
  const restaurantTitle = document.getElementById("restaurant-title");
  const searchInput = document.getElementById("restaurant-search");
  const searchResults = document.getElementById("search-results");

  // QR/skaner — URL'da businessId bo'lsa, to'g'ridan-to'g'ri stol tanlash
  // sahifasiga o'tkazib yuboramiz (qidiruv ko'rinmaydi).
  if (businessId) {
    if (businessInput) businessInput.value = businessId;
    window.location.href = `tables.html?businessId=${encodeURIComponent(businessId)}`
      + (params.get("restaurantName") ? `&restaurantName=${encodeURIComponent(params.get("restaurantName"))}` : "");
    return;
  }

  // Yo'l 2: QR ishlamagan — nom bo'yicha qidiruv bilan restoranni tanlaydi.
  if (scanBox) scanBox.hidden = true;
  if (searchBox) searchBox.hidden = false;
  if (locationNote) locationNote.textContent = "Restoranni tanlang.";
  if (restaurantTitle) restaurantTitle.textContent = "Restoranni tanlang";

  let searchTimer = null;
  if (searchInput) {
    searchInput.addEventListener("input", () => {
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => runRestaurantSearch(searchInput.value.trim()), 250);
    });
    // Maydonga bosilganda (fokuslanganda) ro'yxat ochiladi — bo'sh bo'lsa hamma
    // faol restoranlar, yozilgan bo'lsa shu nom bo'yicha filtr chiqadi.
    searchInput.addEventListener("focus", () => {
      clearTimeout(searchTimer);
      runRestaurantSearch(searchInput.value.trim());
    });
  }

  const qrButton = document.getElementById("qr-button");
  if (qrButton) {
    qrButton.addEventListener("click", () => {
      if (locationNote) locationNote.textContent = "QR ishlamasa, quyida restoranni nomidan toping.";
      if (searchInput) searchInput.focus();
    });
  }

  // Sahifa yuklanganda ro'yxat yopiq turadi — faqat foydalanuvchi qidiruv
  // maydoniga bosganida (focus) hamma restoranlar ro'yxati ochiladi.
}

// Nom bo'yicha ochiq qidiruv: Business/SearchPublicBusinesses/public/search (token shart emas).
// Diqqat: controller [controller]/[action] konvensiyasida, shuning uchun action
// nomi (SearchPublicBusinesses) URL'da saqlanishi shart.
async function runRestaurantSearch(query) {
  const searchResults = document.getElementById("search-results");
  const message = document.getElementById("form-message");
  searchResults.hidden = false;
  searchResults.innerHTML = `<p class="search-hint">Qidirilmoqda…</p>`;
  try {
    const url = query
      ? `${API_BASE}/Business/SearchPublicBusinesses/public/search?query=${encodeURIComponent(query)}`
      : `${API_BASE}/Business/SearchPublicBusinesses/public/search`;
    const response = await fetch(url);
    if (!response.ok) throw new Error("Restoranlarni yuklab bo‘lmadi.");
    const businesses = await response.json();
    if (!businesses || businesses.length === 0) {
      searchResults.innerHTML = `<p class="search-hint">Bunday nom topilmadi.</p>`;
      return;
    }
    searchResults.innerHTML = "";
    businesses.forEach((biz) => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "search-result";
      const initial = escapeHtml((biz.name || "R").trim().charAt(0).toUpperCase());
      button.innerHTML =
        `<span class="search-result-icon">${initial}</span>` +
        `<span class="search-result-body">` +
        `<strong>${escapeHtml(biz.name)}</strong>` +
        `<small>${escapeHtml(biz.address || "Restoran")}</small>` +
        `</span>` +
        `<span class="search-result-arrow" aria-hidden="true">→</span>`;
      button.addEventListener("click", () => pickRestaurant(biz));
      searchResults.appendChild(button);
    });
  } catch (error) {
    searchResults.innerHTML = "";
    message.textContent = error.message;
  }
}

// Qidiruvdan tanlangan restoran: stol tanlash sahifasiga (tables.html) o'tadi.
async function pickRestaurant(biz) {
  const searchResults = document.getElementById("search-results");
  if (searchResults) { searchResults.hidden = true; searchResults.innerHTML = ""; }
  const params = new URLSearchParams({
    businessId: String(biz.id),
    restaurantName: biz.name || `Restoran #${biz.id}`,
  });
  window.location.href = `tables.html?${params.toString()}`;
}

async function loadTables(businessId) {
  const tableList = document.getElementById("table-list");
  const tableSection = document.getElementById("table-section");
  const response = await fetch(`${API_BASE}/Business/GetTables/${encodeURIComponent(businessId)}/tables`);

  if (!response.ok) {
    throw new Error("Stollar ro‘yxatini yuklab bo‘lmadi.");
  }

  const tables = await response.json();
  tableList.innerHTML = "";
  tables.forEach((table) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "table-button";
    button.innerHTML = `<img src="${table.image || "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=500&q=80"}" alt="${table.name || `Stol ${table.id}`} rasmi"><span class="table-info"><strong>${table.name || `Stol ${table.id}`}</strong><small>${table.status === 0 ? "Bo'sh" : "Band"}</small></span>`;
    button.disabled = table.status !== 0;
    button.addEventListener("click", () => selectTable(businessId, table, button));
    tableList.appendChild(button);
  });
  tableSection.querySelector("h3").textContent = "Stolni tanlang";
}

function renderTables(tables, businessId) {
  const tableList = document.getElementById("table-list");
  const tableSection = document.getElementById("table-section");
  tableList.innerHTML = "";
  tables.forEach((table) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "table-button";
    button.innerHTML = `<img src="https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=500&q=80" alt="${table.name || `Stol ${table.id}`} rasmi"><span class="table-info"><strong>${table.name || `Stol ${table.id}`}</strong><small>${table.status === 0 ? "Bo'sh" : "Band"}</small></span>`;
    button.disabled = table.status !== 0;
    button.addEventListener("click", () => selectTable(businessId, table, button));
    tableList.appendChild(button);
  });
  tableSection.querySelector("h3").textContent = "Stolni tanlang";
}

async function selectTable(businessId, table, button) {
  // Restoran nomi URL'dan olinadi (index.html'da emas, shu sahifada saqlanmagan).
  const restaurantName = new URLSearchParams(window.location.search).get("restaurantName") || "";
  const params = new URLSearchParams({
    businessId: String(businessId),
    tableId: String(table.id),
    tableName: table.name || `Stol ${table.id}`,
  });
  if (restaurantName) params.set("restaurantName", restaurantName);
  window.location.href = `menu.html?${params.toString()}`;
}

// ---------- tables.html: stol tanlash (alohida sahifa) ----------
async function initTablesPage() {
  const params = new URLSearchParams(window.location.search);
  const businessId = params.get("businessId");
  const restaurantName = params.get("restaurantName") || "Restoran";
  const restaurantTitle = document.getElementById("restaurant-title");
  const locationNote = document.getElementById("location-note");
  const businessInput = document.getElementById("business-id");

  if (restaurantTitle) restaurantTitle.textContent = restaurantName;
  if (businessInput) businessInput.value = businessId || "";
  if (locationNote) locationNote.textContent = `${restaurantName} · stolni tanlang.`;

  // Restoran tanlanmagan bo'lsa — orqaga, tanlash sahifasiga qaytaramiz.
  if (!businessId) {
    if (locationNote) locationNote.textContent = "Avval restoranni tanlang.";
    window.location.href = "index.html";
    return;
  }

  try {
    await loadTables(businessId);
    const tableList = document.getElementById("table-list");
    if (tableList && tableList.children.length === 0 && locationNote) {
      locationNote.textContent = `${restaurantName} — hozircha stol qo‘shilmagan.`;
    }
  } catch (error) {
    document.getElementById("form-message").textContent = error.message;
  }
}

async function initMenuPage() {
  const params = new URLSearchParams(window.location.search);
  const businessId = params.get("businessId");
  const tableId = params.get("tableId");
  const tableName = params.get("tableName") || `Stol ${tableId || ""}`;
  const locationNote = document.getElementById("location-note");

  if (!businessId || !tableId) {
    locationNote.textContent = "Avval stol sahifasidan stolni tanlang.";
    return;
  }

  document.getElementById("business-id").value = businessId;
  document.getElementById("table-id").value = tableId;
  const restaurantName = params.get("restaurantName") || "Restoran";
  const titleEl = document.getElementById("restaurant-title");
  if (titleEl) titleEl.textContent = restaurantName;
  locationNote.textContent = `${restaurantName} · ${tableName} tanlandi.`;

  // "Stolni almashtirish" linki (yuqoridagi topbar'da) — shu restoranni stol
  // tanlash sahifasiga qaytaradi. Bu link doim ko'rinadi: foydalanuvchi
  // xohlagan vaqtda (taom tanlaganda ham, buyurtma yuborilgach ham) stolni
  // almashtirib, ro'yxatga qayta olishi mumkin.
  const backLink = document.getElementById("menu-back-link");
  if (backLink) {
    backLink.href = `tables.html?businessId=${encodeURIComponent(businessId)}&restaurantName=${encodeURIComponent(restaurantName)}`;
  }

  try {
    await loadMenu(businessId, params.get("demo") === "1");
    document.getElementById("menu-section").hidden = false;
  } catch (error) {
    document.getElementById("form-message").textContent = error.message;
  }
}

async function loadMenu(businessId, isDemo = false) {
  const menuList = document.getElementById("menu-list");
  if (isDemo) {
    renderMenu(DEFAULT_PRODUCTS);
    return;
  }
  const response = await fetch(`${API_BASE}/Product/GetByBusiness?businessId=${encodeURIComponent(businessId)}`);

  if (!response.ok) {
    throw new Error("Menyuni yuklab bo‘lmadi.");
  }

  renderMenu(await response.json());
}

function renderMenu(products) {
  const menuList = document.getElementById("menu-list");
  const tabsWrap = document.getElementById("menu-tabs");
  menuList.innerHTML = "";
  if (tabsWrap) tabsWrap.innerHTML = "";
  selectedMenuItems.clear();
  updateSelectionSummary();

  // Faqat mavjud (sotiladigan) mahsulotlarni turi bo'yicha bo'limlarga ajratamiz.
  const available = products.filter((product) => product.isAvailable);
  const groups = new Map();
  available.forEach((product) => {
    const key = productKind(product);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(product);
  });

  // Faqat bo'sh bo'lmagan bo'limlarni olamiz (tartib MENU_CATEGORIES bo'yicha).
  const present = MENU_CATEGORIES.filter(({ key }) => (groups.get(key) || []).length > 0);
  if (present.length === 0) return;

  // Har bir bo'lim uchun: tepada tab tugmasi + panel (yashirin, faqat biri ochiq).
  present.forEach(({ key, title }, index) => {
    // Tab tugmasi — bosilganda shu bo'lim ochiladi.
    if (tabsWrap) {
      const tab = document.createElement("button");
      tab.type = "button";
      tab.className = "menu-tab";
      tab.dataset.kind = key;
      tab.setAttribute("role", "tab");
      tab.setAttribute("aria-selected", index === 0 ? "true" : "false");
      tab.textContent = title;
      tab.addEventListener("click", () => setActiveMenuTab(key));
      tabsWrap.appendChild(tab);
    }

    // Panel (bo'lim) — mahsulotlar ro'yxati; faqat birinchisi ochiq.
    const panel = document.createElement("div");
    panel.className = "menu-panel";
    panel.dataset.kind = key;
    panel.hidden = index !== 0;

    const list = document.createElement("div");
    list.className = "menu-list";
    (groups.get(key) || []).forEach((product) => {
      // Button — klaviatura bilan ham bosilsin (Enter/Space), type="submit" emas.
      const item = document.createElement("button");
      item.type = "button";
      item.className = "menu-item";
      item.dataset.name = product.name;
      item.setAttribute("aria-pressed", "false");
      item.innerHTML = `<img src="${product.image || "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=700&q=80"}" alt="${product.name} rasmi"><span class="food-info"><strong>${product.name}</strong><small>Yangi tayyorlangan</small></span><b>${Number(product.price).toLocaleString()} so'm</b><span class="item-check" aria-hidden="true">✓</span>`;
      item.addEventListener("click", () => toggleMenuSelection(product, item));
      list.appendChild(item);
    });
    panel.appendChild(list);
    menuList.appendChild(panel);
  });
}

// Faqat tanlangan bo'lim (tab) panelini ko'rsatadi. Butun menyu qayta
// chizilmaydi, shuning uchun tanlangan mahsulotlar tab almashganda saqlanadi.
function setActiveMenuTab(key) {
  document.querySelectorAll("#menu-tabs .menu-tab").forEach((tab) => {
    const active = tab.dataset.kind === key;
    tab.classList.toggle("active", active);
    tab.setAttribute("aria-selected", active ? "true" : "false");
  });
  document.querySelectorAll("#menu-list .menu-panel").forEach((panel) => {
    panel.hidden = panel.dataset.kind !== key;
  });
}

function toggleMenuSelection(product, item) {
  // Menyudan har bosishda soni bittaga oshadi — jadval "Soni" ustuni shundan to'ladi.
  const existing = selectedMenuItems.get(product.name);
  if (existing) existing.quantity += 1;
  else selectedMenuItems.set(product.name, { id: product.id ?? null, name: product.name, price: Number(product.price) || 0, quantity: 1 });
  item.classList.add("selected");
  item.setAttribute("aria-pressed", "true");
  updateSelectionSummary();
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (ch) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  }[ch]));
}

function updateSelectionSummary() {
  const wrap = document.getElementById("selection-note");
  if (!wrap) return;
  const body = document.getElementById("selection-note-body");
  const totalCell = document.getElementById("selection-note-total");
  const entries = [...selectedMenuItems.values()];
  wrap.hidden = entries.length === 0;
  if (!body) return;

  // "Stolni almashtirish" back-linki FAQAT buyurtma qutisi butunlay bo'sh
  // bo'lganda ko'rinadi. Quyidagi ikki holatda yashiriladi (foydalanuvchi
  // stolni almashtira olmaydi):
  //   1) Buyurtma qutida tanlangan taom bor (hali yuborilmagan bo'lsa ham).
  //   2) Buyurtma oshxonaga yuborilgan — stol band bo'lgani uchun.
  const backLink = document.getElementById("menu-back-link");
  if (backLink) backLink.hidden = orderSent || entries.length > 0;

  let total = 0;
  body.innerHTML = entries.map((p) => {
    const lineTotal = (Number(p.price) || 0) * p.quantity;
    total += lineTotal;
    return `<tr>
      <td class="cell-name">${escapeHtml(p.name)}</td>
      <td class="cell-qty">
        <button type="button" class="qty-btn" data-act="dec" data-name="${escapeHtml(p.name)}" aria-label="${escapeHtml(p.name)} sonini kamaytirish">−</button>
        <span class="qty-value">${p.quantity}</span>
        <button type="button" class="qty-btn" data-act="inc" data-name="${escapeHtml(p.name)}" aria-label="${escapeHtml(p.name)} sonini oshirish">+</button>
      </td>
      <td class="cell-sum">${lineTotal.toLocaleString("uz-UZ")} so'm</td>
    </tr>`;
  }).join("");
  if (totalCell) totalCell.textContent = `${total.toLocaleString("uz-UZ")} so'm`;

  // Jadval ichidagi +/− tugmalari soni boshqaradi.
  body.querySelectorAll(".qty-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      const name = btn.dataset.name;
      const cur = selectedMenuItems.get(name);
      if (!cur) return;
      cur.quantity += btn.dataset.act === "inc" ? 1 : -1;
      if (cur.quantity <= 0) selectedMenuItems.delete(name);
      syncMenuSelectedState(name);
      updateSelectionSummary();
    });
  });
}

// Menyu ro'yxatidagi "selected" holatini tanlov jadvali bilan sinxronlaydi.
function syncMenuSelectedState(name) {
  const still = selectedMenuItems.has(name);
  document.querySelectorAll(".menu-item").forEach((el) => {
    if (el.dataset.name === name) {
      el.classList.toggle("selected", still);
      el.setAttribute("aria-pressed", String(still));
    }
  });
}

// Tanlangan taomlar backend uchun strukturali ro'yxat: [{ productId, name, quantity }].
function composeSelectionItems() {
  return [...selectedMenuItems.values()].map((p) => ({
    productId: p.id ?? null,
    name: p.name,
    quantity: p.quantity,
  }));
}

document.getElementById("entry-form")?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const orderTextEl = document.getElementById("order-text");
  const tableIdEl = document.getElementById("table-id");
  const businessIdEl = document.getElementById("business-id");
  // Bu handler faqat buyurtma sahifasida (menu.html) ishlaydi. Restoran/stol
  // tanlash sahifalarida order-text/table-id bo'lmagani uchun chiqib ketamiz.
  if (!orderTextEl || !tableIdEl) return;
  const businessId = businessIdEl ? businessIdEl.value : "";
  const tableId = tableIdEl.value;
  const typedText = orderTextEl.value.trim();
  // Menyudan tanlanganlar strukturali (id bilan), qo'lda yozilgan qism — matn.
  const items = composeSelectionItems();
  const message = document.getElementById("form-message");

  if (!businessId || !tableId) {
    message.textContent = "Avval QR orqali kirib, stolni tanlang.";
    return;
  }
  if (!typedText && items.length === 0) {
    message.textContent = "Buyurtma matnini kiriting yoki menyudan taom tanlang.";
    return;
  }
  message.textContent = "Buyurtma yuborilmoqda...";
  try {
    const response = await fetch(`${API_BASE}/Order/CreateFromVoice/voice`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        businessId: Number(businessId),
        tableId: Number(tableId),
        transcriptText: typedText,
        items,
      }),
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || "Buyurtma yuborilmadi.");
    // Buyurtma oshxonaga yuborildi — bu stol endi band. "Stolni almashtirish"
    // back-linki (yuqoridagi topbar'da) endi yashiriladi, ya'ni foydalanuvchi
    // ro'yxatga qaytib, boshqa stolni tanlay olmaydi.
    orderSent = true;
    // Tanlov tozalanadi: menyu belgilari o'chadi, jadval qutidan yo'qoladi.
    selectedMenuItems.clear();
    document.querySelectorAll(".menu-item.selected").forEach((el) => {
      el.classList.remove("selected");
      el.setAttribute("aria-pressed", "false");
    });
    updateSelectionSummary();
    // Buyurtma qabul qilinganda uning tarkibi jadval ko'rinishida ko'rsatiladi.
    const total = Number(result.totalAmount || 0);
    const rows = (Array.isArray(result.items) ? result.items : []).map((i) =>
      `<tr><td class="cell-name">${escapeHtml(i.productNameSpoken)}</td><td class="cell-qty">${i.quantity}</td><td class="cell-sum">${Number(i.lineTotal || 0).toLocaleString("uz-UZ")} so'm</td></tr>`
    ).join("");
    message.innerHTML = `<div class="receipt"><strong>Buyurtma qabul qilindi. №${result.id}</strong>`
      + (rows ? `<table class="order-items-table"><thead><tr><th>Nomi</th><th>Soni</th><th>Sumasi</th></tr></thead><tbody>${rows}</tbody>`
        + `<tfoot><tr><td colspan="2">Jami</td><td>${total.toLocaleString("uz-UZ")} so'm</td></tr></tfoot></table>` : "")
      + `<p class="receipt-note">Oshxonaga yuborildi.</p></div>`;
  } catch (error) {
    message.textContent = `Buyurtma yuborilmadi: ${error.message}`;
  }
});