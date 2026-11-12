// Chỉ đọc trạng thái đã lưu; trang này không POST hoặc tự coi việc mở/quét QR là thu tiền.
(() => {
  "use strict";
  const panel = document.querySelector("[data-simulation-payment]");
  if (!panel) return;
  let stopped = false;
  window.addEventListener("pagehide", () => { stopped = true; });
  async function poll() {
    if (stopped) return;
    try {
      const response = await fetch(`/Checkout/Payment?handler=State&orderId=${encodeURIComponent(panel.dataset.orderId)}`, { cache: "no-store", headers: { Accept: "application/json" }, signal: AbortSignal.timeout(5000) });
      if (!response.ok || response.redirected) throw new Error();
      const state = await response.json();
      if (typeof state.status === "string" && state.status !== "PENDING_PAYMENT") { location.reload(); return; }
      document.getElementById("simulation-status").textContent = "Đang chờ xác nhận mô phỏng…";
    } catch { document.getElementById("simulation-status").textContent = "Chưa đọc được trạng thái. Đang thử lại; không cần tạo thanh toán khác."; }
    if (!stopped) setTimeout(poll, 2000);
  }
  setTimeout(poll, 2000);
})();
