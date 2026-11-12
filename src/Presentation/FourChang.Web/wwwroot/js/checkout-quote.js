/* UC-WEB-06: quote only. checkout-order.js owns order creation and recovery. */
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
    if (!response.ok) throw new Error(`Chưa kiểm tra được giá (HTTP ${response.status}).`);
    return response.json();
  }

  async function refresh() {
    window.fourchangCheckoutQuote = null;
    window.dispatchEvent(new Event("checkout:quote"));
    const seq = ++current;
    controller?.abort();
    controller = new AbortController();
    const signal = controller.signal;
    const mode = tabs.querySelector("[data-tab].selected")?.dataset.tab;
    if (baseLabel) baseLabel.textContent = "Tạm tính giá cơ sở";
    if (mode !== "takeaway" && document.getElementById("checkout-proceed")?.dataset.fulfillmentSimulation !== "true") {
      // Checkout preview has no validated delivery distance or real table token.
      return;
    }
    if (!token) {
      fail("Thiếu mã bảo vệ biểu mẫu báo giá. Tải lại trang.");
      return;
    }
    total.textContent = "Đang báo giá...";
    warning.textContent = "Đang kiểm tra giá món và các tùy chọn bạn đã chọn.";
    try {
      const cartRaw = localStorage.getItem(KEY) || "[]";
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
        if (item.draftVersion === 2) {
          if (!Array.isArray(item.toppings) || item.toppings.length > 10 ||
              new Set(item.toppings.map(t => t?.id)).size !== item.toppings.length)
            throw new Error("Snapshot topping không hợp lệ; hãy sửa dòng giỏ.");
          for (const top of item.toppings) {
            const matches = (variant.toppings || []).filter(t => t.maMonThem === top?.id && t.available === true);
            if (matches.length !== 1 || !Number.isSafeInteger(top.portions) || top.portions < 1 ||
                top.portions > matches[0].gioiHanMoiLy)
              throw new Error("Topping không còn hiệu lực hoặc số phần vượt giới hạn. Hãy sửa giỏ.");
            toppings.push({ maMonThem: top.id, soPhanMoiLy: top.portions });
          }
        } else if (item.topping && normalize(item.topping) !== normalize("Không")) {
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
        hinhThucNhanMon: mode === "delivery" ? "DELIVERY" : mode === "dinein" ? "DINE_IN" : "TAKEAWAY",
        maBanHienThi: mode === "dinein" ? document.getElementById("checkout-table")?.value || null : null,
        tenNguoiNhan: null,
        dienThoaiNguoiNhan: null,
        diaChiGiao: mode === "delivery" ? document.getElementById("checkout-address")?.value.trim() || null : null,
        khoangCachKm: null,
        ghiChu: mode === "delivery" ? document.getElementById("checkout-delivery-note")?.value.trim() || "" : "",
        items
      };
      if (mode === "dinein" && !payload.maBanHienThi) throw new Error("Vui lòng chọn bàn.");
      if (mode === "delivery" && (!payload.diaChiGiao || payload.diaChiGiao.length < 5)) throw new Error("Nhập địa chỉ giao hàng từ 5 ký tự.");
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
      if ((localStorage.getItem(KEY) || "[]") !== cartRaw) return;
      if (![price.tienHang, price.phiGiaoHang, price.tongCong].every(x =>
        typeof x === "number" && Number.isSafeInteger(x) && x >= 0) ||
          !Number.isSafeInteger(price.tienHang + price.phiGiaoHang) ||
          price.tongCong !== price.tienHang + price.phiGiaoHang)
        throw new Error("Chưa nhận được giá hợp lệ. Vui lòng thử lại.");
      base.textContent = money(price.tienHang);
      if (baseLabel) baseLabel.textContent = "Tiền món và topping";
      shipping.textContent = money(price.phiGiaoHang);
      total.textContent = money(price.tongCong);
      warning.textContent = mode === "delivery" ? "Đã gồm topping và phí giao hàng mô phỏng. Kiểm tra tổng tiền trước khi đặt." : "Đã gồm topping; phí giao hàng 0đ.";
      window.fourchangCheckoutQuote = { payload, price, cartRaw, fulfillmentContext: fulfillmentContext() };
      window.dispatchEvent(new Event("checkout:quote"));
    } catch (error) {
      if (seq !== current || signal.aborted) return;
      fail(error instanceof Error ? error.message : "Không thể báo giá giỏ hàng.");
    }
  }

  /** Ràng buộc báo giá với đúng hình thức/bàn/địa chỉ, không dùng lại báo giá khi khách đổi thông tin. */
  function fulfillmentContext() {
    const mode = tabs.querySelector("[data-tab].selected")?.dataset.tab;
    return JSON.stringify([mode, mode === "dinein" ? document.getElementById("checkout-table")?.value : null,
      mode === "delivery" ? document.getElementById("checkout-address")?.value.trim() : null,
      mode === "delivery" ? document.getElementById("checkout-delivery-note")?.value.trim() : null]);
  }
  window.fourchangFulfillmentContext = fulfillmentContext;
  for (const id of ["checkout-table", "checkout-address", "checkout-delivery-note"]) document.getElementById(id)?.addEventListener("input", refresh);
  /** Đọc bàn từ API qua proxy Web; chỉ đưa dữ liệu dạng text vào option. */
  async function loadTables() {
    const select = document.getElementById("checkout-table"); if (!select) return;
    try {
      const rows = await json(await fetch("/Checkout?handler=Tables", { cache:"no-store", credentials:"same-origin" }));
      select.replaceChildren(new Option("Chọn bàn", ""));
      for (const row of rows) select.append(new Option(row.tenBan + " · " + row.maBanHienThi, row.maBanHienThi));
    } catch { select.replaceChildren(new Option("Không tải được bàn, hãy tải lại trang", "")); }
  }
  loadTables();
  tabs.addEventListener("click", event => {
    if (!event.target.closest("[data-tab]")) return;
    queueMicrotask(refresh);
  });
  window.addEventListener("storage", event => { if (event.key === KEY || event.key === null) refresh(); });
  window.addEventListener("pageshow", refresh);
  window.addEventListener("checkout:refresh", refresh);
})();
