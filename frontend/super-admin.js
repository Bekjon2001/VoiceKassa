"use strict";

// Sahifa qaysi manzildan ochildi (https:55982 yoki http:55983) —
// API ham shu joyga ulanadi, "mixed content" bloki yo'q. Fayl sifatida
// ochildi (file://) bo'lsa standart portga tushadi.
const API_BASE = location.protocol.startsWith("http")
  ? `${location.protocol}//${location.host}`
  : "http://localhost:55983";
const SESSION_KEY = "vk_super_session";

const VIEW_TITLES = {
  restaurants: ["Restoranlar", "Restoranlar va obunalarni boshqaring."],
  create: ["Yangi restoran", "Yangi restoran va uning egasini ro‘yxatdan o‘tkazing."],
  markets: ["Supermarketlar", "Supermarketlar va obunalarni boshqaring."],
  "market-create": ["Yangi supermarket", "Yangi supermarket va uning egasini ro‘yxatdan o‘tkazing."],
  system: ["Tizim sozlamalari", "Platforma darajasidagi sozlamalar."],
};

let superAdminToken = "";
let selectedRestaurantId = null;
let selectedRestaurant = null;
let restaurantsCache = [];
let selectedMarketId = null;
let selectedMarket = null;
let marketsCache = [];
let currentMarketFilter = "";

// ---------- Yordamchi funksiyalar ----------
function $(id) { return document.getElementById(id); }

function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  }[ch]));
}

function fmtSum(value) {
  return Number(value || 0).toLocaleString("uz-UZ");
}

function formatDate(value) {
  const date = value ? new Date(value) : null;
  return date && !Number.isNaN(date.getTime())
    ? date.toLocaleDateString("uz-UZ")
    : "-";
}

function setMsg(el, text, state) {
  if (!el) return;
  el.textContent = text;
  el.className = "form-message" + (state ? " " + state : "");
}

function flash(text, isError) {
  const el = $("sa-flash");
  if (!el) return;
  el.textContent = text;
  el.classList.toggle("err", Boolean(isError));
  el.hidden = false;
  clearTimeout(flash._t);
  flash._t = setTimeout(() => { el.hidden = true; }, 6000);
}

function saveToken(token) {
  superAdminToken = token || "";
  try {
    if (token) sessionStorage.setItem(SESSION_KEY, JSON.stringify({ token }));
    else sessionStorage.removeItem(SESSION_KEY);
  } catch { /* ignore */ }
}

function restoreToken() {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY);
    return raw ? (JSON.parse(raw).token || "") : "";
  } catch { return ""; }
}

async function getJson(url) {
  const response = await fetch(`${API_BASE}${url}`, {
    headers: { "X-Super-Admin-Token": superAdminToken },
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data && data.error) || "Ma’lumotni yuklab bo‘lmadi (" + response.status + ").");
  }
  return data;
}
// ---------- POST so'rovlari ----------
async function postJson(url, body, headers = {}) {
  const response = await fetch(`${API_BASE}${url}`, {
    method: "POST",
    headers: Object.assign({ "Content-Type": "application/json" }, headers),
    body: JSON.stringify(body),
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data && data.error) || "So‘rov bajarilmadi (" + response.status + ").");
  }
  return data;
}

// ---------- Ko'rinish almashtirish ----------
function setView(view) {
  document.querySelectorAll(".nav-item[data-view]")
    .forEach(nav => nav.classList.toggle("active", nav.dataset.view === view));
  document.querySelectorAll("[data-view-panel]")
    .forEach(panel => { panel.hidden = panel.dataset.viewPanel !== view; });
  const pair = VIEW_TITLES[view] || [view, ""];
  $("sa-view-title").textContent = pair[0];
  $("sa-view-subtitle").textContent = pair[1];
}

document.querySelectorAll(".nav-item[data-view]").forEach(item =>
  item.addEventListener("click", () => setView(item.dataset.view)));

$("show-create-view").addEventListener("click", () => setView("create"));

$("show-create-market-view").addEventListener("click", () => setView("market-create"));

$("sa-logout-button").addEventListener("click", () => {
  saveToken("");
  location.reload();
});

// ---------- Auth: Super Admin kirishi va birinchi akkaunt ----------
$("show-first-super-form").addEventListener("click", () => {
  const form = $("first-super-form");
  form.hidden = !form.hidden;
  if (!form.hidden) form.querySelector("input").focus();
});

