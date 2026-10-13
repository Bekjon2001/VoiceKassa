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
  { name: "Lag'mon", price: 32000, isAvailable: true, image: "https://images.unsplash.com/photo-1625398407796-82650a8c135f?auto=format&fit=crop&w=700&q=80" },
  { name: "Shashlik", price: 25000, isAvailable: true, image: "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=700&q=80" },
  { name: "Choy", price: 8000, isAvailable: true, image: "https://images.unsplash.com/photo-1544787219-7f47ccb76574?auto=format&fit=crop&w=700&q=80" },
  { name: "Coca Cola", price: 12000, isAvailable: true, image: "https://images.unsplash.com/photo-1554866585-cd94860890b7?auto=format&fit=crop&w=700&q=80" },
];

// Menyuda bosib tanlangan taomlar: nom -> { id (API'dan kelsa ), name }.
// id orqali backendga to'g'ridan-to'g'ri yuboriladi — AI kerak emas.
const selectedMenuItems = new Map();

document.addEventListener("DOMContentLoaded", init);

async function init() {
  if (window.location.pathname.endsWith("/menu.html")) {
    await initMenuPage();
    return;
  }

  const params = new URLSearchParams(window.location.search);
  const businessId = params.get("businessId") || params.get("business");
  const scanBox = document.getElementById("scan-box");
  const tableSection = document.getElementById("table-section");
  const locationNote = document.getElementById("location-note");
  const businessInput = document.getElementById("business-id");
  const restaurantTitle = document.getElementById("restaurant-title");
  const searchBox = document.getElementById("search-box");
  const searchInput = document.getElementById("restaurant-search");
  const searchResults = document.getElementById("search-results");

  // Yo'l 1: QR/skaner — URL'da businessId bo'lsa, to'g'ridan-to'g'ri o'sha
  // restoranga o'tadi (qidiruv ko'rinmaydi, faqat stol tanlanadi).
  if (businessId) {
    businessInput.value = businessId;
    restaurantTitle.textContent = params.get("restaurantName") || `Restoran #${businessId}`;
    scanBox.hidden = true;
    searchBox.hidden = true;
    locationNote.textContent = "Restoran aniqlandi. Stollar ro‘yxati yuklanmoqda...";
    try {
      await loadTables(businessId);
    } catch (error) {
      document.getElementById("form-message").textContent = error.message;
    }
    return;
  }

  // Yo'l 2: QR ishlamagan — nom bo'yicha qidiruv bilan restoranni tanlaydi.
  // Qidiruv vaqtda (har harfda) ishlaydi; natijadan birini bossa stollar ochiladi.
  scanBox.hidden = true;
  searchBox.hidden = false;
  locationNote.textContent = "Restoranni nomidan toping va stolni tanlang.";
  restaurantTitle.textContent = "Restoranni tanlang";

  let searchTimer = null;
  searchInput.addEventListener("input", () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => runRestaurantSearch(searchInput.value.trim()), 250);
  });

  // QR tugmasi: haqiqiy skaner URL'ni (businessId bilan) brauzerda ochadi.
  // Bu yerda kamera-skaner o'rnatilmagan, shuning uchun tugma foydalanuvchini
  // nom bo'yicha qidiruvga yo'naltiradi.
  const qrButton = document.getElementById("qr-button");
  if (qrButton) {
    qrButton.addEventListener("click", () => {
      locationNote.textContent = "QR ishlamasa, quyida restoranni nomidan toping.";
      searchInput.focus();
    });
  }

  // Sahifa ochilganda barcha faol restoranlar ko'rsatiladi (bo'sh so'rov).
  await runRestaurantSearch("");
}

// Nom bo'yicha ochiq qidiruv: Business/public/search (token shart emas).
async function runRestaurantSearch(query) {
  const searchResults = document.getElementById("search-results");
  const message = document.getElementById("form-message");
  searchResults.hidden = false;
  searchResults.innerHTML = `<p class="search-hint">Qidirilmoqda…</p>`;
  try {
    const url = query
      ? `${API_BASE}/Business/public/search?query=${encodeURIComponent(query)}`
      : `${API_BASE}/Business/public/search`;
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
      button.innerHTML = `<strong>${escapeHtml(biz.name)}</strong><small>${escapeHtml(biz.address || "Restoran")}</small>`;
      button.addEventListener("click", () => pickRestaurant(biz));
      searchResults.appendChild(button);
    });
  } catch (error) {
    searchResults.innerHTML = "";
    message.textContent = error.message;
  }
}

// Qidiruvdan tanlangan restoran: sarlavha va stollar yangilanadi.
async function pickRestaurant(biz) {
  const businessInput = document.getElementById("business-id");
  const restaurantTitle = document.getElementById("restaurant-title");
  const locationNote = document.getElementById("location-note");
  const searchResults = document.getElementById("search-results");
  const tableSection = document.getElementById("table-section");

  businessInput.value = biz.id;
  restaurantTitle.textContent = biz.name;
  locationNote.textContent = `${biz.name} tanlandi. Stolni tanlang.`;
  if (searchResults) { searchResults.hidden = true; searchResults.innerHTML = ""; }
  tableSection.hidden = false;
  try {
    await loadTables(biz.id);
    const tableList = document.getElementById("table-list");
    if (tableList && tableList.children.length === 0) {
      locationNote.textContent = `${biz.name} — hozircha stol qo‘shilmagan.`;
    }
  } catch (error) {
    document.getElementById("form-message").textContent = error.message;
  }
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
  const restaurantName = document.getElementById("restaurant-title")?.textContent || "";
  const params = new URLSearchParams({
    businessId: String(businessId),
    tableId: String(table.id),
    tableName: table.name || `Stol ${table.id}`,
  });
  if (restaurantName) params.set("restaurantName", restaurantName);
  window.location.href = `menu.html?${params.toString()}`;
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
  menuList.innerHTML = "";
  selectedMenuItems.clear();
  updateSelectionSummary();
  products.filter((product) => product.isAvailable).forEach((product) => {
    // Button — klaviatura bilan ham bosilsin (Enter/Space), type="submit" emas.
    const item = document.createElement("button");
    item.type = "button";
    item.className = "menu-item";
    item.dataset.name = product.name;
    item.setAttribute("aria-pressed", "false");
    item.innerHTML = `<img src="${product.image || "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=700&q=80"}" alt="${product.name} rasmi"><span class="food-info"><strong>${product.name}</strong><small>Yangi tayyorlangan</small></span><b>${Number(product.price).toLocaleString()} so'm</b><span class="item-check" aria-hidden="true">✓</span>`;
    item.addEventListener("click", () => toggleMenuSelection(product, item));
    menuList.appendChild(item);
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

document.getElementById("entry-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const businessId = document.getElementById("business-id").value;
  const tableId = document.getElementById("table-id").value;
  const typedText = document.getElementById("order-text").value.trim();
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