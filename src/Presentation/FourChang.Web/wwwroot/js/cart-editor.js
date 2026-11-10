// Shared Menu/Cart editor. Only GET catalog data; saving changes the local draft.
(() => {
  "use strict";
  const C = window.FourChangCart;
  if (!C) return;
  const element = (tag, text) => { const e = document.createElement(tag); if (text) e.textContent = text; return e; };
  function input(label, control, parent) { const wrapper = element("label", label); wrapper.className = "field"; wrapper.append(control); parent.append(wrapper); return control; }
  const option = (value, label) => { const o = element("option", label); o.value = value; return o; };
  async function json(url, signal) {
    const r = await fetch(url, { signal, cache: "no-store", headers: { Accept: "application/json" } });
    if (!r.ok) throw new Error(`Không tải được cấu hình (HTTP ${r.status}). Dữ liệu cũ vẫn được giữ.`);
    return r.json();
  }
  function normalize(list) {
    if (!Array.isArray(list)) throw new Error("Dữ liệu biến thể không hợp lệ.");
    const ids = new Set();
    return list.map(v => {
      if (!v || !C.integer(v.maCauHinhMon, 1, Number.MAX_SAFE_INTEGER) || ids.has(v.maCauHinhMon) ||
          !C.money(v.giaBan) || typeof v.kichCo !== "string" || !["HOT", "ICED"].includes(v.nhietDo) ||
          typeof v.available !== "boolean" || !Array.isArray(v.tuyChonPhaChe) || !Array.isArray(v.toppings))
        throw new Error("Cấu hình hoặc giá vượt độ chính xác hỗ trợ.");
      ids.add(v.maCauHinhMon);
      const pairs = v.tuyChonPhaChe.map(p => {
        if (!p || !C.integer(p.phanTramDuong, 0, 100) || !C.integer(p.phanTramDa, 0, 100)) throw new Error("Đường/đá không hợp lệ.");
        return [p.phanTramDuong, p.phanTramDa];
      }).sort((a, b) => a[0] - b[0] || a[1] - b[1]);
      const tops = v.toppings.map(t => {
        if (!t || !C.integer(t.maMonThem, 1, Number.MAX_SAFE_INTEGER) || typeof t.tenMonThem !== "string" ||
            !C.money(t.giaMoiPhan) || !C.integer(t.gioiHanMoiLy, 1, 2147483647) || typeof t.available !== "boolean") throw new Error("Topping không hợp lệ.");
        return { id: t.maMonThem, name: t.tenMonThem, price: t.giaMoiPhan, limit: t.gioiHanMoiLy, available: t.available };
      }).sort((a, b) => a.id - b.id);
      if (new Set(tops.map(t => t.id)).size !== tops.length) throw new Error("Topping bị trùng mã.");
      return { id: v.maCauHinhMon, size: v.kichCo, temperature: v.nhietDo, price: v.giaBan,
        available: v.available, pairs, tops, context: JSON.stringify([v.maCauHinhMon, v.kichCo, v.nhietDo, v.giaBan, pairs, tops]) };
    });
  }
  window.FourChangCartEditor = async function(host, productId, old, onSave, onCancel = () => {}) {
    host.replaceChildren();
    const title = element("h3", old ? "Sửa món trong giỏ" : "Chọn cấu hình từ Backend"); host.append(title);
    const status = element("p", "Đang tải cấu hình…"); status.setAttribute("role", "status"); host.append(status);
    const form = element("form"); host.append(form);
    const variantSelect = input("Size / nhiệt độ", element("select"), form); variantSelect.dataset.editorVariant = "";
    const brew = input("Đường / đá hợp lệ", element("select"), form); brew.dataset.editorBrew = "";
    const topsHost = element("fieldset"); topsHost.append(element("legend", "Topping — số phần trên mỗi ly")); form.append(topsHost);
    const quantity = input("Số ly", element("input"), form); quantity.type = "number"; quantity.min = "1"; quantity.max = "99"; quantity.required = true; quantity.value = old?.quantity ?? 1;
    const note = input("Ghi chú", element("textarea"), form); note.maxLength = 500; note.value = old?.note ?? "";
    const noteHelp = element("p", "Ghi chú không thay đổi công thức/BOM hoặc giá. Muốn đổi thành phần (ví dụ không sữa) nhưng không có tùy chọn cấu trúc, hãy hỏi quán; không coi ghi chú là yêu cầu đã được xác nhận.");
    noteHelp.className = "fine-print"; form.append(noteHelp);
    const price = element("p"); price.dataset.editorTotal = ""; form.append(price);
    const save = element("button", old ? "Lưu món và xác nhận giá" : "Thêm vào giỏ"); save.type = "submit"; save.disabled = true; save.dataset.editorSave = ""; form.append(save);
    const cancel = element("button", "Đóng / bỏ chỉnh sửa"); cancel.type = "button"; cancel.onclick = onCancel; form.append(cancel);
    let variants = [], product, invalidTops = [], topInputs = [];
    const selected = () => variants.find(v => String(v.id) === variantSelect.value);
    function candidate() {
      const v = selected();
      if (!product || !v?.available || !brew.value) throw new Error("Chọn biến thể và cặp đường/đá còn hiệu lực.");
      const pair = JSON.parse(brew.value);
      if (!v.pairs.some(p => JSON.stringify(p) === brew.value) || invalidTops.length) throw new Error("Cần sửa tùy chọn hoặc bỏ topping không còn hiệu lực.");
      const qty = Number(quantity.value);
      if (!C.integer(qty, 1, 99) || note.value.length > 500) throw new Error("Số ly phải từ 1–99, ghi chú tối đa 500 ký tự.");
      const toppings = topInputs.filter(x => x.input.value !== "0").map(x => {
        const portions = Number(x.input.value);
        if (!C.integer(portions, 1, x.top.limit)) throw new Error("Số phần topping vượt cấu hình.");
        return { id: x.top.id, name: x.top.name, price: x.top.price, portions };
      });
      if (toppings.length > 10) throw new Error("Một dòng tối đa 10 loại topping theo hợp đồng Checkout.");
      const item = { draftVersion: 2, productId, variantId: v.id, name: product.name, size: v.size,
        temperature: v.temperature === "HOT" ? "Nóng" : "Lạnh", sugar: pair[0] + "%", ice: pair[1] + "%",
        quantity: qty, note: note.value.trim(), listedPrice: v.price, toppings, pricingContext: v.context, needsRevalidation: true };
      C.total(item); return item;
    }
    function refresh() {
      try { price.textContent = "Tạm tính gồm topping: " + C.format(C.total(candidate())) + " · Checkout sẽ xác minh lại."; save.disabled = false; }
      catch (e) { price.textContent = e.message; save.disabled = true; }
    }
    function renderOptions() {
      const previousPair = brew.value;
      const rememberedTops = topInputs.length ? [...topInputs.filter(x => Number(x.input.value) > 0)
        .map(x => ({ id: x.top.id, name: x.top.name, portions: Number(x.input.value) })), ...invalidTops] : null;
      const v = selected(); brew.replaceChildren(option("", "Chọn đường / đá"));
      topsHost.replaceChildren(element("legend", "Topping — số phần trên mỗi ly")); topInputs = []; invalidTops = [];
      if (!v) { refresh(); return; }
      for (const pair of v.pairs) brew.append(option(JSON.stringify(pair), `Đường ${pair[0]}% / đá ${pair[1]}%`));
      const previous = previousPair || (old ? JSON.stringify([Number(String(old.sugar).replace("%", "")), Number(String(old.ice).replace("%", ""))]) : "");
      if ([...brew.options].some(o => o.value === previous)) brew.value = previous;
      let oldTops = rememberedTops || (Array.isArray(old?.toppings) ? old.toppings : old?.topping && old.topping !== "Không"
        ? [{ name: old.topping, portions: old.toppingQuantity }] : []);
      oldTops = oldTops.filter(t => t && typeof t === "object");
      const matchesTop = (t, top) => t.id ? t.id === top.id : t.name === top.name &&
        v.tops.filter(x => x.available && x.name === t.name).length === 1;
      for (const top of v.tops.filter(t => t.available)) {
        const field = input(`${top.name} · ${C.format(top.price)}/phần · tối đa ${top.limit}`, element("input"), topsHost);
        field.type = "number"; field.min = "0"; field.max = String(top.limit); field.dataset.toppingId = String(top.id);
        const matches = oldTops.filter(t => matchesTop(t, top));
        field.value = matches.length === 1 ? matches[0].portions : 0;
        topInputs.push({ top, input: field });
      }
      invalidTops = oldTops.filter(t => !topInputs.some(x => matchesTop(t, x.top)));
      for (const t of [...invalidTops]) {
        const row = element("p", `${t.name || "Topping cũ"}: không còn hiệu lực. `);
        const remove = element("button", "Bỏ topping này"); remove.type = "button";
        remove.onclick = () => { invalidTops = invalidTops.filter(x => x !== t); row.remove(); refresh(); }; row.append(remove); topsHost.append(row);
      }
      refresh();
    }
    variantSelect.onchange = renderOptions; form.oninput = refresh; form.onchange = refresh;
    form.onsubmit = async event => {
      event.preventDefault(); if (save.disabled) return;
      save.disabled = true;
      try {
        const item = candidate();
        if (old && (old.pricingContext !== item.pricingContext || old.listedPrice !== item.listedPrice) &&
            !confirm("Giá hoặc cấu hình hiện hành khác bản nháp. Xác nhận lưu theo tạm tính đang hiển thị?")) return;
        await onSave(item); status.textContent = "Đã lưu giỏ nháp; chưa tạo đơn hoặc giữ kho.";
      } catch (e) { status.textContent = e.message; }
      finally { refresh(); }
    };
    try {
      if (!C.integer(productId, 1, Number.MAX_SAFE_INTEGER)) throw new Error("Mã món cũ không hợp lệ; dữ liệu chưa bị xóa.");
      const signal = AbortSignal.timeout(10000);
      const catalog = await json("/Cart?handler=Catalog", signal);
      const matches = Array.isArray(catalog) ? catalog.filter(p => p?.productId === productId && p.available === true) : [];
      if (matches.length !== 1 || typeof matches[0].name !== "string") throw new Error("Món không còn được công bố hoặc nguồn không hợp lệ.");
      product = matches[0]; variants = normalize(await json(`/Cart?handler=Variants&productId=${productId}`, signal));
      variantSelect.replaceChildren(option("", "Chọn size / nhiệt độ"));
      for (const v of variants) { const o = option(String(v.id), `${v.size} / ${v.temperature === "HOT" ? "Nóng" : "Lạnh"} · ${C.format(v.price)}${v.available ? "" : " · Tạm hết"}`); o.disabled = !v.available; variantSelect.append(o); }
      if (old && variants.some(v => v.id === old.variantId && v.available)) variantSelect.value = String(old.variantId);
      status.textContent = old ? "Giữ nguyên dòng cũ cho đến khi bạn lưu. Kiểm tra cấu hình và giá hiện hành." : "Chọn cấu hình, đường/đá và topping từ Backend; không giữ kho.";
      renderOptions();
    } catch (e) { status.textContent = e.name === "TimeoutError" ? "Nguồn cấu hình hết thời gian chờ; giữ nguyên giỏ." : e.message; save.disabled = true; }
  };
})();
