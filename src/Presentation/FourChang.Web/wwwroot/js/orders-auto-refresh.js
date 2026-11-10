// UC-WEB-08: read-only snapshot polling, not a push/realtime stream.
// Navigating by GET reauthenticates the customer and rechecks ownership at the API.
(() => {
  "use strict";
  if (!document.querySelector("[data-order-auto-refresh]")) return;
  const readUrlLink = document.querySelector("[data-order-refresh-target]");
  if (!readUrlLink?.href) return;

  const intervalMs = 30000;
  let unsavedFormInput = false;
  function noteEdit(event) {
    if (event.target?.closest?.("form")) unsavedFormInput = true;
  }
  document.addEventListener("input", noteEdit);
  document.addEventListener("change", noteEdit);

  function refreshIfSafe() {
    // Do not discard a typed cancellation reason or an unapplied list filter.
    // order-snapshot.js already handles reconnecting and restoring a hidden tab.
    if (document.hidden || navigator.onLine === false || unsavedFormInput ||
        document.activeElement?.closest?.("form")) {
      window.setTimeout(refreshIfSafe, intervalMs);
      return;
    }
    // A reload of a POST response may resubmit a cancellation form.
    // Always navigate by GET to the server-rendered Orders read link.
    let target;
    try { target = new URL(readUrlLink.href, window.location.href); }
    catch { return; }
    if (target.origin !== window.location.origin || !/^https?:$/.test(target.protocol)) return;
    window.location.replace(target.href);
  }
  window.setTimeout(refreshIfSafe, intervalMs);
})();
