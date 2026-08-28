// Sahifa qaysi manzildan ochildi — API ham shu joyga ulanadi.
const API_BASE = location.protocol.startsWith("http")
  ? `${location.protocol}//${location.host}`
  : "http://localhost:55983";
const SESSION_KEY = "vk_owner_session";
const fallbackTableImage = "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=500&q=80";
const fallbackFoodImage = "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=700&q=80";

const VIEW_TITLES = {
  staff: ["Xodimlar", "Xodimlar ro‘yxati, profili va maosh tarixi."],
  tables: ["Stollar", "Restorandagi stollarni boshqaring."],
  meals: ["Ovqatlar", "Menyu va ichimliklarni boshqaring."],
  "staff-detail": ["Xodim profili", "Xodim ma’lumotlari va maosh tarixi."],
  "table-detail": ["Stol profili", "Stol ma’lumotlari va holati."],
  supermarket: ["Supermarket", "Mahsulotlar, narxlar va ombor qoldig‘i shu bo‘limda boshqariladi."],
  shop: ["Do‘kon", "Tovarlar, narxlar va sotuvlar shu bo‘limda boshqariladi."],
  organization: ["Tashkilot", "Umumiy tashkilot ma’lumotlari shu bo‘limda boshqariladi."],
};
const ROLE_NAMES = { 0: "Ega", 1: "Menejer", 2: "Kassir", 3: "Ofitsiant", 4: "Oshpaz" };
const ROLE_META = {
  0: { badge: "role-badge--owner", avatar: "avatar--owner" },
  1: { badge: "role-badge--manager", avatar: "avatar--manager" },
  2: { badge: "role-badge--cashier", avatar: "avatar--cashier" },
  3: { badge: "role-badge--waiter", avatar: "avatar--waiter" },
  4: { badge: "role-badge--cook", avatar: "avatar--cook" },
};
const STAFF_VIEWS = ["staff", "tables", "meals"];
// Restoran guruhiga tegishli barcha ko‘rinishlar (flyout bo‘limlari + ularning profillari)
const RESTAURANT_VIEWS = [...STAFF_VIEWS, "staff-detail", "table-detail"];

// ---------- Holat ----------
let ownerToken = "";
let activeBusinessId = "";
const tables = [];
const products = [];
const staff = [];
let staffFilter = "";
let staffRoleFilter = "all";
let staffStatusFilter = "all";
let staffSortKey = "name-asc";
let selectedStaffId = null;
let selectedRestaurantSub = "tables";
let selectedTableId = null;

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

function setMsg(el, text, state) {
  if (!el) return;
  el.textContent = text;
  el.className = "form-message" + (state ? " " + state : "");
}

function flash(text, isError) {
  const el = $("workspace-flash");
  if (!el) return;
  el.textContent = text;
  el.classList.toggle("err", Boolean(isError));
  el.hidden = false;
  clearTimeout(flash._t);
  flash._t = setTimeout(() => { el.hidden = true; }, 6000);
}

async function api(url, options = {}) {
  const headers = Object.assign({ "Content-Type": "application/json" }, options.headers || {});
  const response = await fetch(`${API_BASE}${url}`, Object.assign({}, options, { headers }));
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error((data && data.error) || "So‘rov bajarilmadi (" + response.status + ").");
  }
  return data;
}
// ---------- Sessiya boshqaruvi (sahifa yangilanganda ham holat saqlanadi) ----------
function saveSession(sess) {
  try { sessionStorage.setItem(SESSION_KEY, JSON.stringify(sess)); } catch { /* ignore */ }
}

function loadSession() {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch { return null; }
}

function clearSession() {
  try { sessionStorage.removeItem(SESSION_KEY); } catch { /* ignore */ }
}

function enterWorkspace(session) {
  ownerToken = session.token || "";
  activeBusinessId = String(session.businessId || "");
  $("entry-stage").hidden = true;
  $("owner-area").hidden = false;
  $("owner-restaurant-name").textContent = session.restaurantName || "Restoran";
  loadWorkspaceData();
  setOwnerView(selectedRestaurantSub || "tables");
}

function resetToEntry() {
  clearSession();
  location.reload();
}

