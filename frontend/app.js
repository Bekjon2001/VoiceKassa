const API_BASE = "http://localhost:55983";
const DEFAULT_BUSINESS_ID = "1";
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

  if (!businessId) {
    businessInput.value = DEFAULT_BUSINESS_ID;
    restaurantTitle.textContent = DEFAULT_RESTAURANT_NAME;
    scanBox.hidden = true;
    locationNote.textContent = "Demo restoran. Stolni tanlang.";
    renderTables(DEFAULT_TABLES, DEFAULT_BUSINESS_ID);
    tableSection.hidden = false;
    return;
  }

  businessInput.value = businessId;
  restaurantTitle.textContent = params.get("restaurantName") || `Restoran #${businessId}`;
  scanBox.hidden = true;
  locationNote.textContent = "Restoran aniqlandi. Stollar ro‘yxati yuklanmoqda...";
  try {
    await loadTables(businessId);
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
  const demoParam = window.location.search ? "" : "&demo=1";
  window.location.href = `menu.html?businessId=${encodeURIComponent(businessId)}&tableId=${encodeURIComponent(table.id)}&tableName=${encodeURIComponent(table.name || `Stol ${table.id}`)}${demoParam}`;
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
  locationNote.textContent = `${tableName} tanlandi.`;

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
  products.filter((product) => product.isAvailable).forEach((product) => {
    const item = document.createElement("div");
    item.className = "menu-item";
    item.innerHTML = `<img src="${product.image || "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=700&q=80"}" alt="${product.name} rasmi"><span class="food-info"><strong>${product.name}</strong><small>Yangi tayyorlangan</small></span><b>${Number(product.price).toLocaleString()} so'm</b>`;
    menuList.appendChild(item);
  });
}

document.getElementById("entry-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const businessId = document.getElementById("business-id").value;
  const tableId = document.getElementById("table-id").value;
  const text = document.getElementById("order-text").value.trim();
  const message = document.getElementById("form-message");

  if (!businessId || !tableId) {
    message.textContent = "Avval QR orqali kirib, stolni tanlang.";
    return;
  }
  if (!text) {
    message.textContent = "Buyurtma matnini kiriting.";
    return;
  }
  message.textContent = "Buyurtma yuborilmoqda...";
  try {
    const response = await fetch(`${API_BASE}/Order/CreateFromVoice/voice`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ businessId: Number(businessId), tableId: Number(tableId), transcriptText: text }),
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error || "Buyurtma yuborilmadi.");
    message.textContent = `Buyurtma qabul qilindi. №${result.id}`;
  } catch (error) {
    message.textContent = `Buyurtma yuborilmadi: ${error.message}`;
  }
});