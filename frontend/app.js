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

// ---------- Bron sessiyasi (kod bilan kirish) ----------
// enter'dan kelgan SESSION_TOKEN localStorage'da saqlanadi (sessionStorage
// emas — tab yopilsa ham qoladi; stol hisobi yopilganda server tomonidan
// bekor qilinadi). Barcha mijoz fetch'lari shu token bilan X-Table-Token
// headerini yuboradi — server faqat o'z stoliga ruxsat beradi.
const TABLE_SESSION_KEY = "vk_table_session";

function saveTableSession(session) {
  try { localStorage.setItem(TABLE_SESSION_KEY, JSON.stringify(session)); } catch { /* ignore */ }
}

function loadTableSession() {
  try {
    const raw = localStorage.getItem(TABLE_SESSION_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch { return null; }
}

function clearTableSession() {
  try { localStorage.removeItem(TABLE_SESSION_KEY); } catch { /* ignore */ }
}

// Mijoz fetch'lari uchun X-Table-Token (sessiya bo'lmasa — bo'sh obyekt).
function tableTokenHeaders() {
  const session = loadTableSession();
  return session && session.token ? { "X-Table-Token": session.token } : {};
}

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

// ---------- tables.html: bron kodi bilan stolga ulanish ----------
// Eski oqim (stollar ro'yxatidan tanlash) o'rniga mijoz 6 raqamli bron
// kodini kiritadi. Server kodni tekshirib SESSION_TOKEN qaytaradi — u
// localStorage'da saqlanib, menu.html'ga o'tkaziladi. Menyu faqat shu
// stolning bo'ladi (barcha so'rovlar X-Table-Token bilan ketadi).
async function initTablesPage() {
  const params = new URLSearchParams(window.location.search);
  const restaurantName = params.get("restaurantName") || "Restoran";
  const restaurantTitle = document.getElementById("restaurant-title");
  const locationNote = document.getElementById("location-note");
  if (restaurantTitle) restaurantTitle.textContent = restaurantName;
  if (locationNote) locationNote.textContent = `${restaurantName} · bron kodingizni kiriting.`;

  // Enter'dan kelgan sessiya bo'lsa — stol allaqachon ulangan, menyuga o'tamiz.
  const existingSession = loadTableSession();
  if (existingSession && existingSession.token) {
    window.location.replace("menu.html");
    return;
  }

  // Faqat raqamlar kiritilsin (6 ta).
  const codeInput = document.getElementById("booking-code");
  codeInput?.addEventListener("input", () => {
    codeInput.value = codeInput.value.replace(/\D/g, "").slice(0, 6);
  });

  document.getElementById("entry-form")
    ?.addEventListener("submit", handleBookingCodeSubmit);
}

async function handleBookingCodeSubmit(event) {
  event.preventDefault();
  const message = document.getElementById("form-message");
  const codeInput = document.getElementById("booking-code");
  const code = (codeInput?.value || "").trim();
  if (code.length !== 6) {
    if (message) message.textContent = "Bron kodi 6 raqamdan iborat bo'lishi kerak.";
    return;
  }
  if (message) message.textContent = "Tekshirilmoqda...";
  try {
    // Controller: [Route("[controller]/[action]")] + [HttpPost("enter")]
    // → /Reservation/Enter/enter
    const response = await fetch(`${API_BASE}/Reservation/Enter/enter`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code }),
    });
    const result = await response.json().catch(() => null);
    if (!response.ok) throw new Error((result && result.error) || "Kod qabul qilinmadi.");
    // Sessiya saqlanadi va menyu sahifasiga o'tamiz.
    saveTableSession({
      token: result.sessionToken,
      reservationId: result.reservationId,
      businessId: result.businessId,
      tableId: result.tableId,
      tableName: result.tableName || "",
    });
    window.location.replace("menu.html");
  } catch (error) {
    if (message) message.textContent = `Ulanib bo'lmadi: ${error.message}`;
  }
}

