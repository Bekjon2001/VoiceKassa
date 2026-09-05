"use strict";

// Sahifa qaysi manzildan ochildi (https:55982 yoki http:55983) —
// API ham shu joyga ulanadi, "mixed content" bloki yo'q. Fayl sifatida
// ochildi (file://) bo'lsa standart portga tushadi.
const API_BASE = location.protocol.startsWith("http")
  ? `${location.protocol}//${location.host}`
  : "http://localhost:55983";
const SESSION_KEY = "vk_super_session";

const JARVIS_KEY = "vk_jarvis_enabled"; // Jarvis (AI buyruqlar) switch holati — localStorage

// JARVIS rejimi: buyruq faqat switch yoqilgan VA matnda "AI"/"Jarvis" so'zi (vaqf so'z) bo'lsa bajariladi.
function isJarvisOn() { const el = $("jarvis-toggle"); return !!el && el.checked; }
// Vaqf so'z: "AI"/"Jarvis" (latin) + ovozda kirillcha eshitilgan variantlar (джарвис/джарвес)
function hasJarvisWake(text) {
  const t = String(text || "").toLowerCase();
  return /\b(jarvis|jarves|jayvis|ai)\b/.test(t) || /джарвис|джарвес|джарвиз/.test(t);
}
function jarvisShouldRun(text) { return isJarvisOn() && hasJarvisWake(text); }

// Jarvis switch holatini tiklash (sahifa yangilansa ham saqlanadi) va saqlash
(function () {
  const wrap = document.getElementById("jarvis-wrap");
  const el = document.getElementById("jarvis-toggle");
  if (!el) return;
  try { el.checked = localStorage.getItem(JARVIS_KEY) === "1"; } catch { /* ignore */ }
  const apply = () => { if (wrap) wrap.classList.toggle("jarvis-toggle--on", el.checked); };
  apply();
  el.addEventListener("change", () => {
    try { localStorage.setItem(JARVIS_KEY, el.checked ? "1" : "0"); } catch { /* ignore */ }
    apply();
    // Switch yoqilganda Jarvis doimiy eshitishni boshlaydi, o'chirilganda to'xtatadi
    if (el.checked) startAlwaysListen(); else stopAlwaysListen();
  });
})();