// ---------- Auth: restoran egasi kirishi ----------
$("owner-login-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("login-message");
  setMsg(message, "Kirilmoqda...");
  try {
    const result = await api("/Business/LoginOwner/owner/login", {
      method: "POST",
      body: JSON.stringify({
        login: $("login-value").value.trim(),
        password: $("password-value").value,
      }),
    });
    const session = {
      token: result.accessToken,
      businessId: result.businessId,
      restaurantName: result.restaurantName,
    };
    saveSession(session);
    enterWorkspace(session);
  } catch (error) {
    setMsg(message, "Kirish amalga oshmadi: " + error.message, "err");
  }
});

// ---------- Ish maydoni ma'lumotlarini yuklash ----------
async function loadWorkspaceData() {
  tables.length = 0;
  products.length = 0;
  staff.length = 0;
  selectedTableId = null;
  $("table-detail-card").hidden = true;
  renderTables();
  renderProducts();
  renderStaff();
  await Promise.allSettled([loadTablesFromApi(), loadProductsFromApi(), loadStaffFromApi()]);
  loadTodayStats();
}

async function loadTablesFromApi() {
  try {
    const list = await api(`/Business/GetTables/${encodeURIComponent(activeBusinessId)}/tables`);
    (Array.isArray(list) ? list : []).forEach(table => {
      tables.push({ id: table.id, name: table.name, capacity: table.capacity, status: Number(table.status ?? 0), image: fallbackTableImage });
    });
  } catch (error) {
    flash("Stollarni yuklab bo‘lmadi: " + error.message, true);
  }
  renderTables();
}

async function loadProductsFromApi() {
  try {
    const list = await api(`/Product/GetByBusiness?businessId=${encodeURIComponent(activeBusinessId)}`);
    (Array.isArray(list) ? list : []).forEach(product => {
      const aliases = Array.isArray(product.aliases) ? product.aliases : [];
      products.push({
        id: product.id,
        name: product.name,
        price: product.price,
                kind: aliases.includes("drink") ? "drink"
            : aliases.includes("fruit") ? "fruit"
            : aliases.includes("dessert") ? "dessert"
            : "food",
        image: fallbackFoodImage,
        isAvailable: product.isAvailable !== false,
      });
    });
  } catch (error) {
    flash("Menyuni yuklab bo‘lmadi: " + error.message, true);
  }
  renderProducts();
}

// Xodimlar ro'yxatini API'dan olish (owner token bilan).
async function loadStaffFromApi() {
  staff.length = 0;
  selectedStaffId = null;
  $("staff-detail-card").hidden = true;
  try {
    const list = await api(`/Business/GetStaff/${encodeURIComponent(activeBusinessId)}/staff`, {
      headers: { "X-Owner-Token": ownerToken },
    });
    (Array.isArray(list) ? list : []).forEach(s => staff.push(s));
  } catch (error) {
    $("staff-list").innerHTML =
      `<tr><td colspan="8" style="text-align:center;color:var(--red)">${esc(error.message)}</td></tr>`;
    return;
  }
  renderStaff();
}

/// Bugungi kun statistikasi — backend Query hisobotidan olinadi.
async function loadTodayStats() {
  const salesEl = $("sales-today");
  const noteEl = $("orders-today-note");
  const now = new Date();
  const fromDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const toDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
  salesEl.textContent = "...";
  try {
    const summary = await api(
      `/Query/GetSummary/summary?businessId=${encodeURIComponent(activeBusinessId)}`
      + `&fromDate=${encodeURIComponent(fromDate.toISOString())}`
      + `&toDate=${encodeURIComponent(toDate.toISOString())}`);
    salesEl.textContent = fmtSum(summary.totalAmount) + " so‘m";
    noteEl.textContent = "buyurtmalar: " + (summary.orderCount ?? 0);
  } catch {
    salesEl.textContent = "—";
    noteEl.textContent = "buyurtmalar: —";
  }
}
// ---------- Ish maydoni navigatsiyasi ----------
function setOwnerView(view) {
  const isRestaurantView = RESTAURANT_VIEWS.includes(view);
  const restaurantGroup = document.querySelector(".nav-group[data-owner-group=\"restaurant\"]");
  const toggleBtn = restaurantGroup ? restaurantGroup.querySelector("[data-owner-toggle]") : null;
  // Bo'limga kirilganda flyout yopiladi, lekin "Restoran" tugmasi faol holatda qoladi
  if (restaurantGroup && isRestaurantView) restaurantGroup.classList.remove("open");
  if (toggleBtn) toggleBtn.classList.toggle("active", isRestaurantView);

  // Nav itemlarini belgilash
  document.querySelectorAll(".nav-item[data-owner-view]")
    .forEach(nav => nav.classList.toggle("active", nav.dataset.ownerView === view));

  // Panellarni ko'rsatish/yashirish (data-owner-panel vergul bilan bir nechta qiymat oladi)
  document.querySelectorAll("[data-owner-panel]").forEach(panel => {
    const keys = (panel.dataset.ownerPanel || "").split(/\s+/);
    panel.hidden = !keys.includes(view);
  });

  const pair = VIEW_TITLES[view] || (STAFF_VIEWS.includes(view) ? ["Restoran", ""] : [view, ""]);
  $("owner-view-title").textContent = pair[0];
  $("owner-view-subtitle").textContent = pair[1];
}

