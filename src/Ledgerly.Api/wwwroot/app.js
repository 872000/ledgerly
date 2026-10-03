// Ledgerly dashboard — vanilla JS, no dependencies. Talks to the ASP.NET Core API.
const state = { year: new Date().getFullYear(), month: new Date().getMonth() + 1 };

const $ = (id) => document.getElementById(id);
const money = (n) => (n < 0 ? "-" : "") + "$" + Math.abs(n).toFixed(2);

async function api(path, options) {
  const res = await fetch(path, options);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || ("HTTP " + res.status));
  }
  return res.status === 204 ? null : res.json();
}

function shiftMonth(delta) {
  let { year, month } = state;
  month += delta;
  if (month < 1) { month = 12; year--; }
  if (month > 12) { month = 1; year++; }
  state.year = year; state.month = month;
  refresh();
}

function drawDailyChart(daily) {
  const canvas = $("dailyChart");
  const ctx = canvas.getContext("2d");
  const W = canvas.width, H = canvas.height;
  ctx.clearRect(0, 0, W, H);
  if (!daily.length) return;

  const max = Math.max(1, ...daily.map((d) => Math.max(d.income, d.expenses)));
  const n = daily.length;
  const groupW = W / n;
  const barW = Math.max(2, groupW * 0.32);

  daily.forEach((d, i) => {
    const x = i * groupW + groupW / 2;
    const ih = (d.income / max) * (H - 30);
    const eh = (d.expenses / max) * (H - 30);
    ctx.fillStyle = "#34d399";
    ctx.fillRect(x - barW - 1, H - 20 - ih, barW, ih);
    ctx.fillStyle = "#f87171";
    ctx.fillRect(x + 1, H - 20 - eh, barW, eh);
  });

  ctx.fillStyle = "#94a3b8";
  ctx.font = "10px system-ui";
  ctx.fillText("$" + max.toFixed(0), 4, 12);
}

function renderBudgets(categories) {
  const el = $("budgets");
  el.innerHTML = "";
  const budgeted = categories.filter((c) => c.budgeted > 0);
  if (!budgeted.length) {
    el.innerHTML = '<p class="hint">No budgets set for this month yet. Use the API: POST /api/budgets { categoryId, year, month, amount }.</p>';
    return;
  }
  for (const c of budgeted) {
    const pct = Math.min(100, c.percentUsed);
    const row = document.createElement("div");
    row.className = "budget-row";
    row.innerHTML =
      '<div class="top"><span>' + escapeHtml(c.categoryName) + '</span>' +
      "<span>" + money(c.spent) + " / " + money(c.budgeted) + " (" + c.percentUsed.toFixed(0) + "%)</span></div>" +
      '<div class="bar' + (c.percentUsed > 100 ? " over" : "") + '"><div style="width:' + pct + '%"></div></div>';
    el.appendChild(row);
  }
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

async function refresh() {
  const { year, month } = state;
  $("monthLabel").textContent = new Date(year, month - 1).toLocaleString("en", { month: "long", year: "numeric" });

  const summary = await api("/api/reports/monthly?year=" + year + "&month=" + month);
  $("incomeVal").textContent = money(summary.income);
  $("expenseVal").textContent = money(summary.expenses);
  $("netVal").textContent = money(summary.net);
  $("netVal").style.color = summary.net >= 0 ? "#34d399" : "#f87171";
  drawDailyChart(summary.daily);
  renderBudgets(summary.categories);

  const txs = await api("/api/transactions?year=" + year + "&month=" + month);
  const tbody = $("txTable").querySelector("tbody");
  tbody.innerHTML = "";
  for (const t of txs) {
    const tr = document.createElement("tr");
    tr.innerHTML =
      "<td>" + escapeHtml(t.date.slice(0, 10)) + "</td>" +
      "<td>" + escapeHtml(t.description) + "</td>" +
      "<td>" + escapeHtml(t.category) + "</td>" +
      '<td class="num ' + (t.amount >= 0 ? "pos" : "neg") + '">' + money(t.amount) + "</td>" +
      '<td><button class="del" data-id="' + t.id + '" aria-label="Delete">&times;</button></td>';
    tbody.appendChild(tr);
  }
  tbody.querySelectorAll(".del").forEach((btn) =>
    btn.addEventListener("click", async () => {
      await api("/api/transactions/" + btn.dataset.id, { method: "DELETE" });
      refresh();
    })
  );
}

async function loadCategories() {
  const cats = await api("/api/categories");
  const select = $("txCategory");
  select.innerHTML = "";
  for (const c of cats) {
    const opt = document.createElement("option");
    opt.value = c.id;
    opt.textContent = c.name + " (" + c.kind + ")";
    select.appendChild(opt);
  }
}

$("prevMonth").addEventListener("click", () => shiftMonth(-1));
$("nextMonth").addEventListener("click", () => shiftMonth(1));

$("txForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  await api("/api/transactions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      date: $("txDate").value,
      description: $("txDesc").value,
      amount: parseFloat($("txAmount").value),
      categoryId: $("txCategory").value
    })
  });
  $("txDesc").value = "";
  $("txAmount").value = "";
  refresh();
});

$("csvForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const file = $("csvFile").files[0];
  if (!file) return;
  const form = new FormData();
  form.append("file", file);
  try {
    const result = await api("/api/transactions/import", { method: "POST", body: form });
    $("csvMsg").textContent = "Imported " + result.imported + " transactions.";
    refresh();
  } catch (err) {
    $("csvMsg").textContent = "Import failed: " + err.message;
  }
});

// Seed the date picker with today, generate any due recurring transactions, then load.
$("txDate").value = new Date().toISOString().slice(0, 10);
api("/api/recurring/run", { method: "POST" })
  .catch(() => {})
  .finally(async () => { await loadCategories(); await refresh(); });
