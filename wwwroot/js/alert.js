const ALERT_ID = "sharedAlertComponent";

export function showAlert(message, type = "danger", options = {}) {
  if (!$("#alert-container").length)
    $("body").prepend('<div id="alert-container" class="my-3 text-center"></div>');

  if ($(`#${ALERT_ID}`).length) return;

  const dismiss = options.dismissible ? 'alert-dismissible fade show' : '';
  const closeBtn = options.dismissible
    ? `<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Bezárás"></button>`
    : '';

  const alertHtml = `
    <div id="${ALERT_ID}" class="alert alert-${type} ${dismiss}" role="alert">
      ${message}
      ${closeBtn}
    </div>
  `;

  $("#alert-container").append(alertHtml);

  if (options.timeout)
    setTimeout(() => $(`#${ALERT_ID}`).alert('close'), options.timeout);
}

export function clearAlert() {
  $(`#${ALERT_ID}`).remove();
}