document.querySelectorAll(".nav-item[data-owner-view]").forEach(item => {
  item.addEventListener("click", () => setOwnerView(item.dataset.ownerView));
});

// "Restoran" — yon tomonga ochiladigan flyout: bosilganda ochiladi/yopiladi
document.querySelectorAll("[data-owner-toggle]").forEach(btn => {
  btn.addEventListener("click", event => {
    event.stopPropagation();
    const group = btn.closest(".nav-group");
    group.classList.toggle("open", !group.classList.contains("open"));
  });
});

// Flyout tashqarisiga bosilsa — yopiladi
document.addEventListener("click", event => {
  document.querySelectorAll(".nav-group.open").forEach(group => {
    if (!group.contains(event.target)) group.classList.remove("open");
  });
});

// Flyoutdagi Xodimlar/Stollar/Ovqatlar — qaysi biri bosilsa, shu bo'limga kiriladi
document.querySelectorAll(".nav-item.sub[data-owner-view]").forEach(item => {
  item.addEventListener("click", () => {
    selectedRestaurantSub = item.dataset.ownerView;
    setOwnerView(selectedRestaurantSub);
  });
});

// Birinchi ochishda oxirgi tanlangan bo'limni ko'rsatish
window.addEventListener("DOMContentLoaded", () => {
  if (!(document.getElementById("owner-area") || {}).hidden) setOwnerView(selectedRestaurantSub || "tables");
});

// Alohida sahifalardan (profil) orqaga qaytish — tepada va pastdagi tugmalar
document.querySelectorAll(".back-to-staff").forEach(btn => btn.addEventListener("click", () => setOwnerView("staff")));
document.querySelectorAll(".back-to-tables").forEach(btn => btn.addEventListener("click", () => setOwnerView("tables")));

// ---------- Logout ----------
$("logout-button").addEventListener("click", resetToEntry);

// ---------- Rasm tanlanganda ko'rinishi ----------
function previewFile(inputId, previewId) {
  const input = $(inputId);
  const preview = $(previewId);
  input.addEventListener("change", () => {
    const file = input.files[0];
    if (!file) return;
    preview.innerHTML = `<img src="${URL.createObjectURL(file)}" alt="Tanlangan rasm">`;
  });
}
previewFile("table-image", "table-preview");
previewFile("product-image", "product-preview");

// ---------- Chizish funksiyalari ----------
function tablePill(status) {
  if (status === 0) return ["Bo‘sh", "pill--ok"];
  if (status === 1) return ["Band", "pill--warn"];
  if (status === 2) return ["Tozalanmoqda", "pill--off"];
  return ["—", "pill--off"];
}

function renderTables() {
  $("tables-empty").hidden = tables.length > 0;
  $("table-count").textContent = String(tables.length);
  const badge = $("table-count-badge");
  if (badge) badge.textContent = String(tables.length);

  // Tablitsa ko'rinishida chizish: № | Stol | Sig'imi | Holati | Ko'rish
  $("table-items").innerHTML = tables.map((table, index) => {
    const [label, cls] = tablePill(table.status);
    const img = table.image || fallbackTableImage;
    return `<tr class="rowlink" data-table-id="${table.id}">
      <td class="table-num">${index + 1}</td>
      <td><div class="staff-cell">
        <img class="item-thumb" src="${img}" alt="${esc(table.name)}">
        <div class="staff-name"><strong>${esc(table.name)}</strong><small>ID: ${esc(String(table.id))}</small></div>
      </div></td>
      <td><strong>${esc(String(table.capacity || 0))}</strong> <span class="muted">kishilik</span></td>
      <td><span class="pill ${cls}">${label}</span></td>
      <td style="text-align:right;white-space:nowrap"><button class="btn btn-ghost btn-sm" type="button">Ko‘rish →</button></td>
    </tr>`;
  }).join("");

  // Qator bosilganda — stol profili alohida sahifada ochiladi
  document.querySelectorAll("#table-items tr").forEach(row => {
    row.addEventListener("click", () => {
      const table = findTableById(Number(row.dataset.tableId));
      if (table) selectTable(table);
    });
  });
}