const VIEW_TITLES = {
  restaurants: ["Restoranlar", "Restoranlar va obunalarni boshqaring."],
  create: ["Yangi restoran", "Yangi restoran va uning egasini ro‘yxatdan o‘tkazing."],
  markets: ["Supermarketlar", "Supermarketlar va obunalarni boshqaring."],
  "market-create": ["Yangi supermarket", "Yangi supermarket va uning egasini ro‘yxatdan o‘tkazing."],
  system: ["Tizim sozlamalari", "Platforma darajasidagi sozlamalar."],
  ai: ["AI yordamchi", "Ovozli AI yordamchi — platforma haqida savol bering."],
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

// ======================= Ovozli buyruqlar (Super Admin) =======================
function normalizeVoiceText(text) {
  return String(text || "")
    .trim()
    .toLowerCase()
    .replace(/\s+/g, " ")
    .replace(/["']/g, "")
    .replace(/[.,!?]+$/g, "")
    .replace(/\brestoranlar\b/g, "restoran")
    .replace(/\bpechlar\b/g, "restoran")
    .replace(/\bpech\b/g, "restoran")
    .replace(/\bmagazin\b/g, "supermarket")
    .replace(/\bmarket\b/g, "supermarket")
    // Ovoz (mikrofon) orqali kirgan keng tarqalgan imlo xatolari — Jarvis buyruqlari ularni ham taniydi:) Restoranlar/royxat turlari
    .replace(/\brestornlarni\b/g, "restoran")
    .replace(/\brestornlari\b/g, "restoran")
    .replace(/\brestarnlar\b/g, "restoran")
    .replace(/\brestranlar\b/g, "restoran")
    .replace(/\brestornni\b/g, "restoran")
    .replace(/\brestorni\b/g, "restoran")
    .replace(/\brestarnni\b/g, "restoran")
    .replace(/\brestarni\b/g, "restoran")
    .replace(/\brestarn\b/g, "restoran")
    // "restorani" / "restoranni" — e'tiborsiz qolgan shakllar
    .replace(/\brestoranni\b/g, "restoran")
    .replace(/\brestorani\b/g, "restoran")
    // "ro'yxat"ning ovoz orqali buzilgan shakllari (suffikslar bilan ham: royxati, ruyxatini, ...)
    .replace(/\b(?:r[uo]yxat|ryyxat|rxxat|ro[uy]hat)[a-z]*\b/g, "ro'yxat");
}

function findRestaurantByVoice(text) {
  const clean = normalizeVoiceText(text);
  const idMatch = clean.match(/restoran\s*(\d+)/i);
  if (idMatch) {
    const id = Number(idMatch[1]);
    const found = restaurantsCache.find(r => Number(r.id) === id);
    if (found) return found;
  }

  const phrase = clean.replace(/^(restoran|supermarket)\s+/, "").trim();
  if (!phrase) return restaurantsCache[0] || null;

  const byName = restaurantsCache
    .filter(r => typeof r.name === "string")
    .sort((a, b) => b.name.length - a.name.length)
    .find(r => r.name.toLowerCase().includes(phrase) || phrase.includes(r.name.toLowerCase()));
  if (byName) return byName;

  const byText = restaurantsCache.find(r => [r.name, r.address, r.phoneNumber].join(" ").toLowerCase().includes(phrase));
  return byText || null;
}

function findMarketByVoice(text) {
  const clean = normalizeVoiceText(text);
  const idMatch = clean.match(/supermarket\s*(\d+)/i);
  if (idMatch) {
    const id = Number(idMatch[1]);
    const found = marketsCache.find(m => Number(m.id) === id);
    if (found) return found;
  }

  const phrase = clean.replace(/^supermarket\s+/, "").trim();
  if (!phrase) return marketsCache[0] || null;

  const byName = marketsCache
    .filter(m => typeof m.name === "string")
    .sort((a, b) => b.name.length - a.name.length)
    .find(m => m.name.toLowerCase().includes(phrase) || phrase.includes(m.name.toLowerCase()));
  if (byName) return byName;

  return marketsCache.find(m => [m.name, m.address, m.phoneNumber].join(" ").toLowerCase().includes(phrase)) || null;
}

async function performVoiceCommand(rawText) {
  const text = normalizeVoiceText(rawText);
  if (!text) return false;

  if (/(restoran|ro'yxat|royxat|pechlar|pech)/.test(text) && /(och|ko'rsat|show|ro'yxat|royxat)/.test(text)) {
    setView("restaurants");
    if (!restaurantsCache.length) await loadRestaurants();
    const first = restaurantsCache[0];
    if (first) {
      const row = document.querySelector(`#restaurant-list tr[data-business="${first.id}"]`);
      if (row) selectRestaurant(first, row);
    }
    aiSpeak("Restoranlar ro‘yxati ochildi.");
    return true;
  }

  if (/(restoran|pech)/.test(text) && /(qo'sh|yarat|qosh|yangi|create|add)/.test(text)) {
    setView("create");
    aiSpeak("Restoran yaratish formasi ochildi.");
    return true;
  }

  if (/(supermarket|market).*(qo'sh|yarat|yangi|create|add)/.test(text)) {
    setView("market-create");
    aiSpeak("Supermarket yaratish formasi ochildi.");
    return true;
  }

  if (/(supermarket|market).*(och|ko'rsat|show|ro'yxat|royxat)/.test(text)) {
    setView("markets");
    if (!marketsCache.length) await loadMarkets();
    const first = marketsCache[0];
    if (first) {
      const row = document.querySelector(`#market-list tr[data-business="${first.id}"]`);
      if (row) selectMarket(first, row);
    }
    aiSpeak("Supermarketlar ro‘yxati ochildi.");
    return true;
  }

  if (/(supermarket|market)\s*\d+|restoran\s*\d+/.test(text)) {
    const restaurant = findRestaurantByVoice(text);
    const market = findMarketByVoice(text);
    const target = restaurant || market;
    if (restaurant && !market) {
      setView("restaurants");
      await loadRestaurants();
      const row = document.querySelector(`#restaurant-list tr[data-business="${restaurant.id}"]`);
      if (row) selectRestaurant(restaurant, row);
      aiSpeak(`${restaurant.name} restorani tanlandi.`);
      return true;
    }
    if (market) {
      setView("markets");
      await loadMarkets();
      const row = document.querySelector(`#market-list tr[data-business="${market.id}"]`);
      if (row) selectMarket(market, row);
      aiSpeak(`${market.name} supermarketi tanlandi.`);
      return true;
    }
    if (target) {
      setView("restaurants");
      aiSpeak(`${target.name} tanlandi.`);
      return true;
    }
  }

  if (/(restoran|supermarket|pech|magazin).*(faollashtir|yoq|aktivlashtir|enable|activate)/.test(text)) {
    if (!selectedRestaurant && restaurantsCache.length) {
      const restaurant = findRestaurantByVoice(text) || restaurantsCache[0];
      if (restaurant) {
        const row = document.querySelector(`#restaurant-list tr[data-business="${restaurant.id}"]`);
        if (row) selectRestaurant(restaurant, row);
      }
    }
    if (!selectedRestaurant) {
      aiSpeak("Faollashtirish uchun avval restoran tanlang.");
      return true;
    }
    const owner = selectedRestaurant._owner;
    if (!owner) {
      aiSpeak("Tanlangan restoran ma’lumotlari yuklanmagan. Qayta urinib ko‘ring.");
      return true;
    }
    if (owner.isActive === false) {
      const selectedRow = document.querySelector("#restaurant-list .row-selected");
      const updated = await putJson("/Business/UpdateOwnerStatus", {
        businessId: Number(selectedRestaurant.id),
        isActive: true,
      });
      selectedRestaurant._owner = updated;
      renderRestaurantRows();
      updateSuperStats();
      if (selectedRow) selectRestaurant(selectedRestaurant, selectedRow);
      aiSpeak(`${selectedRestaurant.name} restorani faollashtirdim.`);
      return true;
    }
    aiSpeak(`${selectedRestaurant.name} restorani allaqachon faol.`);
    return true;
  }

  if (/(restoran|supermarket|pech|magazin).*(passiv|o'chir|yop|deactivate|disable)/.test(text)) {
    if (!selectedRestaurant && restaurantsCache.length) {
      const restaurant = findRestaurantByVoice(text) || restaurantsCache[0];
      if (restaurant) {
        const row = document.querySelector(`#restaurant-list tr[data-business="${restaurant.id}"]`);
        if (row) selectRestaurant(restaurant, row);
      }
    }
    if (!selectedRestaurant) {
      aiSpeak("Passiv qilish uchun avval restoran tanlang.");
      return true;
    }
    const owner = selectedRestaurant._owner;
    if (!owner) {
      aiSpeak("Tanlangan restoran ma’lumotlari yuklanmagan.");
      return true;
    }
    if (owner.isActive !== false) {
      const selectedRow = document.querySelector("#restaurant-list .row-selected");
      const updated = await putJson("/Business/UpdateOwnerStatus", {
        businessId: Number(selectedRestaurant.id),
        isActive: false,
      });
      selectedRestaurant._owner = updated;
      renderRestaurantRows();
      updateSuperStats();
      if (selectedRow) selectRestaurant(selectedRestaurant, selectedRow);
      aiSpeak(`${selectedRestaurant.name} restorani passiv qildim.`);
      return true;
    }
    aiSpeak(`${selectedRestaurant.name} restorani allaqachon passiv.`);
    return true;
  }

  if (/(qaysi|nimani|what).*(restoran|pech|supermarket)|((restoran|supermarket).*\?)/.test(text)) {
    const restaurant = selectedRestaurant || restaurantsCache[0];
    const market = selectedMarket || marketsCache[0];
    if (restaurant) {
      aiSpeak(`Tanlangan restoran: ${restaurant.name}.`);
      return true;
    }
    if (market) {
      aiSpeak(`Tanlangan supermarket: ${market.name}.`);
      return true;
    }
    aiSpeak("Hozir hech bir restoran tanlanmagan.");
    return true;
  }

  if (/\byordam\b|help|nima qila olasan|buyruq/.test(text)) {
    aiSpeak("Buyruqlar: restoranlar ro‘yxati, restoran qo‘sh, supermarketlar ro‘yxati, supermarket qo‘sh, tanlangan restoranni faollashtirish yoki passiv qilish, buni tugat, orqaga qaytish.");
    return true;
  }

  // "AI, buni tugat / yop / orqaga qayt" — asosiy ro'yxatga qaytish
  if (/(tugat|yop\b|orqaga)/.test(text)) {
    setView("restaurants");
    aiSpeak("Asosiy ro‘yxatga qaytdim. Buyruq bering.");
    return true;
  }

  // "AI, yordamchini och" — AI chat bo'limini ochish
  if (/yordamchi/.test(text) && /(och|ko'rsat|korsat|show)/.test(text)) {
    setView("ai");
    aiSpeak("AI yordamchi ochildi.");
    return true;
  }

  if (/^\d+$/.test(text)) {
    const idx = parseInt(text, 10) - 1;
    const view = getCurrentView();
    if (view === "restaurants" && restaurantsCache.length && idx >= 0 && idx < restaurantsCache.length) {
      const r = restaurantsCache[idx];
      const row = document.querySelector(`#restaurant-list tr[data-business="${r.id}"]`);
      if (row) selectRestaurant(r, row);
      aiSpeak(`${r.name} restorani tanlandi.`);
      return true;
    }
    if (view === "markets" && marketsCache.length && idx >= 0 && idx < marketsCache.length) {
      const m = marketsCache[idx];
      const row = document.querySelector(`#market-list tr[data-business="${m.id}"]`);
      if (row) selectMarket(m, row);
      aiSpeak(`${m.name} supermarketi tanlandi.`);
      return true;
    }
    aiSpeak("Bunday raqamda element topilmadi.");
    return true;
  }

  if (text.length >= 2) {
    const view = getCurrentView();
    if (view === "restaurants") {
      const r = findRestaurantByVoice(text);
      if (r) {
        await loadRestaurants();
        const row = document.querySelector(`#restaurant-list tr[data-business="${r.id}"]`);
        if (row) selectRestaurant(r, row);
        aiSpeak(`${r.name} restorani tanlandi.`);
        return true;
      }
    }
    if (view === "markets") {
      const m = findMarketByVoice(text);
      if (m) {
        await loadMarkets();
        const row = document.querySelector(`#market-list tr[data-business="${m.id}"]`);
        if (row) selectMarket(m, row);
        aiSpeak(`${m.name} supermarketi tanlandi.`);
        return true;
      }
    }
  }

  return false;
}

// ======================= Ovozli AI yordamchi =======================
const SpeechRec = window.SpeechRecognition || window.webkitSpeechRecognition;
const canMic = typeof SpeechRec !== "undefined";
const canSpeak = typeof window.speechSynthesis !== "undefined";
const aiMsgEl = $("sa-ai-message");

let aiRecognition = null;
let aiListening = false;
// Mikrofon tan olish tili (uz-UZ / ru-RU) — sahifa yangilanganda saqlanadi
let aiRecLang = localStorage.getItem("vk_ai_rec_lang") || "uz-UZ";
let aiNoSpeechRetried = false;
const aiLangBtn = $("sa-ai-lang");
function aiLangLabel() { return aiRecLang.toLowerCase().startsWith("ru") ? "ru" : "uz"; }
if (aiLangBtn) {
  aiLangBtn.textContent = aiLangLabel();
  aiLangBtn.addEventListener("click", () => {
    aiRecLang = aiLangLabel() === "uz" ? "ru-RU" : "uz-UZ";
    localStorage.setItem("vk_ai_rec_lang", aiRecLang);
    aiLangBtn.textContent = aiLangLabel();
    if (aiMsgEl) setMsg(aiMsgEl, "Mikrofon tili: " + (aiLangLabel() === "uz" ? "o‘zbekcha (uz)" : "ruscha (ru)"), "");
    // Jarvis doimiy eshitish yoqil bo'lsa — yangi til bilan qayta ishga tushiramiz
    if (aiAlwaysListen && aiRecognition) { try { aiRecognition.stop(); } catch { /* onend qayta ishga tushiradi */ } }
  });
}

// ======================= Jarvis doimiy eshitish rejimi =======================
// Jarvis switch yoqilganda mikrofon doim ochiq turadi. Foydalanuvchi 🎤 bosmasdan
// "AI, ..." yoki "Jarvis, ..." deb gapirsa — buyruq darhol bajariladi.
// Vaqf so'zisiz gaplar e'tiborga olinmaydi. Jarvis o'zi gapirayotganda mikrofon
// to'xtatiladi (o'z ovozini eshitib qolmasligi uchun).
let aiAlwaysListen = false; // Jarvis switch yoqilganmi (doimiy eshitish)
let aiSpeakingNow = false;  // Jarvis hozir ovoz chiqaryaptimi
let aiAlwaysErrors = 0;     // ketma-ket xatolar (cheksiz loop bo'lmasligi uchun)
let aiLastHeardCmd = "";    // so'nggi bajarilgan buyruq (takrorlanishni oldini olish)
let aiLastHeardAt = 0;      // so'nggi buyruq vaqti

// Mikrofonga ruxsatni aniq so'raymiz (SpeechRecognition ishga tushishi uchun
// brauzer aynan foydalanuvchi ish-harakatidan so'ng ruxsat olishi kerak).
// true — ruxsat bor; string — xato xabari.
async function requestMicPermission() {
  try {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return true;
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    stream.getTracks().forEach(t => t.stop());
    return true;
  } catch (err) {
    const name = err && err.name ? String(err.name) : "";
    if (/NotAllowed|Permission|denied|security/i.test(name))
      return "Mikrofonga ruxsat berilmagan. Brauzer manzil qatoridagi qulf belgisini bosib → 'Mikrofon' → 'Ruxsat' ni tanlang, keyin Ctrl+F5 bilan sahifani yangilang.";
    if (/NotFound|Devices?|source/i.test(name))
      return "Mikrofon qurilmasi topilmadi. Mikrofon ulanganini va Windows Sozlamalar → Tizim → Ovoz → Kirish bo‘limida standart qilib tanlanganini tekshiring.";
    if (/NotReadable|in.use/i.test(name))
      return "Mikrofon boshqa dasturda ishlatilyapti (masalan Zoom). Uni yoping va qayta urinib ko‘ring.";
    return "Mikrofonga ulanishda xatolik: " + (name || "noma'lum") + ". Qayta urinib ko‘ring.";
  }
}

function updateJarvisUi() {
  const wrap = document.getElementById("jarvis-wrap");
  if (!wrap) return;
  const small = wrap.querySelector(".jarvis-toggle__label small");
  if (small) small.textContent = aiAlwaysListen ? (aiListening ? "eshityapti 🔴" : "ulanmoqda...") : "AI buyruqlar";
  wrap.classList.toggle("jarvis-toggle--listening", aiAlwaysListen && aiListening);
}

async function startAlwaysListen() {
  if (!canMic) {
    if (aiMsgEl) setMsg(aiMsgEl, "Bu brauzer mikrofonni qo‘llab-quvvatlamaydi. Chrome yoki Edge’dan foydalaning.", "err");
    return;
  }
  if (aiAlwaysListen) return;
  const perm = await requestMicPermission();
  if (perm !== true) {
    if (aiMsgEl) setMsg(aiMsgEl, "Jarvis eshita olmaydi: " + perm, "err");
    return;
  }
  aiAlwaysListen = true;
  updateJarvisUi();
  startAlwaysRecognition();
}

function stopAlwaysListen() {
  aiAlwaysListen = false;
  aiAlwaysErrors = 0;
  if (aiRecognition) { try { aiRecognition.onend = null; aiRecognition.stop(); } catch { /* ignore */ } }
  aiRecognition = null;
  aiListening = false;
  updateJarvisUi();
}

// DIQQAT: Chrome’da `continuous = true` rejimda final (yakuniy) natijalar
// ishonchli yetib kelmaydi — gaplar interim holatda qotib qoladi. Shuning uchun
// biz `continuous = false` dan foydalanamiz (gap tugagach final natija keladi va
// onend yuz beradi), keyin Jarvis o'zini qayta ishga tushiradi. Bu — ishonchli usul.
function startAlwaysRecognition() {
  if (!aiAlwaysListen || !canMic || aiListening) return;
  aiRecognition = new SpeechRec();
  aiRecognition.lang = aiRecLang;
  aiRecognition.continuous = false;     // qat'iy rejim — ishonchli eshitish
  aiRecognition.interimResults = true;  // jonli ko'rsatkich uchun
  aiRecognition.maxAlternatives = 1;

  aiRecognition.onstart = () => {
    aiListening = true;
    aiAlwaysErrors = 0;
    updateJarvisUi();
  };

  aiRecognition.onresult = event => {
    let finalText = "";
    let interimText = "";
    for (let i = 0; i < event.results.length; i++) {
      const r = event.results[i];
      if (r.isFinal) finalText += r[0].transcript + " ";
      else interimText += r[0].transcript;
    }
    finalText = finalText.trim();

    // Jonli ko'rsatkich: brauzer nima eshitayotganini darhol ko'rsatamiz
    if (!finalText && interimText.trim() && !aiSpeakingNow) {
      if (aiMsgEl) setMsg(aiMsgEl, "🔴 Eshtyapti: “" + interimText.trim() + "”", "");
      return;
    }
    if (!finalText || aiSpeakingNow) return;

    // Bir xil buyruqning takrorlanmasligi (qisqa vaqt ichida ikki marta)
    const now = Date.now();
    if (finalText === aiLastHeardCmd && now - aiLastHeardAt < 4000) return;
    aiLastHeardCmd = finalText;
    aiLastHeardAt = now;

    handleAlwaysHeard(finalText);
  };

  aiRecognition.onerror = event => {
    const code = event && event.error ? String(event.error) : "";
    if (/language-not-supported/i.test(code) && aiLangLabel() !== "ru") {
      aiRecLang = "ru-RU";
      try { localStorage.setItem("vk_ai_rec_lang", aiRecLang); } catch { /* ignore */ }
      if (aiLangBtn) aiLangBtn.textContent = aiLangLabel();
      return; // onend qayta ishga tushiradi
    }
    if (/not-allowed|permission|denied|audio-capture|service-not-allowed/i.test(code)) {
      aiAlwaysErrors++;
      if (aiAlwaysErrors >= 2) {
        stopAlwaysListen();
        if (aiMsgEl) setMsg(aiMsgEl, "Doimiy eshitish to‘xtatildi: mikrofonga ruxsat yo‘q yoki mikrofon topilmadi. 🎤 tugmasi bilan bir martalik eshitish va matn yozish ishlaydi.", "err");
        return;
      }
    }
    // no-speech / network — jim o'tadi, onend qayta ishga tushiradi
  };

  aiRecognition.onend = () => {
    aiListening = false;
    updateJarvisUi();
    if (aiAlwaysListen && !aiSpeakingNow) {
      setTimeout(() => { if (aiAlwaysListen && !aiListening && !aiSpeakingNow) startAlwaysRecognition(); }, 250);
    }
  };

  try { aiRecognition.start(); }
  catch { setTimeout(() => { if (aiAlwaysListen && !aiListening && !aiSpeakingNow) startAlwaysRecognition(); }, 400); }
}

async function handleAlwaysHeard(text) {
  if (jarvisShouldRun(text)) {
    aiAddMessage("user", text);
    const done = await performVoiceCommand(text);
    if (done) { aiAddMessage("bot", "✅ Buyruq bajarildi."); return; }
    // Vaqf so'zi bor, lekin UI buyrug'i emas — AI savol sifatida javob beradi
    await askBackend(text);
  } else if (aiMsgEl) {
    setMsg(aiMsgEl, "🔴 Eshitildi: “" + text + "” — buyruq bo‘lishi uchun ‘AI’ yoki ‘Jarvis’ bilan boshlang.", "");
  }
}

// Sahifa yukilganda switch yoqil bo'lsa — Jarvisni ishga tushiramiz.
// DIQQAT: brauzer mikrofonga ruxsatni faqat foydalanuvchi ish-harakatidan keyin
// beradi, avtostart esa bloklanadi. Shuning uchun birinchi bosish/teginishda
// boshlaymiz.
if (isJarvisOn()) {
  const beginJarvis = () => {
    window.removeEventListener("pointerdown", beginJarvis);
    window.removeEventListener("keydown", beginJarvis);
    startAlwaysListen();
  };
  window.addEventListener("pointerdown", beginJarvis);
  window.addEventListener("keydown", beginJarvis);
  if (aiMsgEl) setMsg(aiMsgEl, "Jarvis yoqilgan — ishga tushishi uchun sahifani bir marta bosing (yoki istalgan tugmani bosing), keyin ‘AI, ...’ deb gapiring.", "");
}

// Chatda ko'rsatish uchun markdown belgilarni tozalash (qatorlar saqlanadi)
function mdDisplayClean(text) {
  return String(text || "")
    .replace(/```[a-zA-Z]*\n?/g, "")                // kod blok belgilari
    .replace(/`([^`]*)`/g, "$1")                    // inline kod
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")        // [havola](url) → matn
    .replace(/^#{1,6}\s*/gm, "")                    // sarlavha belgilari (#)
    .replace(/^\s*[-*•‣◦▪]{1,2}\s+/gm, "• ")        // ro'yxat belgisi (qalin '**' dan oldin!)
    .replace(/(\*\*\*|___)([^\*_]+?)\1/g, "$2")     // ***qalin kursiv***
    .replace(/(\*\*|__)([^\*_]+?)\1/g, "$2")        // **qalin**
    .replace(/(\*|_)([^\*_]+?)\1/g, "$2")           // *kursiv*
    .replace(/~~([^~]+)~~/g, "$1")                  // ~~o'chirilgan~~
    .trim();
}

// Vaqt matnini formatlash: 234 ms → "234 ms", 1500 ms → "1.5 s", 65000 ms → "1:05"
function fmtDuration(ms) {
  if (ms < 1000) return ms + " ms";
  if (ms < 60000) return (ms / 1000).toFixed(1).replace(/\.0$/, "") + " s";
  const m = Math.floor(ms / 60000);
  const s = Math.floor((ms % 60000) / 1000);
  return m + ":" + String(s).padStart(2, "0");
}

// Salvodagi sun'iy intellekt javobini chatga qo'shish.
// meta = { askMs, speakMs } — vaqt ko'rsatkichlari (faqat bot xabarlarida).
function aiAddMessage(who, text, meta) {
  const box = $("sa-ai-messages");
  if (!box) return;
  const div = document.createElement("div");
  div.className = "ai-msg " + (who === "user" ? "ai-msg--user" : "ai-msg--bot");
  const head = document.createElement("div");
  head.className = "ai-msg__head";
  const label = document.createElement("span");
  label.className = "ai-msg__label";
  label.textContent = who === "user" ? "Siz" : "AI";
  head.appendChild(label);
  if (meta) {
    const stats = document.createElement("span");
    stats.className = "ai-msg__stats";
    const parts = [];
    if (typeof meta.askMs === "number") parts.push("javob: " + fmtDuration(meta.askMs));
    if (typeof meta.speakMs === "number") parts.push("ovoz: " + fmtDuration(meta.speakMs));
    stats.textContent = parts.join(" · ");
    head.appendChild(stats);
  }
  const body = document.createElement("div");
  if (who === "user") body.textContent = text;
  else appendRichAiText(body, text);
  div.appendChild(head);
  div.appendChild(body);
  box.appendChild(div);
  box.scrollTop = box.scrollHeight;
}

// AI javobini boyitilgan ko'rinishda chizish: markdown jadval → HTML jadval,
// qolgan qatorlar → tozalangan matn.
function appendRichAiText(body, text) {
  const lines = String(text || "").replace(/\r/g, "").split("\n");
  let i = 0;
  const buf = [];
  const flush = () => {
    if (!buf.length) return;
    body.appendChild(document.createTextNode(buf.join("\n")));
    buf.length = 0;
  };
  while (i < lines.length) {
    if (isAiTableLine(lines[i])) {
      const rows = [];
      let j = i;
      while (j < lines.length && isAiTableLine(lines[j])) {
        if (!isAiTableSep(lines[j])) rows.push(lines[j]); // --- ajratgichni tashlab yuboramiz
        j++;
      }
      if (rows.length >= 2) {
        flush();
        body.appendChild(buildAiTable(rows));
        i = j;
        continue;
      }
    }
    buf.push(mdDisplayClean(lines[i]));
    i++;
  }
  flush();
}

function isAiTableLine(line) {
  return /^\s*\|.+\|/.test(String(line || ""));
}

function isAiTableSep(line) {
  const s = String(line || "");
  return /^\s*\|?[\s:|-]+\|?\s*$/.test(s) && s.includes("-");
}

function buildAiTable(rows) {
  const table = document.createElement("table");
  table.className = "ai-table";
  rows.forEach((row, idx) => {
    const tr = document.createElement("tr");
    const cells = row.trim().replace(/^\|/, "").replace(/\|$/, "").split("|");
    cells.forEach(cell => {
      const el = document.createElement(idx === 0 ? "th" : "td");
      el.textContent = mdDisplayClean(cell).trim();
      tr.appendChild(el);
    });
    table.appendChild(tr);
  });
  return table;
}

// --- Ovoz tanlash: faqat ayol ovozlar (o'zbek va rus) ---
let aiVoice = null;
function pickFemaleVoice() {
  if (!window.speechSynthesis) return null;
  const voices = window.speechSynthesis.getVoices() || [];
  const preferredNames = ["Madina", "Svetlana", "Zira", "Aynur", "Artemida", "Microsoft", "Google"];
  const allFemale = voices.filter(v => {
    const name = (v.name || "").toLowerCase();
    const lang = (v.lang || "").toLowerCase();
    return /(madina|svetlana|zira|aynur|female|woman|ayol)/i.test(name + " " + lang)
      || preferredNames.some(item => name.includes(item.toLowerCase()));
  });
  if (allFemale.length) return allFemale[0];
  return voices.find(v => /(uz|ru)/i.test(v.lang || "")) || voices[0] || null;
}

if (window.speechSynthesis) {
  aiVoice = pickFemaleVoice();
  window.speechSynthesis.onvoiceschanged = () => { aiVoice = pickFemaleVoice(); };
}

// Matnni ovozli o'qish uchun tozalash (markdown, emoji, kod, maxsus belgilarni olib tashlash → "imlo xatolari" yo'qoladi)
function speechClean(text) {
  return String(text || "")
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/`([^`]*)`/g, "$1")
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/\(https?:\/\/[^)\s]+\)/g, "")
    .replace(/https?:\/\/\S+/g, "")
    .replace(/^#{1,6}\s*/gm, "")
    .replace(/[*_~]{1,}/g, " ")
    .replace(/^\s*[-•▪◦‣]\s*/gm, ". ")
    .replace(/^\s*(\d+)[.):]\s*/gm, ". ")
    .replace(/^[\s|:+\-]+$/gm, " ")
    .replace(/-{2,}/g, " ")
    .replace(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}\u{2B00}-\u{2BFF}\u{FE0F}\u{2190}-\u{21FF}\u{2500}-\u{257F}\u{25A0}-\u{25FF}\u{2700}-\u{27BF}\u{1F680}]/gu, " ")
    .replace(/[│┃||]{1,}/g, " ")
    .replace(/[ \u00A0\u200B\u3000]+/g, " ")
    .replace(/\s*\n+\s*/g, ". ")
    .replace(/\s*([;:])\s*/g, ". ")
    .replace(/\.{2,}/g, ".")
    .replace(/\s{2,}/g, " ")
    .replace(/\s+([.,!?])/g, "$1")
    .replace(/([.,!?])([A-Za-z\u0400-\u04FF])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
}

// Uzoq javobni o'zbeka ovozga bo'laklash (har bir bo'lak ~180 belgi, gaplar kesishmasin)
function splitSpeechChunks(text, max) {
  const sentences = text.split(/(?<=[.!?…])\s+/);
  const chunks = [];
  let cur = "";
  for (const raw of sentences) {
    const piece = String(raw || "").trim();
    if (!piece) continue;
    if (cur && (cur + " " + piece).length > max) { chunks.push(cur); cur = piece; }
    else cur = cur ? cur + " " + piece : piece;
  }
  if (cur) chunks.push(cur);
  // Hali ham uzun bo'lak bo'lsa (juda uzun so'zlar) — kesib tashlaymiz
  const out = [];
  for (const chunk of chunks) {
    if (chunk.length <= max) { out.push(chunk); continue; }
    for (let i = 0; i < chunk.length; i += max) out.push(chunk.slice(i, i + max));
  }
  return out;
}

let aiAudioEl = null;      // yagona audio pleer
let aiSpeakAbort = null;   // joriy ovozni to'xtatish (yangi savol berilsa)

function playAudioBlob(blob, signal) {
  return new Promise((resolve, reject) => {
    if (!aiAudioEl) aiAudioEl = new Audio();
    const url = URL.createObjectURL(blob);
    const audio = aiAudioEl;
    let settled = false;
    let timer = null;
    const cleanup = () => {
      URL.revokeObjectURL(url);
      audio.onended = null;
      audio.onerror = null;
      audio.onloadeddata = null;
      if (timer) { clearTimeout(timer); timer = null; }
    };
    const finish = (err) => {
      if (settled) return;
      settled = true;
      cleanup();
      signal.removeEventListener("abort", onAbort);
      if (err) reject(err); else resolve();
    };
    const onAbort = () => {
      try { audio.pause(); } catch { /* ignore */ }
      finish(new DOMException("aborted", "AbortError"));
    };
    signal.addEventListener("abort", onAbort, { once: true });
    audio.onended = () => finish();
    audio.onerror = () => finish(new Error("audio playback"));
    // Har yangi bo'lakdan oldin eski listenerlarni tozalab, yangi url o'rnatamiz.
    audio.pause();
    audio.removeAttribute("src");
    audio.load();
    audio.src = url;
    audio.load();
    const start = () => {
      try { audio.currentTime = 0; } catch { /* ignore */ }
      const p = audio.play();
      if (p && typeof p.then === "function") {
        p.catch(err => finish(err));
      }
      timer = setTimeout(() => finish(), 30000);
    };
    if (audio.readyState >= 2) start();
    else audio.onloadeddata = start;
  });
}

function selectedAiVoice(text) {
  const hasCyr = /[А-Яа-яЁё]/.test(String(text || ""));
  return "&voice=" + encodeURIComponent(hasCyr ? "ru-RU-SvetlanaNeural" : "uz-UZ-MadinaNeural");
}

// Javobni o'qish: birinchi navbatda Edge TTS (backend proxy) — haqiqiy o'zbekcha
// nervli ovoz (uz-UZ-MadinaNeural); ishlamasa brauzerning speechSynthesis ovozi zaxira sifatida.
async function aiSpeak(text) {
  const toggle = $("ai-voice-toggle");
  if (toggle && !toggle.checked) return;
  if (aiSpeakAbort) aiSpeakAbort.abort(); // oldingi ovozni to'xtatamiz
  const control = new AbortController();
  aiSpeakAbort = control;

  const clean = speechClean(text);
  if (!clean) return;
  // Jarvis o'z ovozini eshitib qolmasligi uchun — gapirayotganda mikrofonni to'xtatamiz
  aiSpeakingNow = true;
  if (aiAlwaysListen && aiRecognition) {
    try { aiRecognition.onend = null; aiRecognition.stop(); } catch { /* ignore */ }
    aiListening = false;
    updateJarvisUi();
  }
  if (canSpeak) { try { window.speechSynthesis.cancel(); } catch { /* ignore */ } }
  if (aiAudioEl) { try { aiAudioEl.pause(); } catch { /* ignore */ } }

  const chunks = splitSpeechChunks(clean, 800);
  const fetchChunk = async chunk => {
    const doFetch = () =>
    fetch("/Query/SpeakSuper/speak-super?text=" + encodeURIComponent(chunk) + selectedAiVoice(chunk), {
        headers: superAdminToken ? { "X-Super-Admin-Token": superAdminToken } : {},
        signal: control.signal,
      }).then(res => {
        if (!res.ok) throw new Error("tts-status-" + res.status);
        return res.blob();
      });
    try {
      return await doFetch();
    } catch (err) {
      if (control.signal.aborted) throw err;
      // Server/Bing vaqtincha ishlamasa — bir marta qayta urinamiz
      return await doFetch();
    }
  };
  try {
    // Birinchi bo'lakni yuklaymiz; keyingisini hozirgi ovoz chalinayotgan
    // paytda oldindan yuklaymiz — bo'laklar orasida pauza qolmaydi.
    let nextPromise = fetchChunk(chunks[0]);
    for (let i = 0; i < chunks.length; i++) {
      if (control.signal.aborted) return;
      const blob = await nextPromise;
      nextPromise = i + 1 < chunks.length ? fetchChunk(chunks[i + 1]) : null;
      if (control.signal.aborted) return;
      await playAudioBlob(blob, control.signal);
    }
  } catch (error) {
    if (error && error.name === "AbortError") return;
    console.warn("AI ovoz yordamchi xatosi:", error);
    if (aiMsgEl) setMsg(aiMsgEl, "Ovoz xizmati javob bera olmadi. Qayta urinib ko'ring.", "err");
    // Zaxira: brauzer ovozi (eng oddiy speechSynthesis). Edge TTS ishlamasa,
    // foydalanuvchi hech bo'lmaganda brauzer ovozida eshitsin — ovoz sifati
    // pastroq, lekin yaxshiroq hech narsa yo'q.
    if (canSpeak) {
      try {
        if (aiAudioEl) { try { aiAudioEl.pause(); } catch { /* ignore */ } }
        const utter = new SpeechSynthesisUtterance(clean || "Javob topilmadi.");
        utter.lang = "uz-UZ";
        if (aiVoice) utter.voice = aiVoice;
        else {
          // Avval ayol/ru/uz ovozini topamiz — sifat yaxshiroq bo'ladi.
          const voices = window.speechSynthesis.getVoices() || [];
          utter.voice = voices.find(v => /(uz|ru)/i.test(v.lang || "")) || null;
        }
        utter.rate = 0.95;
        utter.pitch = 1;
        window.speechSynthesis.speak(utter);
      } catch { /* ignore */ }
    }
  } finally {
    // Ovoz tugadi — doimiy eshitishni qayta yoqamiz
    aiSpeakingNow = false;
    if (aiAlwaysListen) setTimeout(() => { if (aiAlwaysListen && !aiListening && !aiSpeakingNow) startAlwaysRecognition(); }, 400);
  }
}

// Savolni backendga yuborish va javobni ko'rsatish
async function aiAsk(question) {
  const q = String(question || "").trim();
  const input = $("sa-ai-input");
  if (input) input.value = "";
  if (!q) {
    if (aiMsgEl) setMsg(aiMsgEl, "Avval savolingizni yozing yoki mikrofonda gapiring.", "err");
    return;
  }
  aiAddMessage("user", q);

  // JARVIS rejimi: switch yoqilgan VA matnda "AI"/"Jarvis" so'zi bo'lsa — buyruq bajariladi.
  // Aks holda (oddiy savol) quyidagi backend AI javobiga o'tiladi — eski xatti-harakat buzilmaydi.
  if (jarvisShouldRun(q)) {
    const handled = await performVoiceCommand(q);
    if (handled) {
      if (aiMsgEl) setMsg(aiMsgEl, "", "");
      aiAddMessage("bot", "✅ Buyruq bajarildi.");
      return;
    }
  }

  await askBackend(q);
}

// Backend AI javobi (foydalanuvchi xabari chatga allaqachon qo'shilgan bo'ladi).
// Jarvis doimiy eshitish rejimida ham shu funksiya ishlatiladi (user xabari takrorlanmaydi).
async function askBackend(q) {
  if (aiMsgEl) setMsg(aiMsgEl, "AI javob kutilmoqda...", "");
  const askStart = performance.now();
  let meta = null;
  try {
    const res = await postJson(
      "/Query/AskSuperAdmin/ask-super",
      { question: q },
      { "X-Super-Admin-Token": superAdminToken }
    );
    const askMs = Math.round(performance.now() - askStart);
    if (aiMsgEl) setMsg(aiMsgEl, "", "");
    const answer = res && res.answer ? String(res.answer).trim() : "";
    if (answer) {
      // Ovoz vaqtini o'lchash: aiSpeak tugagach meta ni to'ldiramiz va
      // xabarni yangilaymiz.
      meta = { askMs, speakMs: null };
      aiAddMessage("bot", answer, meta);
      const lastBox = $("sa-ai-messages").lastElementChild;
      const lastStats = lastBox ? lastBox.querySelector(".ai-msg__stats") : null;
      const speakStart = performance.now();
      aiSpeak(answer).then(() => {
        const speakMs = Math.round(performance.now() - speakStart);
        if (lastStats) {
          meta.speakMs = speakMs;
          lastStats.textContent =
            "javob: " + fmtDuration(meta.askMs) + " · ovoz: " + fmtDuration(meta.speakMs);
        }
      }).catch(() => { /* ovoz xatosi — vaqt ko'rsatilmaydi */ });
    } else {
      aiAddMessage("bot", "AI javob bermadi. Serverda AI xizmati (Gemini) sozlanmagan bo‘lishi mumkin.",
        { askMs });
    }
  } catch (error) {
    if (aiMsgEl) setMsg(aiMsgEl, "", "");
    const askMs = Math.round(performance.now() - askStart);
    aiAddMessage("bot", "Xatolik: " + error.message, { askMs });
  }
}

// Mikrofon tugmasi
$("sa-ai-mic").addEventListener("click", () => {
  if (!canMic) return;
  if (aiAlwaysListen) {
    // Doimiy eshitish yoqil — qo'shimcha 🎤 bosish shart emas
    if (aiMsgEl) setMsg(aiMsgEl, "Jarvis doimiy eshityapti — 🎤 bosmasdan shunchaki ‘AI, ...’ deb gapiring.", "");
    return;
  }
  if (aiListening) {
    if (aiRecognition) aiRecognition.stop();
    aiListening = false;
    lockMicUi(false);
    if (aiMsgEl) setMsg(aiMsgEl, "", "");
    return;
  }
  aiNoSpeechRetried = false; // yangi urinish — qayta-urinish huquqi qayta beriladi
  startAiRecognition(false);
});

async function startAiRecognition(isRetry) {
  if (!isRetry) {
    const perm = await requestMicPermission();
    if (perm !== true) {
      lockMicUi(false);
      if (aiMsgEl) setMsg(aiMsgEl, perm, "err");
      return;
    }
  }
  aiRecognition = new SpeechRec();
  aiRecognition.lang = aiRecLang;
  aiRecognition.interimResults = true; // jonli ko'rsatkich: brauzer nima eshitayotganini darhol ko'rsatadi
  aiRecognition.continuous = false;
  aiRecognition.maxAlternatives = 1;
  aiRecognition.onstart = () => {
    aiListening = true; lockMicUi(true);
    if (aiMsgEl) setMsg(aiMsgEl, "🔴 Eshtyapti... gapiring (til: " + aiLangLabel() + ")", "");
  };
  aiRecognition.onresult = async event => {
    let finalText = "", interimText = "";
    for (let i = 0; i < event.results.length; i++) {
      const r = event.results[i];
      if (r.isFinal) finalText += r[0].transcript;
      else interimText += r[0].transcript;
    }
    const input = $("sa-ai-input");
    // Oraliq natija — foydalanuvchiga "meni eshityapti" degan jonli belgi
    if (!finalText.trim()) {
      if (input && interimText.trim()) input.value = interimText.trim();
      if (aiMsgEl && interimText.trim()) setMsg(aiMsgEl, "🔴 Eshtyapti: " + interimText.trim(), "");
      return;
    }
    finalText = finalText.trim();
    if (input) { input.value = finalText; input.focus(); }
    // Mikrofon: JARVIS yoqilgan + "AI"/"Jarvis" so'zi bo'lsa — buyruq bajariladi,
    // aks holda oddiy AI savoli sifatida yuboriladi.
    if (jarvisShouldRun(finalText)) {
      const done = await performVoiceCommand(finalText);
      if (done) return; // tasdiq ovozini performVoiceCommand o'zi aytadi
    }
    aiAsk(finalText);
  };
  aiRecognition.onerror = (event) => {
    aiListening = false; lockMicUi(false);
    const code = event && event.error ? String(event.error) : "";
    // Til qo'llanmasa — avtomatik rus tiliga o'tib qayta boshlash
    if (/language-not-supported/i.test(code) && aiLangLabel() !== "ru") {
      aiRecLang = "ru-RU";
      localStorage.setItem("vk_ai_rec_lang", aiRecLang);
      if (aiLangBtn) aiLangBtn.textContent = aiLangLabel();
      if (aiMsgEl) setMsg(aiMsgEl, "uz-UZ tili qo‘llanmadi — ruscha (ru) tanlandi. Yana gapiring.", "err");
      startAiRecognition(true);
      return;
    }
    // Ovoz eshitilmadi — bir marta avtomatik qayta urinish
    if (/no-speech/i.test(code) && !isRetry && !aiNoSpeechRetried) {
      aiNoSpeechRetried = true;
      if (aiMsgEl) setMsg(aiMsgEl, "Ovoz eshitilmadi — yana eshitishga urinilyapti, gapiring...", "err");
      startAiRecognition(true);
      return;
    }
    let msg = "Mikrofon ishlamadi. Qaytadan urinib ko‘ring.";
    if (/not-allowed|permission|denied/i.test(code))
      msg = "Mikrofonga ruxsat berilmagan. Brauzer manzil qatoridagi qulf belgisini bosib, 'Mikrofon → Ruxsat berish'ni tanlang, so‘ng qayta gapiring.";
    else if (/no-speech/i.test(code))
      msg = "Ovoz eshitilmadi. 1) 🎤 tugmasini bosib DARHOL gapira boshlang. 2) Windows: Sozlamalar → Tizim → Ovoz → Kirish (Input) bo‘limida to‘g‘ri mikrofon tanlanganini va ovoz darajasi yuqori ekanini tekshiring (siz gapirayotgan mikrofon standart bo‘lishi kerak). 3) Klaviatura orqali yozib ham so‘rashingiz mumkin.";
    else if (/audio-capture/i.test(code))
      msg = "Mikrofon qurilmasi topilmadi. Kompyuterga mikrofon ulanganini tekshiring.";
    else if (/language-not-supported/i.test(code))
      msg = "Tanlangan mikrofon tili qo‘llanmaydi. 🎤 yonidagi uz/ru tugmasi bilan tilni o‘zgartiring.";
    else if (/audio|service|network/i.test(code))
      msg = "Ovoz xizmati vaqtincha ishlamayapti (mikrofon tan olish uchun internet kerak). Qayta urinib ko‘ring.";
    if (aiMsgEl) setMsg(aiMsgEl, msg, "err");
  };
  aiRecognition.onend = () => { aiListening = false; lockMicUi(false); };
  try { aiRecognition.start(); } catch { /* ignore */ }
}

function lockMicUi(listening) {
  const mic = $("sa-ai-mic");
  if (!mic) return;
  mic.textContent = listening ? "🔴" : "🎤";
  mic.classList.toggle("btn-primary", listening);
}

// Brauzer mikrofonni qo'llab-quvvatlamasa — tugma o'chiriladi
if (!canMic) {
  const mic = $("sa-ai-mic");
  if (mic) { mic.disabled = true; mic.title = "Brauzer mikrofonni qo‘llab-quvvatlamaydi"; }
}

// Yuborish va Enter
$("sa-ai-send").addEventListener("click", () => aiAsk($("sa-ai-input").value));
$("sa-ai-input").addEventListener("keydown", event => {
  if (event.key === "Enter") { event.preventDefault(); aiAsk($("sa-ai-input").value); }
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
