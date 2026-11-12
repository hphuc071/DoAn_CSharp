(() => {
  "use strict";
  const C = window.FourChangCart, editor = document.getElementById("product-editor");
  if (!C || !editor || !window.FourChangCartEditor) return;
  let generation = 0, feedbackTimer;
  const configDialog = document.getElementById("product-config-dialog");
  // Hộp thoại native giữ focus bàn phím bên trong, Escape trả lại focus nút chọn món.
  function closeConfiguration() { configDialog?.close(); }
  configDialog?.querySelector(".config-close")?.addEventListener("click", closeConfiguration);
  configDialog?.addEventListener("close", () => { generation++; editor.replaceChildren(); closeConfiguration(); });
  // Chuyển vùng nhìn và focus tới bước đang thao tác; tôn trọng cài đặt giảm chuyển động.
  function moveTo(target, focus) {
    target?.scrollIntoView({ behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth", block: "start" });
    focus?.focus({ preventScroll: true });
  }
  // Chỉ dọn form cấu hình sau khi lưu giỏ thành công; tuyệt đối không xóa các món đã có.
  function readyForNextProduct(name) {
    const message = document.createElement("p");
    message.setAttribute("role", "status"); message.dataset.productAdded = "";
    message.textContent = `Đã thêm ${name} vào giỏ. Chọn món tiếp theo từ thực đơn.`;
    const feedback = document.getElementById("menu-feedback");
    if (feedback) { feedback.replaceChildren(message); feedback.hidden = false; clearTimeout(feedbackTimer); feedbackTimer = setTimeout(() => { feedback.hidden = true; }, 6000); }
    else editor.replaceChildren(message);
    closeConfiguration();
    const menu = document.getElementById("menu") || document.getElementById("product-grid");
    moveTo(menu, document.getElementById("menu-search"));
  }
  function badge() {
    try {
      const n = C.parse(C.raw()).reduce((sum, x) => sum + (C.integer(x?.quantity, 1, 99) ? x.quantity : 0), 0);
      const target = document.getElementById("cart-badge"); if (target) target.textContent = String(n);
    } catch { /* Preserve corrupt storage for explicit repair; never overwrite on load. */ }
  }
  // Delegation trên document hỗ trợ cả markup cũ không có #product-grid và card
  // mới được SSE thêm vào. Selector bên dưới vẫn chỉ nhận nút chọn món trong card.
  document.addEventListener("click", async event => {
    const button = event.target.closest(".product-card [data-product]");
    if (!button || button.disabled) return;
    const current = ++generation;
    if (configDialog && !configDialog.open) configDialog.showModal();
    // A new host prevents a slow prior selection from repainting the active product.
    const host = document.createElement("div"); editor.replaceChildren(host);
    let id = Number(button.dataset.productId);
    try {
      C.parse(C.raw());
      if (!C.integer(id, 1, Number.MAX_SAFE_INTEGER)) throw new Error("Thiếu mã sản phẩm. Tải lại thực đơn từ Backend.");
      await window.FourChangCartEditor(host, id, null, async (item, action) => {
        if (current !== generation) throw new Error("Món đang chọn đã đổi; vui lòng chọn lại.");
        await C.write(null, rows => C.put(rows, item)); badge();
        // Nếu khách đã chọn món khác trong khi lưu, không xóa form mới vừa mở.
        if (current === generation) {
          generation++;
          if (action?.checkout) { closeConfiguration(); location.assign("/Checkout"); }
          else readyForNextProduct(item.name);
        }
      }, () => { generation++; editor.replaceChildren(); closeConfiguration(); });
      if (current === generation && host.isConnected)
        moveTo(editor.closest(".customizer") || editor, host.querySelector("[data-editor-size]"));
    } catch (e) { host.textContent = e.message; }
  });
  window.addEventListener("storage", badge); window.addEventListener("cart:changed", badge); badge();
})();