function findTableById(id) {
  return tables.find(t => t.id === id);
}

function selectTable(table) {
  selectedTableId = table.id;

  const card = $("table-detail-card");
  card.hidden = false;
  $("table-detail-img").src = table.image || fallbackTableImage;
  $("table-detail-name").textContent = table.name || "Stol";
  $("table-detail-capacity").textContent = `${table.capacity || 0} kishilik`;
  $("table-status-select").value = String(Number(table.status ?? 0));

  const [label, cls] = tablePill(table.status);
  $("table-detail-grid").innerHTML = [
    ["Stol nomi", esc(table.name || "Stol")],
    ["Stol ID", esc(String(table.id))],
    ["Sig‘imi", `${table.capacity || 0} kishilik`],
    ["Holati", `<span class="pill ${cls}">${label}</span>`],
  ].map(([k, v]) => `<div class="detail-item"><small>${k}</small><strong>${v}</strong></div>`).join("");

  // Stol profili alohida sahifada ochiladi
  setOwnerView("table-detail");
}

function renderProducts() {
  const foods = products.filter(p => p.kind === "food");
  const drinks = products.filter(p => p.kind === "drink");
  const fruits = products.filter(p => p.kind === "fruit");
  const desserts = products.filter(p => p.kind === "dessert");
  $("products-empty").hidden = foods.length > 0;
  $("drinks-empty").hidden = drinks.length > 0;
  $("fruits-empty").hidden = fruits.length > 0;
  $("desserts-empty").hidden = desserts.length > 0;
  $("food-count").textContent = String(foods.length);
  $("drink-count").textContent = String(drinks.length);
  $("fruit-count").textContent = String(fruits.length);
  $("dessert-count").textContent = String(desserts.length);
  const rowHtml = product => `<div class="item">
      <img class="item-thumb" src="${product.image}" alt="">
      <div class="item-body"><strong>${esc(product.name)}</strong><small>${product.isAvailable ? "Mavjud" : "Vaqtincha yo‘q"}</small></div>
      <span class="item-end"><strong>${fmtSum(product.price)} so‘m</strong></span>
    </div>`;
  $("product-items").innerHTML = foods.map(rowHtml).join("");
  $("drink-items").innerHTML = drinks.map(rowHtml).join("");
  $("fruit-items").innerHTML = fruits.map(rowHtml).join("");
  $("dessert-items").innerHTML = desserts.map(rowHtml).join("");
}

function fmtDate(value) {
  if (!value) return "—";
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? "—" : d.toLocaleDateString("uz-UZ");
}

function staffPill(person) {
  if (person.isActive === false) return ["Bo‘shatilgan", "pill--bad"];
  return ["Ishlayapti", "pill--ok"];
}

function renderStaffStats() {
  const total = staff.length;
  const activeList = staff.filter(s => s.isActive !== false);
  const active = activeList.length;
  const fired = total - active;
  const payroll = staff.reduce((sum, s) => sum + Number(s.monthlySalary || 0), 0);
  const avgSalary = active ? Math.round(activeList.reduce((sum, s) => sum + Number(s.monthlySalary || 0), 0) / active) : 0;
  $("staff-total").textContent = String(total);
  $("staff-active").textContent = String(active);
  $("staff-fired").textContent = String(fired);
  $("staff-payroll").textContent = fmtSum(payroll) + " so‘m";
  $("staff-avg-salary").textContent = fmtSum(avgSalary) + " so‘m";
}

// Xodim ismidan bosh harflar (avatar uchun): "Ali Aliyev" -> "AA".
function initialsOf(person) {
  const parts = String(person.fullName || [person.firstName, person.lastName].filter(Boolean).join(" ") || "")
    .trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return "?";
  return ((parts[0][0] || "") + (parts[1] ? parts[1][0] : "")).toUpperCase();
}

function dateMs(value) {
  if (!value) return 0;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? 0 : d.getTime();
}