$("super-login-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("super-auth-message");
  setMsg(message, "Kirilmoqda...");
  try {
    const result = await postJson("/Business/LoginSuperAdmin/super-admin/login", {
      login: $("sa-login").value.trim(),
      password: $("sa-password").value,
    });
    saveToken(result.accessToken);
    setMsg(message, "", "");
    showDashboard();
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});

$("first-super-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("super-auth-message");
  setMsg(message, "Yaratilmoqda...");
  try {
    const result = await postJson("/Business/CreateFirstSuperAdmin/super-admin/first", {
      fullName: event.target.elements.fullName.value.trim(),
      phoneNumber: event.target.elements.phoneNumber.value.trim(),
      login: event.target.elements.login.value.trim(),
      password: event.target.elements.password.value,
    });
    saveToken(result.accessToken);
    setMsg(message, "Super Admin yaratildi.", "ok");
    showDashboard();
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});

// ---------- Yangi restoran yaratish formasi ----------
$("restaurant-create-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("restaurant-create-message");
  setMsg(message, "Saqlanmoqda...");
  try {
    const elements = event.target.elements;
    await postJson("/Business/CreateRestaurant/restaurant", {
      restaurantName: elements.restaurantName.value.trim(),
      ownerFullName: elements.ownerFullName.value.trim(),
      ownerPhoneNumber: elements.ownerPhoneNumber.value.trim(),
      subscriptionAmount: Number(elements.subscriptionAmount.value),
      paymentPaidAt: new Date(elements.paymentPaidAt.value).toISOString(),
      subscriptionMonths: Number(elements.subscriptionMonths.value),
      login: elements.login.value.trim(),
      password: elements.password.value,
    }, { "X-Super-Admin-Token": superAdminToken });
    event.target.reset();
    setMsg(message, "Restoran yaratildi.", "ok");
    flash("Yangi restoran ro‘yxatga olindi.");
    setView("restaurants");
    loadRestaurants();
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});

// ---------- Yangi supermarket yaratish formasi ----------
$("market-create-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("market-create-message");
  setMsg(message, "Saqlanmoqda...");
  try {
    const elements = event.target.elements;
    await postJson("/Business/CreateMarket/market", {
      marketName: elements.marketName.value.trim(),
      ownerFullName: elements.ownerFullName.value.trim(),
      ownerPhoneNumber: elements.ownerPhoneNumber.value.trim(),
      subscriptionAmount: Number(elements.subscriptionAmount.value),
      paymentPaidAt: new Date(elements.paymentPaidAt.value).toISOString(),
      subscriptionMonths: Number(elements.subscriptionMonths.value),
      login: elements.login.value.trim(),
      password: elements.password.value,
    }, { "X-Super-Admin-Token": superAdminToken });
    event.target.reset();
    setMsg(message, "Supermarket yaratildi.", "ok");
    flash("Yangi supermarket ro‘yxatga olindi.");
    setView("markets");
    loadMarkets();
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});

// ---------- Restoranlar jadvali ----------
let currentFilter = "";

// Holat pill: Passiv > obuna muddati ustuvorligi bilan.
function statusPillHtml(owner) {
  if (owner.isActive === false) return '<span class="pill pill--off">Passiv</span>';
  const activeSub = new Date(owner.subscriptionEndsAt) > new Date();
  return activeSub
    ? '<span class="pill pill--ok">Aktiv</span>'
    : '<span class="pill pill--bad">Tugagan</span>';
}

function ownerCellsHtml(restaurant) {
  const owner = restaurant._owner;
  if (!owner) {
    return '<td class="cell-owner"><span class="cell-loading">...</span></td>'
      + '<td>-</td><td>-</td><td>-</td>'
      + '<td class="cell-status"><span class="cell-loading">...</span></td>';
  }
  return `<td class="cell-owner">${esc(owner.ownerFullName || "-")}</td>`
    + `<td>${fmtSum(owner.subscriptionAmount)} so‘m</td>`
    + `<td>${formatDate(owner.paymentPaidAt)}</td>`
    + `<td>${owner.subscriptionMonths || 0} oy</td>`
    + `<td class="cell-status">${statusPillHtml(owner)}</td>`;
}

