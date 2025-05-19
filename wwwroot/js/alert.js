// alert.js – újrafelhasználható Bootstrap alert modul (ES module)

const ALERT_ID = "sharedAlertComponent";

function ensureAlertContainer() {
  if (!document.getElementById("alert-container")) {
    const container = document.createElement("div");
    container.id = "alert-container";
    container.className = "my-3 text-center";
    document.body.prepend(container);
  }
}

export function showAlert(message, type = "danger", options = {}) {
  ensureAlertContainer();

  const container = document.getElementById("alert-container");
  const existing = document.getElementById(ALERT_ID);
  if (existing) return; // csak egy alert engedélyezett egyszerre

  const alert = document.createElement("div");
  alert.id = ALERT_ID;
  alert.className = `alert alert-${type} ${options.dismissible ? "alert-dismissible fade show" : ""}`;
  alert.setAttribute("role", "alert");
  alert.innerHTML = message;

  if (options.dismissible) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "btn-close";
    btn.setAttribute("data-bs-dismiss", "alert");
    btn.setAttribute("aria-label", "Bezárás");
    alert.appendChild(btn);
  }

  container.appendChild(alert);

  if (options.timeout && typeof options.timeout === "number") {
    setTimeout(() => {
      alert.classList.remove("show");
      alert.classList.add("fade");
      setTimeout(() => alert.remove(), 300);
    }, options.timeout);
  }
}

export function clearAlert() {
  const existing = document.getElementById(ALERT_ID);
  if (existing) existing.remove();
}