async function initMenuPage() {
  // Sessiya localStorage'dan o'qiladi (kod kirish sahifasida saqlangan).
  const session = loadTableSession();
  if (!session || !session.token) {
    const locationNote = document.getElementById("location-note");
    if (locationNote) locationNote.textContent = "Avval bron kodingiz bilan kiring.";
    window.location.replace("tables.html");
    return;
  }
  const businessId = String(session.businessId || "");
  const tableId = String(session.tableId || "");
  const tableName = session.tableName || `Stol ${tableId}`;
  const locationNote = document.getElementById("location-note");

  document.getElementById("business-id").value = businessId;
  document.getElementById("table-id").value = tableId;
  const titleEl = document.getElementById("restaurant-title");
  if (titleEl) titleEl.textContent = tableName;
  if (locationNote) locationNote.textContent = `${tableName} tanlandi. Menyudan taom tanlang.`;

  // "Stolni almashtirish" linki — sessiyani tozalab, kod oynasiga qaytaradi.
  // Link FAQAT buyurtma qutisi butunlay bo'sh bo'lganda ko'rinadi
  // (updateSelectionSummary() boshqaradi).
  const backLink = document.getElementById("menu-back-link");
  if (backLink) {
    backLink.addEventListener("click", (event) => {
      // Savatda taom bor yoki buyurtma yuborilgan — almashtirishga ruxsat yo'q.
      if (orderSent || selectedMenuItems.size > 0) {
        event.preventDefault();
        return;
      }
      clearTableSession();
    });
  }

  try {
    await loadMenu(businessId, false);
    document.getElementById("menu-section").hidden = false;
  } catch (error) {
    document.getElementById("form-message").textContent = error.message;
  }

  // Sahifa yangilanganda (refresh) yuborilgan buyurtma cheki yo'qolmasligi
  // uchun — stolning hozirgi faol buyurtmasini serverdan qayta tiklaymiz.
  await restoreActiveOrder(businessId, tableId);
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

// Buyurtma yuborilgach mijoz shu sahifada qolsa — oshxona bekor qilganini
// ko'rishi uchun holatni kuzatamiz (polling). orderSent=true bo'lganda ishlaydi.
let customerOrderId = null;
let customerOrderSnapshot = [];
let customerPollTimer = null;

function stopCustomerPolling() {
  if (customerPollTimer) { clearInterval(customerPollTimer); customerPollTimer = null; }
}

function startCustomerPolling(orderId, initialItems) {
  stopCustomerPolling();
  customerOrderId = orderId;
  customerOrderSnapshot = Array.isArray(initialItems) ? initialItems.map(i => ({
    name: i.productNameSpoken,
    qty: Number(i.quantity),
  })) : [];
  // Har 5 soniyada buyurtma holatini tekshiramiz.
  customerPollTimer = setInterval(pollCustomerOrder, 5000);
}

async function pollCustomerOrder() {
  if (!customerOrderId || !orderSent) { stopCustomerPolling(); return; }
  const message = document.getElementById("form-message");
  const statusBox = document.getElementById("order-status");
  if (!message) return;
  try {
    // Controller: [Route("[controller]/[action]")] + GetById action
    // → /Order/GetById/{orderId} — X-Table-Token bilan (faqat o'z stoli).
    const res = await fetch(`${API_BASE}/Order/GetById/${customerOrderId}`, {
      headers: tableTokenHeaders(),
    });
    if (!res.ok) return; // buyurtma hali ko'rinmasa — jim kutamiz
    const order = await res.json();
    const status = Number(order.status);
    const items = Array.isArray(order.items) ? order.items : [];

    // 1) Butun buyurtma bekor qilingan (Cancelled = 3)
    if (status === 3) {
      stopCustomerPolling();
      hidePayActions(); // bekor qilingan buyurtma to'lab bo'lmaydi
      // Bekor qilingan buyurtmaning taomlar ro'yxati o'chiriladi —
      // faqat ogohlantirish xabari qoladi (customercancel bilan bir xil).
      const receipt = message.querySelector(".receipt");
      if (receipt) receipt.remove();
      if (statusBox) {
        statusBox.hidden = false;
        statusBox.className = "order-status order-status--cancelled";
        statusBox.innerHTML = `⚠️ <strong>Buyurtmangiz oshxona tomonidan bekor qilindi.</strong>`
          + `<br/>Sabab: taom tugagan yoki oshxona qabul qila olmagan bo'lishi mumkin. Iltimos, ofitsiant bilan gaplashing yoki qayta buyurtma bering.`;
      }
      return;
    }

    // 2) Bitta taom o'chirilgan — avvalgi ro'yxatda bor, yangisida yo'q
    const currentNames = new Set(items.map(i => `${i.productNameSpoken}__${i.quantity}`));
    const removed = customerOrderSnapshot.filter(s => !currentNames.has(`${s.name}__${s.qty}`));
    // Miqdori o'zgargan bo'lsa ham (qisman o'chirish) — sodda solishtirish: nom bo'yicha
    const currentNameOnly = new Set(items.map(i => i.productNameSpoken));
    const removedByName = customerOrderSnapshot.filter(s => !currentNameOnly.has(s.name));

    if (removedByName.length > 0) {
      const names = removedByName.map(r => escapeHtml(r.name)).join(", ");
      if (statusBox) {
        statusBox.hidden = false;
        statusBox.className = "order-status order-status--warn";
        statusBox.innerHTML = `⚠️ <strong>Bekor qilingan taom:</strong> ${names}.`
          + `<br/>Bu taom tugagan bo'lishi mumkin — qolgan taomlaringiz tayyorlanmoqda.`
          + (order.totalAmount != null ? `<br/>Yangilangan jami: <strong>${Number(order.totalAmount).toLocaleString("uz-UZ")} so'm</strong>` : "")
          // Oshxona bir taomni o'chirganda mijozdan keyingi qadam so'raladi:
          // buyurtmaga qo'shishmi yoki butunlay bekor qilishmi.
          + `<span class="order-status-ask">Buyurtmaga nimadir qo'shasizmi yoki bekor qilasizmi?</span>`
          + `<span class="order-status-actions">`
          + `<button type="button" class="status-action-btn status-action-btn--add" id="order-add-more">➕ Yana qo'shish</button>`
          + `<button type="button" class="status-action-btn status-action-btn--cancel" id="order-cancel-own">❌ Bekor qilish</button>`
          + `</span>`;
      }
      // Savol tugmalari — har bir xabar qayta chizilganda qayta ulanadi.
      document.getElementById("order-add-more")?.addEventListener("click", startAddMoreItems);
      document.getElementById("order-cancel-own")?.addEventListener("click", cancelOwnOrder);
      // Keyingi polling'da qayta xabar chiqmasligi uchun snapshot'ni yangilaymiz
      customerOrderSnapshot = items.map(i => ({ name: i.productNameSpoken, qty: Number(i.quantity) }));
      // Qabul qilingan chek jadvalini ham yangilab qo'yamiz (qatorlar + jami summa)
      const receiptTable = message.querySelector(".order-items-table tbody");
      if (receiptTable) {
        receiptTable.innerHTML = items.map((i) =>
          `<tr><td class="cell-name">${escapeHtml(i.productNameSpoken)}</td><td class="cell-qty">${i.quantity}</td><td class="cell-sum">${Number(i.lineTotal || 0).toLocaleString("uz-UZ")} so'm</td></tr>`
        ).join("");
      }
      const receiptTotal = message.querySelector(".order-items-table tfoot td:last-child");
      if (receiptTotal && order.totalAmount != null) {
        receiptTotal.textContent = `${Number(order.totalAmount).toLocaleString("uz-UZ")} so'm`;
      }
    } else if (removed.length > 0) {
      customerOrderSnapshot = items.map(i => ({ name: i.productNameSpoken, qty: Number(i.quantity) }));
    }
  } catch { /* tarmoq uzilsa — keyingi siklda urinib ko'ramiz */ }
}

// ---------- Faol buyurtmani tiklash (refresh'da chek saqlanadi) ----------
// Yuborilgan buyurtma ID'si sessionStorage'da saqlanadi (tab yopilsa o'chadi).
// Asosiy manba — serverdagi ochiq buyurtmalar; sessionStorage faqat
// buyurtma bekor qilinganini keyingi ochilishda ko'rsatish uchun kerak.
function activeOrderStorageKey() {
  const b = document.getElementById("business-id")?.value;
  const t = document.getElementById("table-id")?.value;
  return b && t ? `vk_active_order_${b}_${t}` : "";
}

function saveActiveOrderId(id) {
  try { const k = activeOrderStorageKey(); if (k) sessionStorage.setItem(k, String(id)); } catch { /* ignore */ }
}

function readActiveOrderId() {
  try { const k = activeOrderStorageKey(); return k ? sessionStorage.getItem(k) : null; } catch { return null; }
}

function clearActiveOrderId() {
  try { const k = activeOrderStorageKey(); if (k) sessionStorage.removeItem(k); } catch { /* ignore */ }
}

// ---------- To'lov (mijoz buyurtmani to'liq yopishi) ----------
// Ochiq buyurtma ko'rsatilganda #order-pay bloki ochiladi; mijoz Naqd/Karta/
// Onlayn tugmasini bosib /Order/{id}/close orqali buyurtmani yopadi — stol
// bo'shatiladi va qayta buyurtma berish mumkin.
function showPayActions() {
  const box = document.getElementById("order-pay");
  if (box) box.hidden = false;
}

function hidePayActions() {
  const box = document.getElementById("order-pay");
  if (box) box.hidden = true;
}

async function payActiveOrder(paymentType) {
  if (!customerOrderId) return;
  const statusBox = document.getElementById("order-status");
  try {
    // Controller yo'li: [controller]/[action] + [HttpPost("{orderId}/close")]
    // → /Order/Close/{orderId}/close — X-Table-Token bilan.
    const res = await fetch(`${API_BASE}/Order/Close/${encodeURIComponent(customerOrderId)}/close`, {
      method: "POST",
      headers: Object.assign({ "Content-Type": "application/json" }, tableTokenHeaders()),
      body: JSON.stringify({ paymentType: Number(paymentType) }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || "To'lov bo'yicha xatolik.");
    stopCustomerPolling();
    clearActiveOrderId();
    hidePayActions();
    orderSent = false; // stol bo'sh — "Stolni almashtirish" back-linki qaytadi
    updateSelectionSummary();
    if (statusBox) {
      statusBox.hidden = false;
      statusBox.className = "order-status order-status--paid";
      statusBox.innerHTML = `✅ <strong>To'lov qabul qilindi.</strong> Buyurtma yopildi, stol bo'shatildi.`;
    }
    const note = document.querySelector("#form-message .receipt-note");
    if (note) note.textContent = "To'landi ✅";
  } catch (error) {
    if (statusBox) {
      statusBox.hidden = false;
      statusBox.className = "order-status order-status--warn";
      statusBox.textContent = `To'lov amalga oshmadi: ${error.message}`;
    }
  }
}

document.querySelectorAll("#order-pay .pay-btn").forEach((btn) => {
  btn.addEventListener("click", () => payActiveOrder(Number(btn.dataset.pay)));
});

// ---------- Oshxona bir taomni o'chirgandan keyingi savol ----------
// "Yana qo'shish" — mijoz menyuga qaytadi, tanlaydi va yuborganda backend
// CreateFromTextAsync mavjud ochiq buyurtmaga (GetOpenOrderByTableAsync)
// yangi taomlarni QO'SHADI — alohida buyurtma yaratilmaydi.
function startAddMoreItems() {
  const statusBox = document.getElementById("order-status");
  if (statusBox) statusBox.hidden = true; // eski ogohlantirish aralashmasin
  const menuList = document.getElementById("menu-list");
  if (menuList) menuList.scrollIntoView({ behavior: "smooth", block: "start" });
}

// "Bekor qilish" — mijoz o'z buyurtmasini bekor qiladi: /Order/Cancel/{id}/cancel
// (sabab bilan) — stol bo'shatiladi, polling to'xtaydi, back-link qaytadi.
async function cancelOwnOrder() {
  if (!customerOrderId) return;
  if (!window.confirm("Buyurtmangiz bekor qilinsinmi? Taomlar tayyorlanishni to'xtatadi.")) return;
  const statusBox = document.getElementById("order-status");
  try {
    // Controller: [HttpPost("{orderId:long}/cancel")] + action "Cancel"
    // → /Order/Cancel/{orderId}/cancel — X-Table-Token bilan.
    const res = await fetch(`${API_BASE}/Order/Cancel/${encodeURIComponent(customerOrderId)}/cancel`, {
      method: "POST",
      headers: Object.assign({ "Content-Type": "application/json" }, tableTokenHeaders()),
      body: JSON.stringify({ reason: "Mijoz bekor qildi" }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || "Bekor qilib bo'lmadi.");
    stopCustomerPolling();
    clearActiveOrderId(); // stol bo'sh — yangi buyurtma uchun ID tozalanadi
    hidePayActions();
    orderSent = false; // "Stolni almashtirish" back-linki qaytadi
    updateSelectionSummary();
    // Chekdagi taomlar ro'yxati ENDI O'CHIRILADI — aks holda bekor qilingan
    // buyurtmaning ro'yxati joyida qolib ketardi. #form-message ichiga faqat
    // qisqa xabar qoladi; to'liq xabar #order-status'da ko'rsatiladi.
    const message = document.getElementById("form-message");
    if (message) message.textContent = "Buyurtmangiz bekor qilindi ❌";
    if (statusBox) {
      statusBox.hidden = false;
      statusBox.className = "order-status order-status--cancelled";
      statusBox.innerHTML = `ℹ️ <strong>Buyurtmangiz bekor qilindi.</strong> Stol bo'shatildi — yangi buyurtma bera olasiz.`;
    }
  } catch (error) {
    if (statusBox) {
      statusBox.hidden = false;
      statusBox.className = "order-status order-status--warn";
      statusBox.textContent = `Bekor qilib bo'lmadi: ${error.message}`;
    }
  }
}

// Buyurtmani chek ko'rinishida ko'rsatadi — yuborilgandan KEYIN va sahifa
// qayta yuklanganda (restoreActiveOrder) ikkala holatda ham shu funksiya ishlaydi.
function showCustomerReceipt(order) {
  const items = Array.isArray(order.items) ? order.items : [];
  // Stol band: "Stolni almashtirish" back-linki yashiriladi, menyu tanlovi tozalanadi.
  orderSent = true;
  // Chek qayta chizilganda eski ogohlantirish/savol (taom o'chirilgani va h.k.)
  // qolmasligi uchun — faqat chek ko'rinadi. To'lov/bekorlik xabarlari alohida
  // paytlarda (payActiveOrder / cancelOwnOrder) qayta chiziladi.
  const statusBox = document.getElementById("order-status");
  if (statusBox) statusBox.hidden = true;
  selectedMenuItems.clear();
  document.querySelectorAll(".menu-item.selected").forEach((el) => {
    el.classList.remove("selected");
    el.setAttribute("aria-pressed", "false");
  });
  updateSelectionSummary();

  const message = document.getElementById("form-message");
  if (!message) return;
  const total = Number(order.totalAmount || 0);
  const rows = items.map((i) =>
    `<tr><td class="cell-name">${escapeHtml(i.productNameSpoken)}</td><td class="cell-qty">${i.quantity}</td><td class="cell-sum">${Number(i.lineTotal || 0).toLocaleString("uz-UZ")} so'm</td></tr>`
  ).join("");
  // Oshxona buyurtmani allaqachon olgan bo'lsa (InProgress=1) — boshqacha yozuv.
  const statusText = Number(order.status) === 1
    ? "Oshxonada tayyorlanmoqda."
    : "Oshxonaga yuborildi.";
  message.innerHTML = `<div class="receipt"><strong>Buyurtma qabul qilindi. №${order.id}</strong>`
    + (rows ? `<table class="order-items-table"><thead><tr><th>Nomi</th><th>Soni</th><th>Sumasi</th></tr></thead><tbody>${rows}</tbody>`
      + `<tfoot><tr><td colspan="2">Jami</td><td>${total.toLocaleString("uz-UZ")} so'm</td></tr></tfoot></table>` : "")
    + `<p class="receipt-note">${statusText}</p></div>`;
  // Ochiq buyurtmada mijoz to'liq to'lov qila oladi (Naqd/Karta/Onlayn).
  if (Number(order.status) === 0 || Number(order.status) === 1) showPayActions();
  else hidePayActions();
}

// Sahifa yuklanganda stolning faol buyurtmasini topadi va chekni qayta chizadi.
async function restoreActiveOrder(businessId, tableId) {
  // 1) Serverdagi ochiq (Open/InProgress) buyurtmalar orqali shu stolni topamiz —
  //    bu asosiy yo'l: refresh yoki yangi tab ochilsa ham ishlaydi.
  let active = null;
  try {
    const res = await fetch(`${API_BASE}/Order/GetOpen/open/${encodeURIComponent(businessId)}`);
    if (res.ok) {
      const orders = await res.json();
      active = (Array.isArray(orders) ? orders : [])
        .find(o => String(o.tableId) === String(tableId)) || null;
    }
  } catch { /* tarmoq xatosi — 2-bosqichga o'tamiz */ }

  if (active) {
    showCustomerReceipt(active);
    saveActiveOrderId(active.id);
    startCustomerPolling(active.id, active.items);
    return;
  }

  // 2) Serverda ochiq buyurtma yo'q — ehtimol u bekor qilingan yoki yopilgan.
  //    sessionStorage'dagi oxirgi buyurtma orqali holatni ko'rsatamiz.
  const savedId = readActiveOrderId();
  if (!savedId) return;
  try {
    const res = await fetch(`${API_BASE}/Order/GetById/${encodeURIComponent(savedId)}`);
    if (!res.ok) { clearActiveOrderId(); return; }
    const order = await res.json();
    const status = Number(order.status);
    if (status === 3) {
      // Oshxona bekor qilgan — mijoz xabardor qilinadi (chek emas, ogohlantirish).
      const statusBox = document.getElementById("order-status");
      if (statusBox) {
        statusBox.hidden = false;
        statusBox.className = "order-status order-status--cancelled";
        statusBox.innerHTML = `⚠️ <strong>Buyurtmangiz oshxona tomonidan bekor qilindi.</strong>`
          + `<br/>Sabab: taom tugagan yoki oshxona qabul qila olmagan bo'lishi mumkin. Iltimos, ofitsiant bilan gaplashing yoki qayta buyurtma bering.`;
      }
      // buyurtma yopilgan/emas — keyingi ochilishda ham shu xabar ko'rinsin,
      // shuning uchun ID saqlanadi (yangi buyurtma yuborilganda almashtiriladi).
      hidePayActions();
    } else if (status === 0 || status === 1) {
      // Ochiq buyurtma (GetOpen topmagan bo'lsa — masalan tarmoq chigalligi) — chekni tiklaymiz.
      showCustomerReceipt(order);
      saveActiveOrderId(order.id);
      startCustomerPolling(order.id, order.items);
    } else {
      // Completed (yopilgan/to'langan) — eski yozuvni tozalaymiz.
      clearActiveOrderId();
    }
  } catch { /* ignore */ }
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
    // ro'yxatga qaytib, boshqa stolni tanlay olmaydi. Chek chiziladi va
    // buyurtma ID'si sessionStorage'ga saqlanadi — refresh'da qayta tiklanadi.
    showCustomerReceipt(result);
    saveActiveOrderId(result.id);
    // Oshxona bekor qilsa — mijoz shu sahifada xabar oladi (5s da bir tekshiruv).
    startCustomerPolling(result.id, result.items);
  } catch (error) {
    message.textContent = `Buyurtma yuborilmadi: ${error.message}`;
  }
});