function renderRestaurantRows() {
  const tbody = $("restaurant-list");
  const query = currentFilter.trim().toLowerCase();
  const visible = restaurantsCache.filter(r => !query ||
    [r.name, r.address, r.phoneNumber].join(" ").toLowerCase().includes(query));

  if (!visible.length) {
    tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;color:var(--muted)">'
      + (restaurantsCache.length ? "Mos restoran topilmadi." : "Restoranlar hali yo‘q.")
      + "</td></tr>";
    return;
  }

  tbody.innerHTML = "";
  visible.forEach(restaurant => {
    const row = document.createElement("tr");
    row.className = "rowlink";
    row.dataset.business = restaurant.id;
    if (restaurant.id === selectedRestaurantId) row.classList.add("row-selected");
    row.innerHTML =
      `<td>${restaurant.id}</td>`
      + `<td><strong>${esc(restaurant.name)}</strong><small>${esc(restaurant.address || "Manzil kiritilmagan")}</small></td>`
      + `<td>${esc(restaurant.phoneNumber || "-")}</td>` + ownerCellsHtml(restaurant)
      + '<td><button class="btn btn-ghost btn-sm row-action" type="button">Ko‘rish</button></td>';
    row.addEventListener("click", () => selectRestaurant(restaurant, row));
    tbody.appendChild(row);
  });
}
// ---------- Restoranlarni yuklash va boyitish ----------
async function loadRestaurants() {
  selectedRestaurantId = null;
  selectedRestaurant = null;
  $("owner-actions").hidden = true;
  $("reset-credentials-form").hidden = true;
  restaurantsCache = [];
  renderStatsBar(0, 0, 0);
  try {
    const all = await getJson("/Business/GetAll");
    // Faqat Restaurant (Type = 0) filtri — Supermarket alohida bo'limda.
    restaurantsCache = (Array.isArray(all) ? all : []).filter(b => Number(b.type) === 0);
  } catch (error) {
    restaurantsCache = [];
    $("restaurant-list").innerHTML =
      `<tr><td colspan="9" style="text-align:center;color:var(--red)">${esc(error.message)}</td></tr>`;
    return;
  }
  renderRestaurantRows();

  // Har bir restoran uchun egasini parallel olib kelamiz.
  await Promise.allSettled(restaurantsCache.map(r => enrichWithOwner(r)));
  renderRestaurantRows();
  updateSuperStats();
}

// ---------- Supermarketlarni yuklash va boyitish ----------
async function loadMarkets() {
  selectedMarketId = null;
  selectedMarket = null;
  $("market-owner-actions").hidden = true;
  $("market-reset-credentials-form").hidden = true;
  renderMarketStatsBar(0, 0, 0);
  marketsCache = [];
  try {
    const all = await getJson("/Business/GetAll");
    // Faqat Market (1) turi — Supermarketlar bo‘limi.
    marketsCache = (Array.isArray(all) ? all : []).filter(b => Number(b.type) === 1);
  } catch (error) {
    marketsCache = [];
    $("market-list").innerHTML =
      `<tr><td colspan="9" style="text-align:center;color:var(--red)">${esc(error.message)}</td></tr>`;
    return;
  }
  renderMarketRows();

  // Har bir supermarket uchun egasini parallel olib kelamiz.
  await Promise.allSettled(marketsCache.map(m => enrichWithOwner(m)));
  renderMarketRows();
  updateMarketStats();
}

async function enrichWithOwner(restaurant) {
  try {
    restaurant._owner = await getJson(`/Business/GetOwner/${restaurant.id}/owner`);
  } catch {
    restaurant._owner = null; // ega topilmadi / xatolik
  }
}

function updateSuperStats() {
  let active = 0;
  let expired = 0;
  let passive = 0;
  restaurantsCache.forEach(r => {
    if (!r._owner) return;
    if (r._owner.isActive === false) passive++;
    else if (r._owner.subscriptionEndsAt) {
      new Date(r._owner.subscriptionEndsAt) > new Date() ? active++ : expired++;
    }
  });
  renderStatsBar(restaurantsCache.length, active, expired, passive);
}

function renderStatsBar(total, active, expired, passive) {
  $("sa-total").textContent = String(total);
  $("sa-active").textContent = String(active);
  $("sa-expired").textContent = String(expired);
  $("sa-passive").textContent = String(passive);
}

