// Read-only global header count. Never clears a cart or changes a pending Checkout operation.
(() => {
  "use strict";
  const badge = document.getElementById("cart-badge");
  if (!badge) return;
  const key = "fourchang.cart-draft.v1";
  function refresh() {
    try {
      const raw = localStorage.getItem(key);
      const rows = JSON.parse(raw === null ? "[]" : raw);
      if (!Array.isArray(rows) || rows.length > 50) throw new Error("Invalid cart");
      let quantity = 0;
      for (const row of rows) {
        if (!row || !Number.isSafeInteger(row.quantity) || row.quantity < 1 || row.quantity > 99)
          throw new Error("Invalid quantity");
        quantity += row.quantity;
        if (!Number.isSafeInteger(quantity)) throw new Error("Quantity overflow");
      }
      badge.textContent = String(quantity);
      badge.removeAttribute("title");
    } catch {
      badge.textContent = "!";
      badge.title = "Giỏ hàng không đọc được; mở giỏ để kiểm tra";
    }
  }
  window.addEventListener("cart:changed", refresh);
  window.addEventListener("storage", event => { if (event.key === key || event.key === null) refresh(); });
  window.addEventListener("pageshow", refresh);
  refresh();
})();
