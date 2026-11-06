/* UC-WEB-06: server quote preview only. Never POST /api/customer/orders. */
(() => {
  "use strict";
  const KEY = "fourchang.cart-draft.v1";
  const tabs = document.querySelector("[data-tabs]");
  const warning = document.getElementById("checkout-price-warning");
  const base = document.getElementById("checkout-base-subtotal");
  const shipping = document.getElementById("shipping-fee");
  const total = document.getElementById("checkout-total");
  const baseLabel = base?.parentElement?.querySelector("dt");
  const token = document.querySelector('#checkout-quote-antiforgery input[name="__RequestVerificationToken"]')?.value;
  if (!tabs || !warning || !base || !shipping || !total) return;

  const money = amount => new Intl.NumberFormat("vi-VN").format(amount) + "đ";
  const normalize = value => String(value ?? "").trim().toLocaleLowerCase("vi");
  let current = 0;
  let controller;

  function fail(message) {
    total.textContent = "Chưa thể báo giá";
    warning.textContent = message;
  }
  function getCart() {
    try {
      const cart = JSON.parse(localStorage.getItem(KEY) || "[]");
      if (!Array.isArray(cart)) throw new Error("Giỏ hàng không hợp lệ.");
      return cart;
    } catch {
      throw new Error("Không đọc được giỏ hàng nháp. Vui lòng quay lại Giỏ hàng.");
    }
  }
  function percent(value) {
    const match = /^\s*(\d{1,3})\s*%?\s*$/.exec(String(value));
    if (!match) throw new Error("Tỷ lệ đường/đá trong giỏ không hợp lệ.");
    const n = Number(match[1]);
    if (n > 100) throw new Error("Tỷ lệ đường/đá vượt giới hạn.");
    return n;
  }
  async function json(response) {
    if (!response.ok) throw new Error(`Backend không báo giá được (HTTP ${response.status}).`);
    return response.json();
  }

  async function refresh() {
    const seq = ++current;
    controller?.abort();
    controller = new AbortController();
    const signal = controller.signal;
    const mode = tabs.querySelector("[data-tab].selected")?.dataset.tab;
    if (baseLabel) baseLabel.textContent = "Tạm tính giá cơ sở";
    if (mode !== "takeaway") {
      // Checkout preview has no validated delivery distance or real table token.
      return;
    }
    if (!token) {
      fail("Thiếu mã bảo vệ biểu mẫu báo giá. Tải lại trang.");
      return;
    }
    total.textContent = "Đang báo giá...";
    warning.textContent = "Đang kiểm tra biến thể, đường/đá và topping theo Backend.";
    try {
      const cart = getCart();
      if (cart.length < 1 || cart.length > 50)
        throw new Error("Giỏ hàng trống hoặc quá nhiều dòng món.");
      const ids = [...new Set(cart.map(i => Number(i.productId)))];
      if (ids.some(id => !Number.isSafeInteger(id) || id < 1))
        throw new Error("Giỏ hàng có mã món không hợp lệ.");
      const variantsByProduct = new Map(await Promise.all(ids.map(async id => {
        const res = await fetch(`/Checkout?handler=Variants&productId=${id}`, {
          credentials: "same-origin", cache: "no-store", signal
        });
        const variants = await json(res);
        if (!Array.isArray(variants)) throw new Error("Dữ liệu biến thể không hợp lệ.");
        return [id, variants];
      })));
      const items = cart.map(item => {
        const id = Number(item.variantId);
        const qty = Number(item.quantity);
        if (!Number.isSafeInteger(id) || id <= 0 || !Number.isInteger(qty) || qty < 1 || qty > 99)
          throw new Error("Mã biến thể hoặc số lượng không hợp lệ.");
        const variant = (variantsByProduct.get(Number(item.productId)) || [])
          .find(v => Number(v.maCauHinhMon) === id);
        if (!variant || !variant.available)
          throw new Error(`${item.name || "Món"} không còn khả dụng. Vui lòng chỉnh giỏ.`);
        const sugar = percent(item.sugar);
        const ice = percent(item.ice);
        if (!(variant.tuyChonPhaChe || []).some(x =>
          Number(x.phanTramDuong) === sugar && Number(x.phanTramDa) === ice))
          throw new Error(`${item.name || "Món"} không hỗ trợ tỷ lệ đường/đá đã chọn.`);
        const toppings = [];
        if (item.topping && normalize(item.topping) !== normalize("Không")) {
          const qtyTop = Number(item.toppingQuantity);
          const matches = (variant.toppings || []).filter(t =>
            normalize(t.tenMonThem) === normalize(item.topping) && t.available);
          if (matches.length !== 1)
            throw new Error(`Topping "${item.topping}" không được bán cho biến thể đã chọn.`);
          if (!Number.isInteger(qtyTop) || qtyTop < 1 ||
              qtyTop > Number(matches[0].gioiHanMoiLy))
            throw new Error("Số phần topping vượt giới hạn cấu hình món.");
          toppings.push({ maMonThem: matches[0].maMonThem, soPhanMoiLy: qtyTop });
        }
        return {
          maCauHinhMon: id,
          soLuong: qty,
          phanTramDuong: sugar,
          phanTramDa: ice,
          ghiChu: String(item.note || "").slice(0, 500),
          toppings
        };
      });
      const payload = {
        hinhThucNhanMon: "TAKEAWAY",
        maBanHienThi: null,
        tenNguoiNhan: null,
        dienThoaiNguoiNhan: null,
        diaChiGiao: null,
        khoangCachKm: null,
        ghiChu: "",
        items
      };
      const res = await fetch("/Checkout?handler=Quote", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token,
          "Accept": "application/json"
        },
        body: JSON.stringify(payload), signal
      });
      const price = await json(res);
      if (seq !== current || signal.aborted) return;
      if (![price.tienHang, price.phiGiaoHang, price.tongCong].every(x =>
        typeof x === "number" && Number.isFinite(x) && x >= 0))
        throw new Error("Báo giá từ Backend không hợp lệ.");
      base.textContent = money(price.tienHang);
      if (baseLabel) baseLabel.textContent = "Tiền món (Backend báo giá)";
      shipping.textContent = money(price.phiGiaoHang);
      total.textContent = money(price.tongCong);
      warning.textContent = "Báo giá thử từ Backend, đã tính topping; chưa tạo đơn. " +
        "API tạo đơn vẫn cần kiểm tra tồn kho đồng thời, thanh toán và chống tạo đơn trùng.";
    } catch (error) {
      if (seq !== current || signal.aborted) return;
      fail(error instanceof Error ? error.message : "Không thể báo giá giỏ hàng.");
    }
  }

  tabs.addEventListener("click", event => {
    if (!event.target.closest("[data-tab]")) return;
    queueMicrotask(refresh);
  });
  window.addEventListener("storage", event => { if (event.key === KEY) refresh(); });
  window.addEventListener("pageshow", refresh);
})();