function updateMarketStats() {
  let active = 0;
  let expired = 0;
  let passive = 0;
  marketsCache.forEach(m => {
    if (!m._owner) return;
    if (m._owner.isActive === false) passive++;
    else if (m._owner.subscriptionEndsAt) {
      new Date(m._owner.subscriptionEndsAt) > new Date() ? active++ : expired++;
    }
  });
  renderMarketStatsBar(marketsCache.length, active, expired, passive);
}

function renderMarketStatsBar(total, active, expired, passive) {
  $("market-total").textContent = String(total);
  $("market-active").textContent = String(active);
  $("market-expired").textContent = String(expired);
  $("market-passive").textContent = String(passive);
}

// ---------- Supermarketlar jadvali ----------
function marketOwnerCellsHtml(market) {
  const owner = market._owner;
  if (!owner) {
    return '<td class="cell-owner"><span class="cell-loading">...</span></td>'
      + '<td>-</td><td>-</td><td>-</td>'
      + '<td class="cell-status"><span class="cell-loading">...</span></td>';
  }
  return `<td class="cell-owner">${esc(owner.ownerFullName || "-")}</td>`
    + `<td>${fmtSum(owner.subscriptionAmount)} so‘m</td>`
    + `<td>${formatDate(owner.paymentPaidAt)}</td>`
    + `<td>${owner.subscriptionMonths || 0} oy</td>`
    + `<td class="cell-status">${statusPillHtml(owner)}</td>`;
}

function renderMarketRows() {
  const tbody = $("market-list");
  const query = currentMarketFilter.trim().toLowerCase();
  const visible = marketsCache.filter(m => !query ||
    [m.name, m.address, m.phoneNumber].join(" ").toLowerCase().includes(query));

  if (!visible.length) {
    tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;color:var(--muted)">'
      + (marketsCache.length ? "Mos supermarket topilmadi." : "Supermarketlar hali yo‘q.")
      + "</td></tr>";
    return;
  }

  tbody.innerHTML = "";
  visible.forEach(market => {
    const row = document.createElement("tr");
    row.className = "rowlink";
    row.dataset.business = market.id;
    if (market.id === selectedMarketId) row.classList.add("row-selected");
    row.innerHTML =
      `<td>${market.id}</td>`
      + `<td><strong>${esc(market.name)}</strong><small>${esc(market.address || "Manzil kiritilmagan")}</small></td>`
      + `<td>${esc(market.phoneNumber || "-")}</td>` + marketOwnerCellsHtml(market)
      + '<td><button class="btn btn-ghost btn-sm row-action" type="button">Ko‘rish</button></td>';
    row.addEventListener("click", () => selectMarket(market, row));
    tbody.appendChild(row);
  });
}

// ---------- Restoran tanlash (batafsil panel) ----------
async function selectRestaurant(restaurant, row) {
  document.querySelectorAll("#restaurant-list .row-selected")
    .forEach(other => other.classList.remove("row-selected"));
  row.classList.add("row-selected");

  selectedRestaurantId = restaurant.id;
  $("selected-restaurant").textContent = restaurant.name;
  const details = $("payment-details");
  details.innerHTML = '<p class="empty" style="margin-top:0">Yuklanmoqda...</p>';

  try {
    const owner = restaurant._owner || await getJson(`/Business/GetOwner/${restaurant.id}/owner`);
    restaurant._owner = owner;
    details.innerHTML = `<div class="detail-grid">
      <div class="detail-item"><small>Xo‘jayin</small><strong>${esc(owner.ownerFullName)}</strong></div>
      <div class="detail-item"><small>Telefon</small><strong>${esc(owner.ownerPhoneNumber || "-")}</strong></div>
      <div class="detail-item"><small>Login</small><strong>${esc(owner.login)}</strong></div>
      <div class="detail-item"><small>To‘lov summasi</small><strong>${fmtSum(owner.subscriptionAmount)} so‘m</strong></div>
      <div class="detail-item"><small>To‘lov sanasi</small><strong>${formatDate(owner.paymentPaidAt)}</strong></div>
      <div class="detail-item"><small>Obuna</small><strong>${owner.subscriptionMonths || 0} oy</strong></div>
      <div class="detail-item"><small>Tugash sanasi</small><strong>${formatDate(owner.subscriptionEndsAt)}</strong></div>
      <div class="detail-item"><small>Holati</small><strong>${statusPillHtml(owner)}</strong></div>
    </div>`;
    // Harakat tugmalari: Passiv/Faollashtirish + login/parolni tiklash.
    selectedRestaurant = restaurant;
    $("owner-actions").hidden = false;
    $("toggle-owner-status").textContent = owner.isActive === false ? "Faollashtirish" : "Passiv qilish";
    $("new-owner-login").value = owner.login || "";
    $("new-owner-password").value = "";
    setMsg($("reset-credentials-message"), "");
    updateSuperStats();
  } catch (error) {
    details.innerHTML = `<p class="empty" style="margin-top:0">${esc(error.message)}</p>`;
  }
}

