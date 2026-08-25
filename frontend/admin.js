const fallbackTableImage = "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=500&q=80";
const fallbackFoodImage = "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=700&q=80";
const API_BASE = "http://localhost:55983";
const tables = [];
const products = [];
const staff = [];
let ownerAccount = null;
let ownerToken = "";
let activeBusinessId = "";

document.querySelectorAll(".owner-nav-item").forEach(item => item.addEventListener("click", () => {
  document.querySelectorAll(".owner-nav-item").forEach(navItem => navItem.classList.remove("active"));
  item.classList.add("active");
  document.querySelectorAll("[data-owner-panel]").forEach(panel => panel.hidden = panel.dataset.ownerPanel !== item.dataset.ownerView);
  document.getElementById("owner-view-title").textContent = item.querySelector("span").textContent;
}));

document.getElementById("super-form").addEventListener("submit", event => {
  event.preventDefault();
  ownerAccount = {
    restaurant: document.getElementById("new-restaurant-name").value.trim(),
    owner: document.getElementById("owner-name").value.trim(),
    phone: document.getElementById("owner-phone").value.trim(),
    amount: Number(document.getElementById("subscription-amount").value),
    paidAt: document.getElementById("payment-paid-at").value,
    months: Number(document.getElementById("subscription-months").value),
    login: document.getElementById("owner-login").value.trim(),
    password: document.getElementById("owner-password").value,
  };
  createRestaurant(ownerAccount);
});

async function createRestaurant(account) {
  const message = document.getElementById("super-message");
  try {
    const response = await fetch(`${API_BASE}/Business/CreateRestaurant/restaurant`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        restaurantName: account.restaurant,
        restaurantPhoneNumber: account.phone,
        ownerFullName: account.owner,
        ownerPhoneNumber: account.phone,
        subscriptionAmount: account.amount,
        paymentPaidAt: new Date(account.paidAt).toISOString(),
        subscriptionMonths: account.months,
        login: account.login,
        password: account.password,
      }),
    });
    if (!response.ok) throw new Error((await response.json()).error || "Restoran yaratilmadi.");
    const result = await response.json();
    ownerToken = result.accessToken;
    activeBusinessId = result.businessId;
    const paidAt = new Date(result.paymentPaidAt).toLocaleString("uz-UZ");
    message.textContent = `Restoran yaratildi. Login: ${account.login}. To‘lov sanasi: ${paidAt}`;
  } catch (error) {
    message.textContent = `Backend ulanmagan yoki xato: ${error.message}`;
  }
}

document.getElementById("owner-login-form").addEventListener("submit", event => {
  event.preventDefault();
  const login = document.getElementById("login-value").value.trim();
  const password = document.getElementById("password-value").value;
  const message = document.getElementById("login-message");
  loginOwner(login, password, message);
});

