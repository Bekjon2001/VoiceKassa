const API_BASE = "http://localhost:55983";
const list = document.getElementById("restaurant-list");
const details = document.getElementById("payment-details");
let selectedRestaurantId = null;
let superAdminToken = "";

document.querySelectorAll(".workspace-nav-item").forEach(item => item.addEventListener("click", () => {
  document.querySelectorAll(".workspace-nav-item").forEach(navItem => navItem.classList.remove("active"));
  item.classList.add("active");
  document.querySelectorAll("[data-view-panel]").forEach(panel => panel.hidden = panel.dataset.viewPanel !== item.dataset.view);
}));

function showSuperDashboard(account) {
  superAdminToken = account.accessToken;
  document.getElementById("super-auth").hidden = true;
  document.getElementById("super-dashboard").hidden = false;
  loadRestaurants();
}

async function getJson(url) {
  const response = await fetch(`${API_BASE}${url}`, { headers: { "X-Super-Admin-Token": superAdminToken } });
  if (!response.ok) throw new Error("Ma’lumotni yuklab bo‘lmadi.");
  return response.json();
}

async function loadRestaurants() {
  try {
    const restaurants = await getJson("/Business/GetAll");
    list.innerHTML = restaurants.length ? "" : '<tr><td colspan="9" class="table-empty">Restoranlar hali yo‘q.</td></tr>';
    restaurants.forEach(restaurant => {
      const row = document.createElement("tr");
      row.className = "restaurant-row";
      row.innerHTML = `<td>${restaurant.id}</td><td><strong>${restaurant.name}</strong><small>${restaurant.address || "Manzil kiritilmagan"}</small></td><td>Yuklanmoqda...</td><td>${restaurant.phoneNumber || "-"}</td><td>-</td><td>-</td><td>-</td><td><span class="status-pill active">Aktiv</span></td><td><button class="row-action" type="button">Ko‘rish</button></td>`;
      const button = row.querySelector(".row-action");
      button.addEventListener("click", () => selectRestaurant(restaurant, button));
      row.addEventListener("click", event => { if (event.target !== button) selectRestaurant(restaurant, button); });
      list.appendChild(row);
    });
  } catch (error) {
    list.innerHTML = `<tr><td colspan="9" class="table-empty">${error.message}</td></tr>`;
  }
}

async function selectRestaurant(restaurant, button) {
  document.querySelectorAll(".restaurant-option").forEach(item => item.classList.remove("selected"));
  button.classList.add("selected");
  document.getElementById("selected-restaurant").textContent = restaurant.name;
  details.innerHTML = '<p class="form-message">Yuklanmoqda...</p>';
  try {
    const owner = await getJson(`/Business/GetOwner/${restaurant.id}/owner`);
    details.innerHTML = `<div class="detail-item"><small>Xo‘jayin</small><strong>${owner.ownerFullName}</strong></div><div class="detail-item"><small>Telefon</small><strong>${owner.ownerPhoneNumber}</strong></div><div class="detail-item"><small>Login</small><strong>${owner.login}</strong></div><div class="detail-item"><small>To‘lov summasi</small><strong>${Number(owner.subscriptionAmount).toLocaleString()} so‘m</strong></div><div class="detail-item"><small>To‘lov sanasi</small><strong>${new Date(owner.paymentPaidAt).toLocaleString("uz-UZ")}</strong></div><div class="detail-item"><small>Obuna</small><strong>${owner.subscriptionMonths} oy</strong></div><div class="detail-item"><small>Tugash sanasi</small><strong>${new Date(owner.subscriptionEndsAt).toLocaleDateString("uz-UZ")}</strong></div>`;
    selectedRestaurantId = restaurant.id;
  } catch (error) {
    details.innerHTML = `<p class="form-message">${error.message}</p>`;
  }
}

function showForm(formId, firstFieldId) {
  const formCard = document.getElementById(formId);
  formCard.hidden = false;
  formCard.scrollIntoView({ behavior: "smooth", block: "start" });
  document.getElementById(firstFieldId)?.focus({ preventScroll: true });
}

document.getElementById("show-restaurant-form").addEventListener("click", () => showForm("restaurant-form-card", "new-restaurant-name"));
document.getElementById("restaurant-create-form").addEventListener("submit", async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.target));
  data.subscriptionAmount = Number(data.subscriptionAmount);
  data.subscriptionMonths = Number(data.subscriptionMonths);
  data.paymentPaidAt = new Date(data.paymentPaidAt).toISOString();
  const response = await fetch(`${API_BASE}/Business/CreateRestaurant/restaurant`, { method: "POST", headers: { "Content-Type": "application/json", "X-Super-Admin-Token": superAdminToken }, body: JSON.stringify(data) });
  document.getElementById("restaurant-create-message").textContent = response.ok ? "Restoran yaratildi." : "Restoran yaratilmadi.";
  if (response.ok) { event.target.reset(); loadRestaurants(); }
});

document.getElementById("super-login-form").addEventListener("submit", async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.target));
  const response = await fetch(`${API_BASE}/Business/LoginSuperAdmin/super-admin/login`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(data) });
  const result = await response.json();
  document.getElementById("super-auth-message").textContent = response.ok ? "Kirish muvaffaqiyatli." : (result.error || "Login xato.");
  if (response.ok) showSuperDashboard(result);
});

document.getElementById("show-first-super-form").addEventListener("click", () => {
  document.getElementById("first-super-form").hidden = false;
  document.getElementById("first-super-form").querySelector("input").focus();
});

document.getElementById("first-super-form").addEventListener("submit", async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.target));
  const response = await fetch(`${API_BASE}/Business/CreateFirstSuperAdmin/super-admin/first`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  const result = await response.json();
  document.getElementById("super-auth-message").textContent = response.ok
    ? "Super Admin yaratildi. Panel ochilmoqda..."
    : (result.error || "Super Admin yaratilmadi.");
  if (response.ok) showSuperDashboard(result);
});

