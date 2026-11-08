(() => {
  "use strict";
  const C = window.FourChangCart, editor = document.getElementById("product-editor");
  if (!C || !editor || !window.FourChangCartEditor) return;
  let generation = 0;
  function badge() {
    try {
      const n = C.parse(C.raw()).reduce((sum, x) => sum + (C.integer(x?.quantity, 1, 99) ? x.quantity : 0), 0);
      const target = document.getElementById("cart-badge"); if (target) target.textContent = String(n);
    } catch { /* Preserve corrupt storage for explicit repair; never overwrite on load. */ }
  }
  document.querySelectorAll(".product-card [data-product]").forEach(button => button.addEventListener("click", async () => {
    const current = ++generation;
    // A new host prevents a slow prior selection from repainting the active product.
    const host = document.createElement("div"); editor.replaceChildren(host);
    let id = Number(button.dataset.productId);
    try {
      C.parse(C.raw());
      if (!C.integer(id, 1, Number.MAX_SAFE_INTEGER)) throw new Error("Thiếu mã sản phẩm. Tải lại thực đơn từ Backend.");
      await window.FourChangCartEditor(host, id, null, async item => {
        if (current !== generation) throw new Error("Món đang chọn đã đổi; vui lòng chọn lại.");
        await C.write(null, rows => C.put(rows, item)); badge();
      }, () => { generation++; editor.replaceChildren(); });
    } catch (e) { host.textContent = e.message; }
  }));
  window.addEventListener("storage", badge); window.addEventListener("cart:changed", badge); badge();
})();
