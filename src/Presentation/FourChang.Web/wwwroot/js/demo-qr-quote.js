/* UC-WEB-07: illustrative price from a fresh backend quote, never a payment amount.
 * No order is created and no QR is generated from or linked to the price.
 */
(() => {
  "use strict";
  const amount = document.getElementById("demo-cart-quoted-total");
  const status = document.getElementById("demo-cart-quote-status");
  if (!amount || !status) return;
  const KEY = "fourchang.demo-qr-cart.v1";
  const CART = "fourchang.cart-draft.v1";
  const csrf = document.querySelector('#demo-quote-antiforgery input[name="__RequestVerificationToken"]')?.value;
  const money = n => new Intl.NumberFormat("vi-VN").format(n) + "đ";
  const showMissing = () => {
    amount.textContent = "Chưa có báo giá cho giỏ";
    status.textContent = "Hãy mở Checkout, chọn Mang đi, chờ Backend báo giá rồi nhấn Xem QR mô phỏng. QR không thu tiền.";
  };

  (async () => {
    let request;
    try {
      request = JSON.parse(sessionStorage.getItem(KEY) || "null");
      if (request?.version !== 1 || !request.payload ||
          request.payload.hinhThucNhanMon !== "TAKEAWAY" ||
          !Array.isArray(request.payload.items) ||
          request.payload.items.length < 1 || request.payload.items.length > 50 ||
          request.cartRaw !== (localStorage.getItem(CART) || "[]") || !csrf) {
        showMissing();
        return;
      }
    } catch {
      showMissing();
      return;
    }

    // A displayed quote must also disappear if the draft changes AFTER the
    // server response. Other tabs emit `storage`; edits in this tab may emit
    // `cart:changed`. Pageshow/visibility catches edits made while suspended.
    let invalidated = false;
    const checkCurrentCart = () => {
      if (invalidated) return false;
      try {
        if (request.cartRaw === (localStorage.getItem(CART) || "[]")) return true;
      } catch { /* Storage unavailable: fail closed. */ }
      invalidated = true;
      showMissing();
      return false;
    };
    window.addEventListener("storage", event => {
      if (event.key === CART || event.key === null) checkCurrentCart();
    });
    window.addEventListener("cart:changed", checkCurrentCart);
    window.addEventListener("pageshow", checkCurrentCart);
    document.addEventListener("visibilitychange", () => {
      if (!document.hidden) checkCurrentCart();
    });

    amount.textContent = "Đang hỏi lại Backend…";
    status.textContent = "Đang báo giá lại giỏ mang đi (chỉ đọc, không đặt hàng).";
    try {
      const signal = typeof AbortSignal.timeout === "function"
        ? AbortSignal.timeout(10000) : undefined;
      const res = await fetch("/Checkout?handler=Quote", {
        method: "POST", credentials: "same-origin", cache: "no-store",
        headers: {
          "Content-Type": "application/json",
          "Accept": "application/json",
          "RequestVerificationToken": csrf
        },
        body: JSON.stringify(request.payload), signal
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const price = await res.json();
      if (![price?.tienHang, price?.phiGiaoHang, price?.tongCong]
          .every(n => Number.isSafeInteger(n) && n >= 0) ||
          price.phiGiaoHang !== 0 || price.tongCong !== price.tienHang) {
        throw new Error("Báo giá Backend không hợp lệ");
      }
      // Discard a response if the cart was edited during the request.
      if (!checkCurrentCart()) return;
      amount.textContent = money(price.tongCong);
      status.textContent = "Giá giỏ mang đi do Backend vừa báo; không có đơn hàng hoặc yêu cầu thanh toán. Mã QR bên dưới không chứa số tiền.";
    } catch {
      if (invalidated) return;
      amount.textContent = "Chưa có báo giá cho giỏ";
      status.textContent = "Không kiểm tra được giá giỏ từ Backend. Không hiển thị giá cố định hoặc coi QR là thanh toán.";
    }
  })();
})();
