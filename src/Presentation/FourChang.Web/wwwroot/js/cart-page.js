(() => {
  "use strict";
  const C = window.FourChangCart, lines = document.getElementById("cart-lines");
  if (!C || !lines) return;
  const status = document.getElementById("cart-status"), editor = document.getElementById("cart-editor");
  const editDialog = document.getElementById("cart-edit-dialog");
  // Native dialog quản lý focus/Tab/Escape, chỉ đóng vùng nhập và không tự ghi lại giỏ.
  function closeEditor() { editor.hidden = true; editDialog?.close(); }
  editDialog?.querySelector(".config-close")?.addEventListener("click", closeEditor);
  editDialog?.addEventListener("close", () => { editor.hidden = true; editor.replaceChildren(); });
  const el = (tag, text) => { const e = document.createElement(tag); e.textContent = text; return e; };
  function button(label, action) {
    const b = el("button", label); b.type = "button"; b.className = "button secondary";
    b.onclick = async () => { try { await action(); } catch (e) { status.textContent = e.message; } }; return b;
  }
  function render() {
    lines.replaceChildren();
    let raw, cart;
    try { raw = C.raw(); cart = C.parse(raw); }
    catch { status.textContent = "Giỏ lưu trên máy bị lỗi. Chưa xóa hoặc ghi đè; chỉ xóa nháp khi bạn xác nhận."; return; }
    status.textContent = "Kiểm tra món, cấu hình và số lượng trước khi tiếp tục.";
    document.getElementById("cart-empty").hidden = cart.length > 0;
    let sum = 0, valid = true, qty = 0;
    cart.forEach((item, index) => {
      const row = el("article", ""); row.className = "line-item cart-product-row"; row.dataset.cartIndex = String(index);
      const body = el("div", ""); body.className = "cart-product-info"; body.append(el("h3", `${item?.name || "Dòng cũ cần sửa"} · ${item?.size || "?"}`));
      body.append(el("p", `${item?.temperature || "?"} · đường ${item?.sugar || "?"} · đá ${item?.ice || "?"} · ${item?.note || ""}`));
      const tops = item?.draftVersion === 2 && Array.isArray(item.toppings) ? item.toppings.map(t => `${t?.name || "?"} × ${t?.portions || "?"}/ly`).join(", ") : item?.topping;
      body.append(el("p", tops || "Không topping"));
      try { const amount = C.total(item); sum = C.add(sum, amount); const price = el("p", `${item.quantity} ly · ${C.format(amount)} (gồm topping)`); price.className = "cart-line-price"; body.append(price); }
      catch (e) { valid = false; body.append(el("p", e.message)); }
      if (C.integer(item?.quantity, 1, 99)) qty += item.quantity;
      const actions = el("div", ""); actions.className = "cart-line-actions";
      actions.append(button("Sửa món", async () => {
        const before = C.raw();
        if (before !== raw) throw new Error("Giỏ đã đổi. Chọn lại dòng sau khi tải lại.");
        editor.hidden = false;
        if (editDialog && !editDialog.open) editDialog.showModal();
        await window.FourChangCartEditor(editor, item?.productId, item, async (candidate, action) => {
          await C.write(before, rows => C.put(rows, candidate, index)); closeEditor();
          if (action?.checkout) location.assign("/Checkout");
        }, () => { closeEditor(); editor.replaceChildren(); });
      }));
      for (const delta of [-1, 1]) actions.append(button(delta < 0 ? "−" : "+", () => C.write(raw, rows => {
        const q = rows[index]?.quantity + delta;
        if (!C.integer(q, 1, 99)) throw new Error("Số ly phải từ 1–99; dùng Xóa món để bỏ dòng.");
        rows[index].quantity = q; C.total(rows[index]); return rows;
      })));
      // Nhóm số lượng thể hiện số ly đang chọn; các lệnh tăng/giảm cũ được giữ nguyên.
      const quantity = el("span", String(item?.quantity ?? "?")); quantity.className = "cart-quantity-value";
      quantity.setAttribute("aria-label", "Số ly đang chọn");
      actions.insertBefore(quantity, actions.children[2]);
      actions.children[1].classList.add("quantity-step");
      actions.children[3].classList.add("quantity-step");
      const remove = button("Xóa món", () => C.write(raw, rows => rows.filter((_, i) => i !== index)));
      remove.classList.add("remove-line"); actions.append(remove);
      row.append(body, actions); lines.append(row);
    });
    document.getElementById("cart-count").textContent = `${qty} ly · ${cart.length} dòng giỏ nháp`;
    document.getElementById("cart-base-total").textContent = valid ? C.format(sum) : "Chưa xác định — sửa dòng cần xác minh";
    const badge = document.getElementById("cart-badge"); if (badge) badge.textContent = String(qty);
  }
  document.getElementById("clear-cart").onclick = async () => {
    const before = C.raw();
    if (!confirm("Xóa toàn bộ giỏ nháp? Thao tác Checkout chưa rõ kết quả vẫn được giữ nguyên.")) return;
    try {
      // Explicit deletion also permits recovery from corrupt JSON; never delete an intent.
      if (!navigator.locks) throw new Error("Cần Web Locks để xóa giỏ an toàn.");
      await navigator.locks.request("fourchang.cart-draft.write", () => {
        if (C.raw() !== before) throw new Error("Giỏ đã đổi ở tab khác; kiểm tra trước khi xóa.");
        localStorage.setItem(C.key, "[]");
      });
      editor.hidden = true; render();
    } catch (e) { status.textContent = e.message; }
  };
  window.addEventListener("cart:changed", render);
  window.addEventListener("storage", e => { if (e.key === C.key || e.key === null) render(); }); render();
})();