// Ish staji — ishga kirgandan to bugunga (yoki bo'shatilganga qadar).
function staffStaj(person) {
  const start = person.hireDate ? new Date(person.hireDate) : null;
  if (!start || Number.isNaN(start.getTime())) return "—";
  const end = (person.isActive === false && person.firedAt) ? new Date(person.firedAt) : new Date();
  if (Number.isNaN(end.getTime()) || end < start) return "—";
  let months = (end.getFullYear() - start.getFullYear()) * 12 + (end.getMonth() - start.getMonth());
  if (end.getDate() < start.getDate()) months -= 1;
  if (months < 1) {
    const days = Math.max(1, Math.floor((end - start) / 86400000));
    return days + " kun";
  }
  const years = Math.floor(months / 12);
  const rest = months % 12;
  if (years >= 1) return years + " yil" + (rest > 0 ? " " + rest + " oy" : "");
  return rest + " oy";
}

// Qidiruv + lavozim + holat filtrlari va saralash natijasi.
function getFilteredStaff() {
  const query = staffFilter.trim().toLowerCase();
  const list = staff.filter(s => {
    if (staffStatusFilter === "active" && s.isActive === false) return false;
    if (staffStatusFilter === "fired" && s.isActive !== false) return false;
    if (staffRoleFilter !== "all" && String(s.role) !== staffRoleFilter) return false;
    if (!query) return true;
    return [s.fullName, s.firstName, s.lastName, s.phoneNumber, ROLE_NAMES[s.role]]
      .filter(Boolean).join(" ").toLowerCase().includes(query);
  });

  const byName = (a, b, dir) => dir * String(a.fullName || "").localeCompare(String(b.fullName || ""), "uz");
  switch (staffSortKey) {
    case "name-desc": return [...list].sort((a, b) => byName(a, b, -1));
    case "salary-desc": return [...list].sort((a, b) => Number(b.monthlySalary || 0) - Number(a.monthlySalary || 0));
    case "salary-asc": return [...list].sort((a, b) => Number(a.monthlySalary || 0) - Number(b.monthlySalary || 0));
    case "hire-desc": return [...list].sort((a, b) => dateMs(b.hireDate) - dateMs(a.hireDate));
    case "hire-asc": return [...list].sort((a, b) => dateMs(a.hireDate) - dateMs(b.hireDate));
    default: return [...list].sort((a, b) => byName(a, b, 1));
  }
}

function emptyStaffHtml() {
  const filtered = staff.length > 0;
  return '<div class="staff-empty">'
    + '<span class="staff-empty__icon">' + (filtered ? "🔍" : "👤") + "</span>"
    + "<strong>" + (filtered ? "Mos xodim topilmadi" : "Ro‘yxat bo‘sh") + "</strong>"
    + "<p>" + (filtered
      ? "Qidiruv yoki filtrlarni o‘zgartirib ko‘ring."
      : "Hali xodim qo‘shilmagan. “＋ Xodim qo‘shish” tugmasini bosing.") + "</p>"
    + "</div>";
}

function renderStaff() {
  renderStaffStats();
  const list = getFilteredStaff();

  const tbody = $("staff-list");
  const badge = $("staff-count-badge");
  if (badge) badge.textContent = String(list.length) + " / " + String(staff.length);

  if (!list.length) {
    tbody.innerHTML = '<tr><td colspan="8">' + emptyStaffHtml() + "</td></tr>";
    return;
  }

  tbody.innerHTML = "";
  list.forEach(person => {
    const [label, cls] = staffPill(person);
    const meta = ROLE_META[person.role] || {};
    const row = document.createElement("tr");
    row.className = "rowlink";
    row.__staffId = person.id;
    if (person.id === selectedStaffId) row.classList.add("row-selected");
    row.innerHTML =
      `<td><div class="staff-cell">
        <span class="avatar ${esc(meta.avatar || "")}">${esc(initialsOf(person))}</span>
        <span class="staff-name"><strong>${esc(person.fullName || "—")}</strong><small>Ishga kirgan: ${fmtDate(person.hireDate)}</small></span>
      </div></td>`
      + `<td>${esc(person.phoneNumber || "-")}</td>`
      + `<td><span class="role-badge ${esc(meta.badge || "")}">${ROLE_NAMES[person.role] || "Xodim"}</span></td>`
      + `<td>${person.age ? esc(person.age) : "—"}</td>`
      + `<td class="salary-cell"><strong>${fmtSum(person.monthlySalary)}</strong> <span class="muted">so‘m</span></td>`
      + `<td><span class="staj-cell"><span>${staffStaj(person)}</span></span></td>`
      + `<td><span class="pill ${cls}">${label}</span></td>`
      + '<td><button class="btn btn-ghost btn-sm row-action" type="button">Ko‘rish</button></td>';
    row.addEventListener("click", () => selectStaff(person));
    tbody.appendChild(row);
  });
}