// ---------- Supermarket tanlash (batafsil panel) ----------
async function selectMarket(market, row) {
  document.querySelectorAll("#market-list .row-selected")
    .forEach(other => other.classList.remove("row-selected"));
  row.classList.add("row-selected");

  selectedMarketId = market.id;
  $("selected-market").textContent = market.name;
  const details = $("market-payment-details");
  details.innerHTML = '<p class="empty" style="margin-top:0">Yuklanmoqda...</p>';

  try {
    const owner = market._owner || await getJson(`/Business/GetOwner/${market.id}/owner`);
    market._owner = owner;
    details.innerHTML = `<div class="detail-grid">
      <div class="detail-item"><small>Xo‘jayin</small><strong>${esc(owner.ownerFullName)}</strong></div>
      <div class="detail-item"><small>Telefon</small><strong>${esc(owner.ownerPhoneNumber || "-")}</strong></div>
      <div class="detail-item"><small>Login</small><strong>${esc(owner.login)}</strong></div>
      <div class="detail-item"><small>To‘lov summasi</small><strong>${fmtSum(owner.subscriptionAmount)} so‘m</strong></div>
      <div class="detail-item"><small>To‘lov sanasi</small><strong>${formatDate(owner.paymentPaidAt)}</strong></div>
      <div class="detail-item"><small>Obuna</small><strong>${owner.subscriptionMonths || 0} oy</strong></div>
      <div class="detail-item"><small>Tugash sanasi</small><strong>${formatDate(owner.subscriptionEndsAt)}</strong></div>
      <div class="detail-item"><small>Holati</small><strong>${statusPillHtml(owner)}</strong></div>
    </div>`;
    // Harakat tugmalari: Passiv/Faollashtirish + login/parolni tiklash.
    selectedMarket = market;
    $("market-owner-actions").hidden = false;
    $("market-toggle-status").textContent = owner.isActive === false ? "Faollashtirish" : "Passiv qilish";
    $("new-market-login").value = owner.login || "";
    $("new-market-password").value = "";
    setMsg($("market-reset-message"), "");
    updateMarketStats();
  } catch (error) {
    details.innerHTML = `<p class="empty" style="margin-top:0">${esc(error.message)}</p>`;
  }
}

// ---------- Qidiruv ----------
$("restaurant-search").addEventListener("input", event => {
  currentFilter = event.target.value;
  renderRestaurantRows();
});

$("market-search").addEventListener("input", event => {
  currentMarketFilter = event.target.value;
  renderMarketRows();
});

// ---------- Dashboardni ochish ----------
function showDashboard() {
  $("super-auth").hidden = true;
  $("super-dashboard").hidden = false;
  setView("restaurants");
  loadRestaurants();
}

// ---------- Boshlash: tokenni tiklash ----------
const savedToken = restoreToken();
if (savedToken) {
  saveToken(savedToken);
  showDashboard();
}

