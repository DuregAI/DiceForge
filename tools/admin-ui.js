"use strict";
const $ = id => document.getElementById(id);
let players = [];
let selected = null;
let resetting = false;
let feedback = [];
let feedbackLoaded = false;
const csrf = document.querySelector('meta[name="csrf-token"]').content;

function cell(row, value) {
  const td = document.createElement("td");
  td.textContent = String(value);
  row.append(td);
  return td;
}
function render() {
  const search = $("search").value.trim().toLocaleLowerCase();
  const filtered = players.filter(p => `${p.player_name} ${p.player_guid}`.toLocaleLowerCase().includes(search));
  $("players").replaceChildren();
  for (const p of filtered) {
    const row = document.createElement("tr");
    const nameCell = cell(row, "");
    const name = document.createElement("span");
    name.className = "player-name";
    name.textContent = p.player_name || "Без имени";
    const id = document.createElement("span");
    id.className = "player-id";
    id.textContent = p.player_guid;
    nameCell.append(name, id);
    cell(row, p.current_level);
    cell(row, `${p.completed_levels} / ${p.total_levels}`);
    const seen = new Date(Number(p.updated_at_unix_ms_utc));
    cell(row, Number(p.updated_at_unix_ms_utc) > 0 && !Number.isNaN(seen.valueOf()) ? seen.toLocaleString("ru-RU") : "—");
    const button = document.createElement("button");
    button.textContent = "Сбросить прогресс";
    button.className = "reset-button";
    button.addEventListener("click", () => openReset(p));
    cell(row, "").append(button);
    $("players").append(row);
  }
  $("empty").hidden = filtered.length !== 0;
  $("empty").textContent = players.length ? "По этому запросу игроков не найдено." : "Игроков пока нет. Они появятся после подключения игры к базе.";
}
async function request(path, options) {
  const response = await fetch(path, {cache: "no-store", ...options});
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || "Не удалось выполнить запрос.");
  return data;
}
async function refresh() {
  $("refresh").disabled = true;
  $("status").className = "";
  $("status").textContent = "Загружаем игроков…";
  try {
    const data = await request("/api/players");
    players = data.players;
    players.sort((a, b) => Number(b.updated_at_unix_ms_utc) - Number(a.updated_at_unix_ms_utc));
    render();
    $("status").textContent = `Игроков: ${players.length} · Обновлено ${new Date().toLocaleTimeString("ru-RU")}`;
  } catch (error) {
    $("status").className = "error";
    $("status").textContent = error.message;
  } finally {
    $("refresh").disabled = false;
  }
}
function openReset(player) {
  if (resetting) return;
  selected = player;
  $("reset-player").textContent = `${player.player_name || "Без имени"} · ${player.player_guid}`;
  $("reset-error").hidden = true;
  $("reset-dialog").showModal();
  $("cancel-reset").focus();
}
$("cancel-reset").addEventListener("click", () => {if (!resetting) $("reset-dialog").close();});
$("reset-dialog").addEventListener("cancel", event => {if (resetting) event.preventDefault();});
$("confirm-reset").addEventListener("click", async () => {
  if (!selected || resetting) return;
  const target = selected;
  resetting = true;
  $("confirm-reset").disabled = true;
  $("cancel-reset").disabled = true;
  $("reset-error").hidden = true;
  try {
    await request("/api/reset", {method: "POST", headers: {"Content-Type": "application/json", "X-CSRF-Token": csrf}, body: JSON.stringify({identity: target.identity, expected_reset_epoch: target.reset_epoch, confirm: target.identity})});
    $("reset-dialog").close();
    await refresh();
    if (!$("status").classList.contains("error")) $("status").textContent = `Прогресс игрока «${target.player_name || "Без имени"}» сброшен.`;
  } catch (error) {
    $("reset-error").textContent = error.message;
    $("reset-error").hidden = false;
  } finally {
    resetting = false;
    $("confirm-reset").disabled = false;
    $("cancel-reset").disabled = false;
  }
});
$("refresh").addEventListener("click", refresh);
$("search").addEventListener("input", render);
function renderFeedback() {
  const search = $("feedback-search").value.trim().toLocaleLowerCase();
  const filtered = feedback.filter(f => [f.player_name, f.player_guid, f.category, f.message, f.scene_name, f.build_version].join(" ").toLocaleLowerCase().includes(search));
  $("feedback").replaceChildren();
  for (const f of filtered) {
    const row = document.createElement("tr");
    const player = cell(row, "");
    const name = document.createElement("span");
    name.className = "player-name";
    name.textContent = f.player_name || "Без имени";
    const id = document.createElement("span");
    id.className = "player-id";
    id.textContent = f.player_guid || "—";
    player.append(name, id);
    const content = cell(row, "");
    const category = document.createElement("span");
    category.className = "feedback-category";
    category.textContent = f.category || "Без категории";
    const message = document.createElement("p");
    message.className = "feedback-message";
    let body = f.message || "—";
    if (f.category === "rating") {
      try {
        const review = JSON.parse(f.message);
        if (Number.isInteger(review.rating) && review.rating >= 1 && review.rating <= 5 && typeof review.comment === "string") {
          category.textContent = "Оценка игры";
          body = `${"★".repeat(review.rating)}${"☆".repeat(5 - review.rating)} · ${review.rating} / 5\n${review.comment || "Без комментария"}`;
        }
      } catch (_) { /* Older messages remain readable as plain text. */ }
    }
    message.textContent = body;
    const context = document.createElement("span");
    context.className = "player-id";
    context.textContent = [f.scene_name, f.build_version].filter(Boolean).join(" · ");
    content.append(category, message, context);
    const sent = new Date(Number(f.created_at_unix_ms_utc));
    cell(row, Number(f.created_at_unix_ms_utc) > 0 && !Number.isNaN(sent.valueOf()) ? sent.toLocaleString("ru-RU") : "—");
    $("feedback").append(row);
  }
  $("feedback-empty").hidden = filtered.length !== 0;
  $("feedback-empty").textContent = feedback.length ? "По этому запросу отзывов не найдено." : "Отзывов пока нет. Здесь появятся сообщения, отправленные игроками из игры.";
}
async function refreshFeedback() {
  if ($("feedback-refresh").disabled) return;
  $("feedback-refresh").disabled = true;
  $("feedback-status").className = "";
  $("feedback-status").textContent = "Загружаем отзывы…";
  try {
    const data = await request("/api/feedback");
    feedback = data.feedback.sort((a, b) => Number(b.created_at_unix_ms_utc) - Number(a.created_at_unix_ms_utc));
    feedbackLoaded = true;
    renderFeedback();
    $("feedback-status").textContent = `Отзывов: ${feedback.length} · Сначала новые · Обновлено ${new Date().toLocaleTimeString("ru-RU")}`;
  } catch (error) {
    $("feedback-status").className = "error";
    $("feedback-status").textContent = error.message;
  } finally {
    $("feedback-refresh").disabled = false;
  }
}
function selectTab() {
  const showFeedback = location.hash === "#feedback";
  for (const key of ["players", "feedback"]) {
    const active = (key === "feedback") === showFeedback;
    $(`${key}-panel`).hidden = !active;
    $(`${key}-tab`).setAttribute("aria-selected", String(active));
    $(`${key}-tab`).tabIndex = active ? 0 : -1;
  }
  $("players-footer").hidden = showFeedback;
  $("page-title").textContent = showFeedback ? "Отзывы" : "Игроки";
  $("page-description").textContent = showFeedback ? "Сообщения игроков из беты в SpacetimeDB" : "Уровни и прогресс беты в SpacetimeDB";
  document.title = `GlimbleHop · ${showFeedback ? "Отзывы" : "Игроки"}`;
  if (showFeedback && !feedbackLoaded) refreshFeedback();
}
for (const key of ["players", "feedback"]) {
  $(`${key}-tab`).addEventListener("click", () => { location.hash = key; });
  $(`${key}-tab`).addEventListener("keydown", event => {
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    const target = event.key === "Home" ? "players" : event.key === "End" ? "feedback" : key === "players" ? "feedback" : "players";
    location.hash = target;
    $(`${target}-tab`).focus();
  });
}
$("feedback-search").addEventListener("input", renderFeedback);
$("feedback-refresh").addEventListener("click", refreshFeedback);
window.addEventListener("hashchange", selectTab);
selectTab();
refresh();
