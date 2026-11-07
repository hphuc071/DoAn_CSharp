/* One durable checkout operation per origin, serialized across tabs with Web Locks.
 * Only order details/key are stored; authentication stays in the HttpOnly cookie.
 * An uncertain response never authorizes a new GUID or a changed payload.
 */
(() => {
  "use strict";
  const CART = "fourchang.cart-draft.v1";
  const INTENT = "fourchang.checkout-intent.v1";
  const LOCK = "fourchang.checkout-create.v1";
  const button = document.getElementById("checkout-proceed");
  if (!button) return;
  // Rendered by the server; missing/unrecognized values fail closed.
  const creationEnabled = button.dataset.orderCreationEnabled === "true";
  const ownerId = button.dataset.customerId || "";
  const ownerProof = button.dataset.ownerProof || "";
  const owns = intent => !!ownerId && !!ownerProof && intent.ownerId === ownerId && !!intent.command.ownerProof;
  document.getElementById("checkout-disabled").hidden = creationEnabled;
  const name = document.getElementById("takeaway-name");
  const phone = document.getElementById("takeaway-phone");
  const confirmed = document.getElementById("checkout-confirmed");
  const status = document.getElementById("checkout-order-status");
  const receiptPanel = document.getElementById("checkout-receipt");
  const login = document.getElementById("checkout-login");
  const pendingDetails = document.getElementById("checkout-pending-details");
  const token = document.querySelector('#checkout-quote-antiforgery input[name="__RequestVerificationToken"]')?.value;
  const money = value => new Intl.NumberFormat("vi-VN").format(value) + "đ";
  let busy = false;
  let blocked = false;

  function readIntent() {
    const raw = localStorage.getItem(INTENT);
    if (!raw) return null;
    const intent = JSON.parse(raw);
    if (intent?.version !== 1 || !intent.command ||
        !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(intent.command.operation) ||
        typeof intent.cartRaw !== "string" || intent.command.order?.hinhThucNhanMon !== "TAKEAWAY" ||
        !Number.isFinite(intent.command.acceptedTotal) || intent.command.acceptedTotal < 0)
      throw new Error("Dữ liệu thao tác đang lưu không hợp lệ. Cần kiểm tra đơn trước khi đặt tiếp; không tự tạo mã mới.");
    return intent;
  }
  function validReceipt(receipt, intent) {
    return receipt && Number.isSafeInteger(receipt.maDonHang) && receipt.maDonHang > 0 &&
      typeof receipt.maDonHienThi === "string" && receipt.maDonHienThi.trim().length > 0 &&
      receipt.trangThai === "PENDING_PAYMENT" &&
      Number.isFinite(receipt.tienHang) && receipt.tienHang >= 0 && receipt.phiGiaoHang === 0 &&
      receipt.tongCong === receipt.tienHang && receipt.tongCong === intent.command.acceptedTotal &&
      (receipt.hetHanLuc == null || (typeof receipt.hetHanLuc === "string" && Number.isFinite(Date.parse(receipt.hetHanLuc))));
  }
  function showReceipt(receipt) {
    document.getElementById("receipt-code").textContent = receipt.maDonHienThi;
    document.getElementById("receipt-total").textContent = money(receipt.tongCong);
    // SQL datetime2 may be returned without an offset; Backend stores these timestamps in UTC.
    const deadline = receipt.hetHanLuc;
    document.getElementById("receipt-deadline").textContent = deadline
      ? new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(deadline) ? deadline : deadline + "Z").toLocaleString("vi-VN")
      : "Backend chưa cung cấp";
    receiptPanel.hidden = false;
  }
  function update() {
    try {
      const intent = readIntent();
      if ((intent && !owns(intent)) || (!intent && (!ownerId || !ownerProof))) {
        button.disabled = true;
        name.disabled = phone.disabled = confirmed.disabled = true;
        name.value = phone.value = "";
        receiptPanel.hidden = pendingDetails.hidden = true;
        login.hidden = false;
        status.textContent = intent
          ? "Không xác minh được thao tác thuộc tài khoản hiện tại. Dữ liệu cũ được giữ nguyên; đăng nhập đúng tài khoản hoặc liên hệ đối soát."
          : "Cần xác minh Customer đang đăng nhập trước khi đặt. Đăng nhập rồi tải lại Checkout.";
        return;
      }
      const completed = intent?.receipt && validReceipt(intent.receipt, intent);
      if (intent?.receipt && !completed) throw new Error("Biên nhận lưu chưa hợp lệ. Cần kiểm tra đơn trước khi đặt tiếp.");
      const mode = document.querySelector("[data-tabs] [data-tab].selected")?.dataset.tab;
      const quote = window.fourchangCheckoutQuote;
      const validForm = name.value.trim().length > 0 && name.value.trim().length <= 120 &&
        !/[\u0000-\u001f\u007f]/.test(name.value) && /^\+?[0-9]{9,15}$/.test(phone.value.trim());
      const ready = mode === "takeaway" && quote && validForm && confirmed.checked &&
        (localStorage.getItem(CART) || "[]") === quote.cartRaw;
      name.disabled = phone.disabled = confirmed.disabled = !!intent || busy;
      pendingDetails.hidden = !intent || !!completed;
      if (intent) {
        name.value = intent.command.order.tenNguoiNhan;
        phone.value = intent.command.order.dienThoaiNguoiNhan;
        pendingDetails.textContent = "Thao tác mang đi đã gửi: " + name.value + " · " + phone.value +
          " · Tổng đã chấp thuận " + money(intent.command.acceptedTotal) + ". Thử lại sẽ dùng nguyên thông tin này.";
      }
      button.disabled = !creationEnabled || busy || blocked || !navigator.locks || !token || !!completed || (!intent && !ready);
      button.textContent = !creationEnabled ? "Tạo đơn đang tắt" : busy ? "Đang gửi…" : intent && !completed ? "Thử lại thao tác đang chờ kết quả" : "Đặt mang đi";
      receiptPanel.hidden = !completed;
      if (completed) showReceipt(intent.receipt);
      else if (intent && !busy && !status.textContent) status.textContent = "Có thao tác chưa xác định kết quả. Chỉ thử lại với cùng mã và thông tin đã gửi; không tạo đơn mới.";
      if (!navigator.locks) status.textContent = "Trình duyệt chưa hỗ trợ khóa thao tác giữa các tab. Hãy dùng trình duyệt có Web Locks để đặt đơn an toàn.";
    } catch (error) {
      blocked = true;
      button.disabled = true;
      status.textContent = error.message || "Không truy cập được bộ nhớ thao tác. Chưa thể đặt đơn an toàn.";
    }
  }
  function invalidate() { confirmed.checked = false; update(); }
  name.addEventListener("input", invalidate);
  phone.addEventListener("input", invalidate);
  confirmed.addEventListener("change", update);
  window.addEventListener("checkout:quote", invalidate);
  window.addEventListener("storage", event => { if (event.key === INTENT || event.key === CART || event.key === null) invalidate(); });
  window.addEventListener("pageshow", update);

  button.addEventListener("click", async () => {
    if (!creationEnabled || button.disabled || busy) return;
    busy = true;
    update();
    try {
      await navigator.locks.request(LOCK, { ifAvailable: true }, async lock => {
        if (!lock) { status.textContent = "Một tab khác đang gửi đơn. Đợi kết quả rồi thử lại."; return; }
        let intent = readIntent();
        if ((intent && !owns(intent)) || (!intent && (!ownerId || !ownerProof)))
          throw new Error("Không xác minh được chủ thao tác; không gửi dưới tài khoản khác.");
        if (intent?.receipt) return;
        if (!intent) {
          const quote = window.fourchangCheckoutQuote;
          if (!confirmed.checked || !quote || (localStorage.getItem(CART) || "[]") !== quote.cartRaw ||
              document.querySelector("[data-tabs] [data-tab].selected")?.dataset.tab !== "takeaway")
            throw new Error("Giỏ hoặc hình thức nhận đã đổi. Vui lòng báo giá và xác nhận lại.");
          intent = {
            version: 1, ownerId, cartRaw: quote.cartRaw,
            command: {
              operation: crypto.randomUUID(), acceptedTotal: quote.price.tongCong, ownerProof,
              order: { ...quote.payload, tenNguoiNhan: name.value.trim(), dienThoaiNguoiNhan: phone.value.trim() }
            }
          };
          // A failed durable write must prevent the network request.
          localStorage.setItem(INTENT, JSON.stringify(intent));
        }
        update();
        login.hidden = true;
        status.textContent = "Đang gửi thao tác " + intent.command.operation + ".";
        try {
          const response = await fetch("/Checkout?handler=Create", {
            method: "POST", credentials: "same-origin", cache: "no-store",
            headers: { "Content-Type": "application/json", "Accept": "application/json", "RequestVerificationToken": token },
            body: JSON.stringify(intent.command), signal: AbortSignal.timeout(30000)
          });
          let data = null;
          const media = (response.headers.get("Content-Type") || "").split(";")[0].trim().toLowerCase();
          if (media === "application/json" || media.endsWith("+json")) {
            try { data = await response.json(); } catch { /* Keep pending on empty/malformed JSON. */ }
          }
          if (response.status !== 201 && response.status !== 200) {
            if (response.status === 401 || response.status === 403) login.hidden = false;
            // Only this Backend rejection proves no order with this key committed under its transaction lock.
            if (response.status === 409 && data?.code === "CHECKOUT_PRICE_CHANGED") {
              localStorage.removeItem(INTENT);
              invalidate();
              window.dispatchEvent(new Event("checkout:refresh"));
              status.textContent = data.message;
              return;
            }
            if (data?.code === "CHECKOUT_OWNER_MISMATCH") blocked = true;
            throw new Error(`HTTP ${response.status}: ${data?.message || data?.detail || "Không xác nhận được kết quả; phản hồi không phải JSON hợp lệ."}`);
          }
          if (!validReceipt(data, intent)) throw new Error("Biên nhận không hợp lệ; chưa xác định kết quả.");
          // Persist the receipt before touching the cart, so reload cannot create a new operation accidentally.
          intent.receipt = data;
          localStorage.setItem(INTENT, JSON.stringify(intent));
          // Another tab may have edited the draft while this request was in flight.
          status.textContent = "Đã xác nhận tạo đơn. Chưa xác minh thanh toán. Theo dõi đơn để xem trạng thái hiện tại.";
          try {
            if ((localStorage.getItem(CART) || "[]") === intent.cartRaw) localStorage.removeItem(CART);
          } catch {
            status.textContent += " Chưa dọn được giỏ nháp; kiểm tra giỏ trước khi bắt đầu đơn tiếp theo.";
          }
          window.dispatchEvent(new Event("checkout:refresh"));
        } catch (error) {
          status.textContent = (error.message || "Mất kết nối hoặc timeout.") + " Giữ nguyên mã và payload; chỉ thử lại thao tác này.";
        }
      });
    } catch (error) {
      status.textContent = error.message || "Chưa thể lưu thao tác an toàn.";
    } finally { busy = false; update(); }
  });

  document.getElementById("checkout-new-order").addEventListener("click", async () => {
    if (busy || blocked || !navigator.locks) return;
    try {
      await navigator.locks.request(LOCK, () => {
        const intent = readIntent();
        if (!intent?.receipt || !owns(intent) || !validReceipt(intent.receipt, intent)) return;
        localStorage.removeItem(INTENT);
        status.textContent = "Kiểm tra giỏ và xác nhận lại trước khi tạo đơn tiếp theo.";
        invalidate();
        window.dispatchEvent(new Event("checkout:refresh"));
      });
    } catch (error) { status.textContent = error.message; }
  });
  update();
})();