function selectStaff(person) {
  selectedStaffId = person.id;
  // highlighted row
  document.querySelectorAll("#staff-list .rowlink").forEach(row => {
    row.classList.toggle("row-selected", row.__staffId === person.id);
  });

  const card = $("staff-detail-card");
  card.hidden = false;
  $("staff-detail-name").textContent = person.fullName || "Xodim";
  const meta = ROLE_META[person.role] || {};
  const avatar = $("staff-detail-avatar");
  avatar.textContent = initialsOf(person);
  avatar.className = "avatar avatar--lg " + (meta.avatar || "");
  $("staff-detail-role").innerHTML = `<span class="role-badge ${esc(meta.badge || "")}">${ROLE_NAMES[person.role] || "Xodim"}</span>`;

  const [label, cls] = staffPill(person);
  const fireBtn = $("staff-fire-btn");
  fireBtn.textContent = person.isActive === false ? "Qayta faollashtirish" : "Ishdan bo‘shatish";

  $("staff-detail-grid").innerHTML = [
    ["Ism", esc(person.firstName || "—")],
    ["Familiya", esc(person.lastName || "—")],
    ["Yosh", person.age ? `${person.age} yosh` : "—"],
    ["Telefon", esc(person.phoneNumber || "—")],
    ["Lavozim", ROLE_NAMES[person.role] || "—"],
    ["Ishga kirgan", fmtDate(person.hireDate)],
    ["Hozirgi oylik", fmtSum(person.monthlySalary) + " so‘m"],
    ["Holati", `<span class="pill ${cls}">${label}</span>` + (person.firedAt ? `<small>Bo‘shatilgan: ${fmtDate(person.firedAt)}</small>` : "")],
  ].map(([k, v]) => `<div class="detail-item"><small>${k}</small><strong>${v}</strong></div>`).join("");

  $("new-salary").value = person.monthlySalary || 0;
  $("sal-reason").value = "";
  $("salary-message").textContent = "";
  $("staff-salary-form").hidden = true;

  renderSalaryHistory(person);

  // Xodim profili alohida sahifada ochiladi
  setOwnerView("staff-detail");
}

function renderSalaryHistory(person) {
  const tbody = $("salary-history");
  const history = person.salaryHistory || [];
  if (!history.length) {
    tbody.innerHTML =
      '<tr><td colspan="4" style="text-align:center;color:var(--muted)">Hali maosh o‘zgarishi yo‘q.</td></tr>';
    return;
  }
  tbody.innerHTML = history.map(h => `
    <tr>
      <td>${fmtDate(h.changedAt)}</td>
      <td>${fmtSum(h.oldSalary)} so‘m</td>
      <td><strong>${fmtSum(h.newSalary)} so‘m</strong></td>
      <td>${esc(h.reason || "—")}</td>
    </tr>`).join("");
}

function findStaffById(id) {
  return staff.find(s => s.id === id);
}

function requireOwner() {
  if (ownerToken && activeBusinessId) return true;
  flash("Avval restoran egasi sifatida tizimga kiring.", true);
  return false;
}
// ---------- CRUD: Stol ----------
$("table-form").addEventListener("submit", event => {
  event.preventDefault();
  const name = $("table-name").value.trim();
  const capacity = Number($("table-capacity").value);
  const preview = document.querySelector("#table-preview img");
  if (!name || !capacity) return;
  createTable(name, capacity, preview ? preview.src : fallbackTableImage, event.target);
});

async function createTable(name, capacity, image, form) {
  if (!requireOwner()) return;
  try {
    const saved = await api("/Business/CreateTable/tables", {
      method: "POST",
      headers: { "X-Owner-Token": ownerToken },
      body: JSON.stringify({ businessId: Number(activeBusinessId), name, capacity }),
    });
    tables.push({
      id: saved.id,
      name: saved.name || name,
      capacity: saved.capacity || capacity,
      status: Number(saved.status ?? 0),
      image,
    });
    form.reset();
    $("table-preview").innerHTML = "<span>Stol rasmi</span>";
    renderTables();
    flash("Stol qo‘shildi.");
    } catch (error) {
    flash("Stolni saqlab bo‘lmadi: " + error.message, true);
  }
}

