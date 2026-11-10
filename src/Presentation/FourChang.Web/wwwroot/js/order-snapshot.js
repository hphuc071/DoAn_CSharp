// Full document reloads only: no asynchronous event merge, bearer token, financial POST or storage.
(() => {
  "use strict";
  const notice = document.getElementById("order-snapshot-connection");
  if (!notice) return;
  let disconnected = !navigator.onLine;
  let reloading = false;
  let wasHidden = document.hidden;
  // Hide a displayed QR at its own validated UTC deadline. This never expires an order,
  // releases stock, refreshes a QR, sends money or changes a payment status.
  document.querySelectorAll("[data-payment-qr]").forEach(qr => {
    const raw = qr.getAttribute("data-qr-expires-utc") || "";
    // Accept only ISO timestamps with an explicit timezone (Z or ±hh:mm).
    // DateTime/DateTimeOffset can serialize a UTC deadline in either form.
    const expiry = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw) ? Date.parse(raw) : NaN;
    function checkExpiry() {
      const remaining = expiry - Date.now();
      if (!Number.isFinite(remaining) || remaining <= 0) {
        qr.hidden = true;
        const message = document.getElementById("payment-qr-expiry");
        if (message) {
          message.hidden = false;
          message.textContent = "QR đã hết hiệu lực hoặc thiếu thời hạn xác minh. Không chuyển khoản theo mã này; tải lại trạng thái hoặc liên hệ hỗ trợ, không tự chuyển lại khi chưa rõ kết quả.";
        }
        return;
      }
      // Recheck against wall-clock time after timer suspension; cap delays for browser limits.
      window.setTimeout(checkExpiry, Math.min(remaining, 60000));
    }
    checkExpiry();
  });
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
