// Full document reloads only: no asynchronous event merge, bearer token, financial POST or storage.
(() => {
  "use strict";
  const notice = document.getElementById("order-snapshot-connection");
  if (!notice) return;
  let disconnected = !navigator.onLine;
  let reloading = false;
  let wasHidden = document.hidden;
  function stale() {
    notice.hidden = false;
    notice.textContent = "Mất kết nối hoặc bản trang cũ. Trạng thái chưa được xác nhận mới; nối mạng sẽ tải lại snapshot. Không chuyển khoản lại theo suy đoán.";
    document.querySelectorAll("[data-order-private]").forEach(element => { element.hidden = true; });
    document.querySelectorAll("[data-payment-qr]").forEach(element => { element.hidden = true; });
    document.querySelectorAll("[data-online-only]").forEach(element => { element.setAttribute("aria-disabled", "true"); });
  }
  function reload() {
    if (reloading) return;
    reloading = true;
    stale();
    window.location.reload();
  }
  window.addEventListener("offline", () => { disconnected = true; stale(); });
  window.addEventListener("online", () => { if (disconnected) reload(); });
  window.addEventListener("pageshow", event => { if (event.persisted) reload(); });
  document.addEventListener("visibilitychange", () => {
    if (document.hidden) { wasHidden = true; return; }
    if (!wasHidden) return;
    wasHidden = false;
    // An account may have changed in another tab. Do not merge data into the old document.
    if (navigator.onLine) reload();
    else { disconnected = true; stale(); }
  });
  document.addEventListener("click", event => {
    if (event.target.closest?.("[data-online-only][aria-disabled='true']")) event.preventDefault();
  });
  if (disconnected) stale();
})();