// ---------- PUT so'rovi (Super Admin huquqlari bilan) ----------
async function putJson(url, body) {
  const response = await fetch(`${API_BASE}${url}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", "X-Super-Admin-Token": superAdminToken },
    body: JSON.stringify(body),
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data && data.error) || "So‘rov bajarilmadi (" + response.status + ").");
  }
  return data;
}

// ---------- Passiv qilish / Faollashtirish ----------
$("toggle-owner-status").addEventListener("click", async () => {
  if (!selectedRestaurant || !selectedRestaurant._owner) return;
  const owner = selectedRestaurant._owner;
  const makeActive = owner.isActive === false;
  try {
    const updated = await putJson("/Business/UpdateOwnerStatus", {
      businessId: Number(selectedRestaurant.id),
      isActive: makeActive,
    });
    selectedRestaurant._owner = updated;
    flash(makeActive ? "Restoran faollashtirildi." : "Restoran passiv qilindi.");
    renderRestaurantRows();
    updateSuperStats();
    const selectedRow = document.querySelector("#restaurant-list .row-selected");
    if (selectedRow) selectRestaurant(selectedRestaurant, selectedRow);
  } catch (error) {
    flash(error.message, true);
  }
});

// ---------- Login/parolni tiklash (restoran qayta yaratmasdan) ----------
$("show-reset-form").addEventListener("click", () => {
  const form = $("reset-credentials-form");
  form.hidden = !form.hidden;
  if (!form.hidden) $("new-owner-login").focus();
});

$("cancel-reset-form").addEventListener("click", () => {
  $("reset-credentials-form").hidden = true;
  setMsg($("reset-credentials-message"), "");
});

$("reset-credentials-form").addEventListener("submit", async event => {
  event.preventDefault();
  if (!selectedRestaurant) return;
  const message = $("reset-credentials-message");
  const newLogin = $("new-owner-login").value.trim();
  const newPassword = $("new-owner-password").value;
  if (!newLogin && !newPassword) {
    setMsg(message, "Yangi login yoki parol kiritilishi kerak.", "err");
    return;
  }
  setMsg(message, "Saqlanmoqda...");
  try {
    const updated = await putJson("/Business/ResetOwnerCredentials", {
      businessId: Number(selectedRestaurant.id),
      newLogin: newLogin || null,
      newPassword: newPassword || null,
    });
    selectedRestaurant._owner = updated;
    $("new-owner-password").value = "";
    setMsg(message, "Saqlandi. Endi egaga yangi login/parolni bering.", "ok");
    flash("Login/parol tiklandi.");
    renderRestaurantRows();
    updateSuperStats();
    const selectedRow = document.querySelector("#restaurant-list .row-selected");
    if (selectedRow) selectRestaurant(selectedRestaurant, selectedRow);
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});
// ---------- Passiv qilish / Faollashtirish (supermarket) ----------
$("market-toggle-status").addEventListener("click", async () => {
  if (!selectedMarket || !selectedMarket._owner) return;
  const owner = selectedMarket._owner;
  const makeActive = owner.isActive === false;
  try {
    const updated = await putJson("/Business/UpdateOwnerStatus", {
      businessId: Number(selectedMarket.id),
      isActive: makeActive,
    });
    selectedMarket._owner = updated;
    flash(makeActive ? "Supermarket faollashtirildi." : "Supermarket passiv qilindi.");
    renderMarketRows();
    updateMarketStats();
    const selectedRow = document.querySelector("#market-list .row-selected");
    if (selectedRow) selectMarket(selectedMarket, selectedRow);
  } catch (error) {
    flash(error.message, true);
  }
});

// ---------- Login/parolni tiklash (supermarket qayta yaratmasdan) ----------
$("market-show-reset-form").addEventListener("click", () => {
  const form = $("market-reset-credentials-form");
  form.hidden = !form.hidden;
  if (!form.hidden) $("new-market-login").focus();
});

$("market-reset-cancel").addEventListener("click", () => {
  $("market-reset-credentials-form").hidden = true;
  setMsg($("market-reset-message"), "");
});

$("market-reset-credentials-form").addEventListener("submit", async event => {
  event.preventDefault();
  if (!selectedMarket) return;
  const message = $("market-reset-message");
  const newLogin = $("new-market-login").value.trim();
  const newPassword = $("new-market-password").value;
  if (!newLogin && !newPassword) {
    setMsg(message, "Yangi login yoki parol kiritilishi kerak.", "err");
    return;
  }
  setMsg(message, "Saqlanmoqda...");
  try {
    const updated = await putJson("/Business/ResetOwnerCredentials", {
      businessId: Number(selectedMarket.id),
      newLogin: newLogin || null,
      newPassword: newPassword || null,
    });
    selectedMarket._owner = updated;
    $("new-market-password").value = "";
    setMsg(message, "Saqlandi. Endi egaga yangi login/parolni bering.", "ok");
    flash("Login/parol tiklandi.");
    renderMarketRows();
    updateMarketStats();
    const selectedRow = document.querySelector("#market-list .row-selected");
    if (selectedRow) selectMarket(selectedMarket, selectedRow);
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});