// Stol holatini (status) saqlash — detail kartadagi "Saqlash" tugmasi
$("table-status-save").addEventListener("click", async () => {
  const person = findTableById(selectedTableId);
  if (!person || !requireOwner()) return;
  const newStatus = Number($("table-status-select").value);
  try {
    await api(`/Business/tables/${person.id}/status`, {
      method: "PUT",
      headers: { "X-Owner-Token": ownerToken },
      body: JSON.stringify({ status: newStatus }),
    });
    person.status = newStatus;
    renderTables();
    selectTable(person);
    flash("Stol holati yangilandi.");
  } catch (error) {
    flash("Holatni yangilab bo‘lmadi: " + error.message, true);
  }
}); 
// ---------- CRUD: Mahsulot (taom/ichimlik) ----------
$("product-form").addEventListener("submit", event => {
  event.preventDefault();
  const name = $("product-name").value.trim();
  const price = Number($("product-price").value);
  const kind = $("product-kind").value;
  const preview = document.querySelector("#product-preview img");
  if (!name || price < 0) return;
  createProduct(name, price, kind, preview ? preview.src : fallbackFoodImage, event.target);
});

async function createProduct(name, price, kind, image, form) {
  if (!requireOwner()) return;
  try {
    const saved = await api("/Product/Create", {
      method: "POST",
      headers: { "X-Owner-Token": ownerToken },
      body: JSON.stringify({ businessId: Number(activeBusinessId), name, price, unit: "dona", aliases: [kind] }),
    });
    products.push({
      id: saved.id,
      name: saved.name || name,
      price: saved.price ?? price,
      kind,
      image,
      isAvailable: saved.isAvailable !== false,
    });
    form.reset();
    $("product-preview").innerHTML = "<span>Taom rasmi</span>";
    renderProducts();
        flash(kind === "drink" ? "Ichimlik menyuga qo‘shildi."
          : kind === "fruit" ? "Meva menyuga qo‘shildi."
          : kind === "dessert" ? "Shirinlik menyuga qo‘shildi."
          : "Taom menyuga qo‘shildi.");
  } catch (error) {
    flash("Mahsulotni saqlab bo‘lmadi: " + error.message, true);
  }
}

// ---------- CRUD: Xodimlar ----------
// Holat bo'yicha filtrlash chiplari (Barchasi / Faol / Bo'shatilgan)
document.querySelectorAll("#staff-status-chips .chip").forEach(chip => {
  chip.addEventListener("click", () => {
    staffStatusFilter = chip.dataset.staffStatus || "all";
    document.querySelectorAll("#staff-status-chips .chip").forEach(c => c.classList.toggle("active", c === chip));
    renderStaff();
  });
});

// Lavozim bo'yicha filtrlash
$("staff-role-filter").addEventListener("change", event => {
  staffRoleFilter = event.target.value;
  renderStaff();
});

// Saralash
$("staff-sort").addEventListener("change", event => {
  staffSortKey = event.target.value;
  renderStaff();
});

