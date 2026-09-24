const byId = (id) => document.getElementById(id);
let currentTicket = null;
let editingId = null;

async function request(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { "Content-Type": "application/json", ...options.headers },
  });
  if (response.status === 204) return null;
  const body = await response.json();
  if (!response.ok) {
    const errors = body.errors
      ? Object.values(body.errors).flat().join(" ")
      : null;
    throw new Error(
      errors || body.error || "The request could not be completed.",
    );
  }
  return body;
}

function showError(error) {
  byId("error").textContent = error.message;
  byId("error").hidden = false;
}

function showView(view) {
  for (const name of ["list", "form", "detail"]) {
    byId(`${name}-view`).hidden = name !== view;
  }
  byId("error").hidden = true;
}

async function loadTickets() {
  const query = new URLSearchParams({
    search: byId("search").value,
    priority: byId("filter-priority").value,
    status: byId("filter-status").value,
  });
  const tickets = await request(`/api/tickets?${query}`);
  const rows = byId("ticket-rows");
  rows.replaceChildren();
  for (const ticket of tickets) {
    const row = document.createElement("tr");
    const title = document.createElement("td");
    const link = document.createElement("a");
    link.href = `#tickets/${ticket.id}`;
    link.textContent = ticket.title;
    title.append(link);
    row.append(title);
    for (const value of [ticket.priority, ticket.status]) {
      const cell = document.createElement("td");
      cell.textContent = value;
      row.append(cell);
    }
    rows.append(row);
  }
  byId("result-count").textContent = `${tickets.length} ticket(s)`;
  byId("empty-list").hidden = tickets.length !== 0;
}

function openForm(ticket = null) {
  editingId = ticket?.id || null;
  byId("form-heading").textContent = ticket ? "Edit ticket" : "New ticket";
  byId("ticket-title").value = ticket?.title || "";
  byId("ticket-description").value = ticket?.description || "";
  byId("ticket-priority").value = ticket?.priority || "Normal";
  byId("title-error").hidden = true;
  byId("ticket-title").removeAttribute("aria-invalid");
  showView("form");
}

async function loadDetail(id) {
  currentTicket = await request(`/api/tickets/${id}`);
  byId("detail-title").textContent = currentTicket.title;
  byId("detail-description").textContent = currentTicket.description;
  byId("detail-priority").textContent = currentTicket.priority;
  byId("detail-status").textContent = currentTicket.status;
  byId("start-work").hidden = currentTicket.status !== "Open";
  byId("resolve").hidden = currentTicket.status !== "InProgress";
  await loadComments();
  showView("detail");
}

async function loadComments() {
  const comments = await request(`/api/tickets/${currentTicket.id}/comments`);
  byId("comments").replaceChildren();
  for (const comment of comments) {
    const item = document.createElement("li");
    item.textContent = comment.body;
    byId("comments").append(item);
  }
}

async function changeStatus(status, button) {
  button.disabled = true;
  try {
    await request(`/api/tickets/${currentTicket.id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ status }),
    });
    await loadDetail(currentTicket.id);
  } catch (error) {
    showError(error);
  } finally {
    button.disabled = false;
  }
}

async function route() {
  const hash = location.hash;
  if (hash === "#new") {
    openForm();
  } else if (hash.startsWith("#tickets/")) {
    await loadDetail(hash.slice("#tickets/".length));
  } else {
    showView("list");
    await loadTickets();
  }
}

byId("filters").addEventListener("submit", async (event) => {
  event.preventDefault();
  try {
    await loadTickets();
  } catch (error) {
    showError(error);
  }
});

byId("ticket-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const title = byId("ticket-title").value.trim();
  if (title.length === 0 || title.length > 120) {
    byId("title-error").hidden = false;
    byId("ticket-title").setAttribute("aria-invalid", "true");
    byId("ticket-title").focus();
    return;
  }
  const button = event.submitter;
  button.disabled = true;
  try {
    const ticket = await request(
      editingId ? `/api/tickets/${editingId}` : "/api/tickets",
      {
        method: editingId ? "PUT" : "POST",
        body: JSON.stringify({
          title,
          description: byId("ticket-description").value,
          priority: byId("ticket-priority").value,
        }),
      },
    );
    if (location.hash === `#tickets/${ticket.id}`) await loadDetail(ticket.id);
    else location.hash = `#tickets/${ticket.id}`;
  } catch (error) {
    showError(error);
  } finally {
    button.disabled = false;
  }
});

byId("edit").addEventListener("click", () => openForm(currentTicket));
byId("start-work").addEventListener("click", (event) =>
  changeStatus("InProgress", event.currentTarget),
);
byId("resolve").addEventListener("click", (event) =>
  changeStatus("Resolved", event.currentTarget),
);
byId("delete").addEventListener("click", () =>
  byId("delete-dialog").showModal(),
);
byId("cancel-delete").addEventListener("click", () =>
  byId("delete-dialog").close(),
);
byId("confirm-delete").addEventListener("click", async () => {
  try {
    await request(`/api/tickets/${currentTicket.id}`, { method: "DELETE" });
    byId("delete-dialog").close();
    location.hash = "#tickets";
  } catch (error) {
    byId("delete-dialog").close();
    showError(error);
  }
});
byId("comment-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = event.submitter;
  button.disabled = true;
  try {
    await request(`/api/tickets/${currentTicket.id}/comments`, {
      method: "POST",
      body: JSON.stringify({ body: byId("comment-body").value }),
    });
    byId("comment-body").value = "";
    await loadComments();
  } catch (error) {
    showError(error);
  } finally {
    button.disabled = false;
  }
});
byId("cancel").addEventListener("click", async () => {
  try {
    if (editingId) await loadDetail(editingId);
    else location.hash = "#tickets";
  } catch (error) {
    showError(error);
  }
});
window.addEventListener("hashchange", () => route().catch(showError));
route().catch(showError);