async function loginOwner(login, password, message) {
  try {
    const response = await fetch(`${API_BASE}/Business/LoginOwner/owner/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ login, password }),
    });
    if (!response.ok) throw new Error((await response.json()).error || "Login yoki parol noto‘g‘ri.");
    const result = await response.json();
    ownerToken = result.accessToken;
    activeBusinessId = result.businessId;
    document.getElementById("owner-login-card").hidden = true;
    document.getElementById("owner-area").hidden = false;
    document.getElementById("owner-restaurant-name").textContent = result.restaurantName;
  } catch (error) {
    message.textContent = `Kirish amalga oshmadi: ${error.message}`;
  }
}

document.getElementById("logout-button").addEventListener("click", () => {
  document.getElementById("owner-area").hidden = true;
  document.getElementById("owner-login-card").hidden = false;
});

function previewFile(inputId, previewId) {
  const input = document.getElementById(inputId);
  const preview = document.getElementById(previewId);
  input.addEventListener("change", () => {
    const file = input.files[0];
    if (!file) return;
    const url = URL.createObjectURL(file);
    preview.innerHTML = `<img src="${url}" alt="Tanlangan rasm">`;
  });
}

function renderTables() {
  document.getElementById("table-count").textContent = tables.length;
  document.getElementById("table-items").innerHTML = tables.map(table => `
    <div class="admin-item"><img src="${table.image}" alt="${table.name} rasmi"><span><strong>${table.name}</strong><small>${table.capacity} kishilik</small></span></div>
  `).join("");
}

function renderProducts() {
  const foods = products.filter(product => product.kind === "food");
  const drinks = products.filter(product => product.kind === "drink");
  document.getElementById("food-count").textContent = foods.length;
  document.getElementById("drink-count").textContent = drinks.length;
  document.getElementById("product-items").innerHTML = foods.map(product => `
    <div class="admin-item"><img src="${product.image}" alt="${product.name} rasmi"><span><strong>${product.name}</strong><small>${product.price.toLocaleString()} so'm</small></span></div>
  `).join("");
  document.getElementById("drink-items").innerHTML = drinks.map(product => `
    <div class="admin-item"><img src="${product.image}" alt="${product.name} rasmi"><span><strong>${product.name}</strong><small>${product.price.toLocaleString()} so'm</small></span></div>
  `).join("");
}

function renderStaff() {
  const roleNames = { 1: "Menejer", 2: "Kassir", 3: "Ofitsiant", 4: "Oshpaz" };
  document.getElementById("staff-items").innerHTML = staff.map(item => `<div class="admin-item staff-row"><span><strong>${item.fullName}</strong><small>${roleNames[item.role] || "Xodim"} · ${item.phoneNumber}</small></span><span class="staff-state">${item.isActive === false ? "Pasiv" : "Aktiv"}</span></div>`).join("");
}

previewFile("restaurant-image", "restaurant-preview");
previewFile("table-image", "table-preview");
previewFile("product-image", "product-preview");

document.getElementById("restaurant-form").addEventListener("submit", event => {
  event.preventDefault();
  document.getElementById("restaurant-message").textContent = "Restoran ma’lumotlari saqlandi.";
});

document.getElementById("table-form").addEventListener("submit", event => {
  event.preventDefault();
  const name = document.getElementById("table-name").value.trim();
  const capacity = Number(document.getElementById("table-capacity").value);
  const preview = document.querySelector("#table-preview img");
  if (!name || !capacity) return;
  createTable(name, capacity, preview?.src || fallbackTableImage, event.target);
});

async function createTable(name, capacity, image, form) {
  if (!ownerToken || !activeBusinessId) {
    document.getElementById("table-items").innerHTML = '<p class="form-message">Avval restoran egasi sifatida kiring.</p>';
    return;
  }
  const response = await fetch(`${API_BASE}/Business/CreateTable/tables`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Owner-Token": ownerToken },
    body: JSON.stringify({ businessId: Number(activeBusinessId), name, capacity }),
  });
  if (!response.ok) throw new Error("Stolni bazaga saqlab bo‘lmadi.");
  const saved = await response.json();
  tables.push({ name: saved.name || name, capacity: saved.capacity || capacity, image });
  form.reset();
  document.getElementById("table-preview").innerHTML = "<span>Stol rasmi</span>";
  renderTables();
}

document.getElementById("product-form").addEventListener("submit", event => {
  event.preventDefault();
  const name = document.getElementById("product-name").value.trim();
  const price = Number(document.getElementById("product-price").value);
  const kind = document.getElementById("product-kind").value;
  const preview = document.querySelector("#product-preview img");
  if (!name || price < 0) return;
  createProduct(name, price, kind, preview?.src || fallbackFoodImage, event.target);
});

document.getElementById("staff-form").addEventListener("submit", async event => {
  event.preventDefault();
  const name = document.getElementById("staff-name").value.trim();
  const phoneNumber = document.getElementById("staff-phone").value.trim();
  const role = Number(document.getElementById("staff-role").value);
  const message = document.getElementById("staff-message");
  if (!ownerToken || !activeBusinessId) {
    message.textContent = "Avval restoran egasi sifatida kiring.";
    return;
  }
  try {
    const response = await fetch(`${API_BASE}/Business/CreateStaff/staff`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Owner-Token": ownerToken },
      body: JSON.stringify({ businessId: Number(activeBusinessId), fullName: name, phoneNumber, role }),
    });
    if (!response.ok) throw new Error("Xodimni saqlab bo‘lmadi.");
    staff.push(await response.json());
    event.target.reset();
    renderStaff();
    message.textContent = "Xodim qo‘shildi.";
  } catch (error) {
    message.textContent = error.message;
  }
});

async function createProduct(name, price, kind, image, form) {
  if (!ownerToken || !activeBusinessId) {
    document.getElementById("product-items").innerHTML = '<p class="form-message">Avval restoran egasi sifatida kiring.</p>';
    return;
  }
  const response = await fetch(`${API_BASE}/Product/Create`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Owner-Token": ownerToken },
    body: JSON.stringify({ businessId: Number(activeBusinessId), name, price, unit: "dona", aliases: [kind] }),
  });
  if (!response.ok) throw new Error("Taomni bazaga saqlab bo‘lmadi.");
  const saved = await response.json();
  products.push({ name: saved.name || name, price: saved.price ?? price, kind, image });
  form.reset();
  document.getElementById("product-preview").innerHTML = "<span>Taom rasmi</span>";
  renderProducts();
}