// Filtrlangan ro'yxatni CSV fayl sifatida yuklab olish (Excel uchun UTF-8 BOM bilan).
$("staff-export-btn").addEventListener("click", () => {
  const list = getFilteredStaff();
  if (!list.length) {
    flash("Eksport qilish uchun mos xodim yo‘q.", true);
    return;
  }
  const csvEscape = value => String(value ?? "").replace(/;/g, ",").replace(/\r?\n/g, " ");
  const lines = [["Ism", "Telefon", "Lavozim", "Yosh", "Oylik (so‘m)", "Ishga kirgan", "Staj", "Holat"]
    .join(";")];
  list.forEach(person => {
    lines.push([
      person.fullName || "",
      person.phoneNumber || "",
      ROLE_NAMES[person.role] || "Xodim",
      person.age || "",
      String(Number(person.monthlySalary || 0)),
      fmtDate(person.hireDate),
      staffStaj(person),
      person.isActive === false ? "Bo‘shatilgan" : "Ishlayapti",
    ].map(csvEscape).join(";"));
  });
  const blob = new Blob(["\uFEFF" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "xodimlar.csv";
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
  flash("Xodimlar ro‘yxati CSV ko‘rinishida yuklab olindi.");
});

$("staff-add-btn").addEventListener("click", () => {
  const form = $("staff-create-form");
  form.hidden = !form.hidden;
  if (!form.hidden) $("staff-first").focus();
});

$("staff-create-cancel").addEventListener("click", () => {
  $("staff-create-form").hidden = true;
});

$("staff-search").addEventListener("input", event => {
  staffFilter = event.target.value;
  renderStaff();
});

$("staff-create-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("staff-create-message");
  if (!requireOwner()) return;
  setMsg(message, "Saqlanmoqda...");
  try {
    const hireRaw = $("staff-hired").value;
    const saved = await api("/Business/CreateStaff/staff", {
      method: "POST",
      headers: { "X-Owner-Token": ownerToken },
      body: JSON.stringify({
        businessId: Number(activeBusinessId),
        firstName: $("staff-first").value.trim(),
        lastName: $("staff-last").value.trim(),
        phoneNumber: $("staff-phone2").value.trim(),
        age: $("staff-age").value ? Number($("staff-age").value) : null,
        monthlySalary: Number($("staff-salary").value || 0),
        hireDate: hireRaw ? new Date(hireRaw + "T00:00:00").toISOString() : null,
        role: Number($("staff-role").value),
      }),
    });
    staff.push(savedToRow(saved));
    event.target.reset();
    event.target.hidden = true;
    renderStaff();
    setMsg(message, "Xodim qo‘shildi.", "ok");
    flash("Xodim qo‘shildi.");
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});

function savedToRow(saved) {
  return {
    id: saved.id,
    businessId: saved.businessId,
    fullName: saved.fullName || [saved.firstName, saved.lastName].filter(Boolean).join(" "),
    firstName: saved.firstName,
    lastName: saved.lastName,
    phoneNumber: saved.phoneNumber,
    role: saved.role,
    isActive: saved.isActive !== false,
    age: saved.age,
    monthlySalary: saved.monthlySalary,
    hireDate: saved.hireDate,
    firedAt: saved.firedAt,
    salaryHistory: saved.salaryHistory || [],
  };
}

// Maosh o'zgartirish
$("staff-salary-btn").addEventListener("click", () => {
  $("staff-salary-form").hidden = !$("staff-salary-form").hidden;
  if (!$("staff-salary-form").hidden) $("new-salary").focus();
});

$("salary-cancel").addEventListener("click", () => {
  $("staff-salary-form").hidden = true;
});

$("staff-salary-form").addEventListener("submit", async event => {
  event.preventDefault();
  const message = $("salary-message");
  const person = findStaffById(selectedStaffId);
  if (!person || !requireOwner()) return;
  const newSalary = Number($("new-salary").value);
  if (newSalary < 0) { setMsg(message, "Yangi oylik manfiy bo‘lishi mumkin emas.", "err"); return; }
  setMsg(message, "Saqlanmoqda...");
  try {
    const updated = await api(`/Business/staff/${person.id}/salary`, {
      method: "PUT",
      headers: { "X-Owner-Token": ownerToken },
      body: JSON.stringify({ newSalary, reason: $("sal-reason").value.trim() || null }),
    });
    const idx = staff.indexOf(person);
    if (idx >= 0) staff[idx] = savedToRow(updated);
    event.target.hidden = true;
    renderStaff();
    selectStaff(staff[idx]);
    setMsg(message, "Maosh o‘zgartirildi.", "ok");
    flash("Maosh tarixga yozildi.");
  } catch (error) {
    setMsg(message, error.message, "err");
  }
});

// Ishdan bo'shatish / qayta faollashtirish
$("staff-fire-btn").addEventListener("click", async () => {
  const person = findStaffById(selectedStaffId);
  if (!person || !requireOwner()) return;
  const makeActive = person.isActive === false;
  const confirmed = window.confirm(
    makeActive ? "Bu xodimni qayta ishga olamanmi?" : "Bu xodimni ishdan bo‘shatib qo‘yamanmi?"
  );
  if (!confirmed) return;
  try {
    const updated = await api(`/Business/staff/${person.id}/active`, {
      method: "PUT",
      headers: { "X-Owner-Token": ownerToken },
      body: JSON.stringify({ isActive: makeActive }),
    });
    const idx = staff.indexOf(person);
    if (idx >= 0) staff[idx] = savedToRow(updated);
    renderStaff();
    selectStaff(staff[idx]);
    flash(makeActive ? "Xodim qayta faollashtirildi." : "Xodim ishdan bo‘shatildi.");
  } catch (error) {
    flash(error.message, true);
  }
});

// ---------- Boshlash: sessiyani tiklash ----------
const existingSession = loadSession();
if (existingSession && existingSession.token) enterWorkspace(existingSession, true);
