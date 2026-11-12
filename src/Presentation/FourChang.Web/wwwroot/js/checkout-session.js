// Kết thúc bản thao tác cục bộ sau khi máy chủ xác minh đã thu đủ tiền.
// Không tạo đơn, không xác nhận thanh toán và không xóa giỏ khách đang chọn cho lần tiếp theo.
(() => {
  "use strict";
  const key = "fourchang.checkout-intent.v1", lockName = "fourchang.checkout-create.v1";
  let running;

  /** Chỉ bỏ đúng thao tác có biên nhận trùng đơn đã thanh toán, dưới cùng khóa với lệnh tạo đơn. */
  async function releasePaidIntent() {
    if (!navigator.locks) return false;
    try {
      return await navigator.locks.request(lockName, async () => {
        const raw = localStorage.getItem(key);
        if (!raw) return false;
        const intent = JSON.parse(raw), id = intent?.receipt?.maDonHang;
        if (intent?.version !== 1 || !intent.command?.operation || !Number.isSafeInteger(id) || id <= 0) return false;
        const response = await fetch(`/Checkout/Payment?handler=State&orderId=${id}`, {
          credentials: "same-origin", cache: "no-store", headers: { Accept: "application/json" },
          signal: AbortSignal.timeout(5000)
        });
        if (!response.ok || response.redirected) return false;
        const state = await response.json();
        // Endpoint kiểm tra phiên khách và quyền sở hữu đơn. Trạng thái đơn đơn thuần không chứng minh đã thu tiền.
        if (state.orderId !== id || state.paid !== true || localStorage.getItem(key) !== raw) return false;
        localStorage.removeItem(key);
        window.dispatchEvent(new Event("checkout:session-reset"));
        return true;
      });
    } catch {
      // Không đọc được nguồn hoặc storage: giữ nguyên mã chống trùng để thử lại an toàn.
      return false;
    }
  }

  /** Gộp các yêu cầu từ tải trang/đổi tab/polling, tránh truy vấn chồng chéo. */
  function check() {
    if (!running) running = releasePaidIntent().finally(() => { running = null; });
    return running;
  }
  window.FourChangCheckoutSession = { check };
  window.addEventListener("pageshow", check);
  window.addEventListener("storage", event => { if (event.key === key || event.key === null) check(); });
  document.addEventListener("visibilitychange", () => { if (!document.hidden) check(); });
  // Khi khách giữ Checkout mở và trả tiền ở thiết bị/tab khác, tự chuẩn bị cho lần đặt tiếp theo.
  const timer = document.getElementById("checkout-proceed") ? setInterval(() => { if (!document.hidden) check(); }, 5000) : null;
  window.addEventListener("pagehide", () => { if (timer) clearInterval(timer); }, { once: true });
  check();
})();
