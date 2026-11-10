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
      if (!Array.isArray(cart) || cart.some(row => !row || typeof row !== "object" || Array.isArray(row)))
        throw new Error("Giỏ hàng không hợp lệ.");
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
    window.fourchangCheckoutQuote = null;
    window.dispatchEvent(new Event("checkout:quote"));
    const seq = ++current;
    controller?.abort();
    controller = new AbortController();
    const signal = controller.signal;
    const mode = tabs.querySelector("[data-tab].selected")?.dataset.tab;
    // Discard ALL previous server values immediately when the mode/cart changes.
    // Keeping the last TAKEAWAY total while showing DINE_IN or DELIVERY would
    // falsely imply that an unverified table/delivery fee has been approved.
    if (baseLabel) baseLabel.textContent = "Tạm tính giá cơ sở";
    base.textContent = "Chưa xác nhận";
    shipping.textContent = "Chưa báo giá";
    total.textContent = "Chưa thể đặt đơn";
    if (mode !== "takeaway") {
      warning.textContent = mode === "delivery"
        ? "Chưa có địa chỉ, khoảng cách và phí giao được Backend xác minh. Không dùng báo giá mang đi."
        : "Chưa có QR bàn được Backend xác minh. Không dùng báo giá mang đi cho đơn tại bàn.";
      return;
    }
    if (!token) {
      fail("Thiếu mã bảo vệ biểu mẫu báo giá. Tải lại trang.");
      return;
    }
    total.textContent = "Đang báo giá...";
    warning.textContent = "Đang kiểm tra biến thể, đường/đá và topping theo Backend.";
    try {
      const cartRaw = localStorage.getItem(KEY) || "[]";
      const cart = getCart();
      if (cart.length < 1 || cart.length > 50)
        throw new Error("Giỏ hàng trống hoặc quá nhiều dòng món.");
      const ids = [...new Set(cart.map(i => i.productId))];
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
        const id = item.variantId;
        const qty = item.quantity;
        // The cart renderer and checkout must agree: never coerce corrupt draft IDs or quantities.
        if (!Number.isSafeInteger(id) || id <= 0 || !Number.isSafeInteger(qty) || qty < 1 || qty > 99 ||
            !Number.isSafeInteger(item.listedPrice) || item.listedPrice < 0)
          throw new Error("Mã biến thể hoặc số lượng không hợp lệ.");
        const variant = (variantsByProduct.get(Number(item.productId)) || [])
          .find(v => v.maCauHinhMon === id);
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
              new Set(item.toppings.map(t => t?.id)).size !== item.toppings.length ||
              item.toppings.some(t => !t || !Number.isSafeInteger(t.id) || t.id <= 0 ||
                !Number.isSafeInteger(t.price) || t.price < 0))
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
        // Do not silently truncate a customer's instructions. A quote must
        // describe the exact same draft that the customer will confirm.
        if (item.note != null && typeof item.note !== "string")
          throw new Error("Ghi chú trong giỏ không hợp lệ. Vui lòng sửa dòng món.");
        const note = item.note || "";
        if (note.length > 500)
          throw new Error("Ghi chú vượt 500 ký tự. Vui lòng sửa dòng món trước khi báo giá.");
        return {
          maCauHinhMon: id,
          soLuong: qty,
          phanTramDuong: sugar,
          phanTramDa: ice,
          ghiChu: note,
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
      if ((localStorage.getItem(KEY) || "[]") !== cartRaw) {
        fail("Giỏ hàng đã thay đổi trong lúc báo giá. Vui lòng lấy báo giá mới.");
        return;
      }
      if (![price.tienHang, price.phiGiaoHang, price.tongCong].every(x =>
        typeof x === "number" && Number.isSafeInteger(x) && x >= 0) ||
          !Number.isSafeInteger(price.tienHang + price.phiGiaoHang) ||
          price.tongCong !== price.tienHang + price.phiGiaoHang ||
          // BR-16: TAKEAWAY is never charged a delivery fee.
          price.phiGiaoHang !== 0)
        throw new Error("Báo giá mang đi từ Backend không hợp lệ; không được tính phí giao.");
      base.textContent = money(price.tienHang);
      if (baseLabel) baseLabel.textContent = "Tiền món (Backend báo giá)";
      shipping.textContent = money(price.phiGiaoHang);
      total.textContent = money(price.tongCong);
      warning.textContent = "Giá Backend đã gồm topping; phí mang đi 0đ. Xác nhận tổng tiền trước khi đặt. Tồn kho sẽ được kiểm tra lúc tạo đơn.";
      window.fourchangCheckoutQuote = { payload, price, cartRaw };
      window.dispatchEvent(new Event("checkout:quote"));
    } catch (error) {
      if (seq !== current || signal.aborted) return;
      fail(error instanceof Error ? error.message : "Không thể báo giá giỏ hàng.");
    }
  }

  tabs.addEventListener("click", event => {
    if (!event.target.closest("[data-tab]")) return;
    queueMicrotask(refresh);
  });
  window.addEventListener("storage", event => { if (event.key === KEY || event.key === null) refresh(); });
  window.addEventListener("pageshow", refresh);
  window.addEventListener("checkout:refresh", refresh);
  // localStorage does not emit a storage event in the same tab.
  window.addEventListener("cart:changed", refresh);
})();
