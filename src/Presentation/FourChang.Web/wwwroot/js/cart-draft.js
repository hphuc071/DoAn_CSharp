// Local draft only. Never touches a pending Checkout operation or calls a write API.
(() => {
  "use strict";
  const key = "fourchang.cart-draft.v1";
  const fail = message => { throw new Error(message); };
  const money = n => typeof n === "number" && Number.isSafeInteger(n) && n >= 0;
  const integer = (n, min, max) => Number.isSafeInteger(n) && n >= min && n <= max;
  const add = (a, b) => money(a) && money(b) && money(a + b) ? a + b : fail("Tiền vượt độ chính xác an toàn của trình duyệt.");
  const multiply = (a, b) => money(a) && integer(b, 0, 2147483647) && money(a * b) ? a * b : fail("Tiền hoặc số lượng không hợp lệ.");
  const raw = () => localStorage.getItem(key) || "[]";
  function parse(text) {
    const rows = JSON.parse(text);
    if (!Array.isArray(rows)) fail("Giỏ lưu trên máy không hợp lệ; chưa ghi đè dữ liệu cũ.");
    return rows;
  }
  function total(item) {
    if (!item || !integer(item.quantity, 1, 99) || !money(item.listedPrice)) fail("Dòng giỏ cần được sửa và xác nhận lại.");
    let unit = item.listedPrice;
    if (item.draftVersion !== 2 || !Array.isArray(item.toppings)) {
      if (item.topping && item.topping !== "Không") fail("Giỏ cũ chưa có giá topping; hãy mở Sửa món.");
    } else {
      if (item.toppings.length > 10 || new Set(item.toppings.map(t => t?.id)).size !== item.toppings.length) fail("Topping không hợp lệ.");
      for (const t of item.toppings) {
        if (!t || !integer(t.id, 1, Number.MAX_SAFE_INTEGER) || !integer(t.portions, 1, 2147483647)) fail("Topping không hợp lệ.");
        unit = add(unit, multiply(t.price, t.portions));
      }
    }
    return multiply(unit, item.quantity);
  }
  function identity(item) {
    if (item?.draftVersion !== 2 || typeof item.pricingContext !== "string" || !item.pricingContext) return null;
    total(item);
    return JSON.stringify([item.productId, item.variantId, item.name, item.size, item.temperature,
      item.sugar, item.ice, item.note, item.listedPrice, item.pricingContext,
      [...item.toppings].sort((a, b) => a.id - b.id)]);
  }
  async function write(expected, change) {
    if (!navigator.locks) fail("Trình duyệt cần hỗ trợ Web Locks để sửa giỏ an toàn giữa các tab.");
    return navigator.locks.request("fourchang.cart-draft.write", () => {
      const before = raw();
      if (expected !== null && expected !== before) fail("Giỏ đã đổi ở tab khác. Tải lại dòng trước khi lưu.");
      const next = change(parse(before));
      if (next.length > 50) fail("Giỏ tối đa 50 dòng.");
      localStorage.setItem(key, JSON.stringify(next));
      window.dispatchEvent(new Event("cart:changed"));
    });
  }
  function put(rows, item, index = -1) {
    const id = identity(item);
    if (!id) fail("Cần cấu hình và giá đã tải từ Backend.");
    const next = rows.filter((_, i) => i !== index);
    // Ignore invalid/legacy candidates for merging, but preserve their data for repair.
    const match = next.find(x => { try { return identity(x) === id; } catch { return false; } });
    if (match) {
      const quantity = match.quantity + item.quantity;
      if (!integer(quantity, 1, 99)) fail("Một dòng tối đa 99 ly; không tự cắt số lượng.");
      match.quantity = quantity; total(match);
    } else next.splice(index < 0 ? next.length : Math.min(index, next.length), 0, item);
    // Reject aggregate overflow without discarding old lines that need manual repair.
    let sum = 0;
    for (const row of next) {
      let amount;
      try { amount = total(row); } catch { if (row === item || row === match) throw new Error("Giá không hợp lệ."); else continue; }
      sum = add(sum, amount);
    }
    return next;
  }
  window.FourChangCart = Object.freeze({ key, raw, parse, total, identity, write, put, money, integer, add, multiply,
    format: n => new Intl.NumberFormat("vi-VN").format(n) + "đ" });
})();
