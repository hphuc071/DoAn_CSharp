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
    const title = element("h3", old ? "Sửa món trong giỏ" : "Tùy chỉnh món của bạn"); host.append(title);
    const status = element("p", "Đang tải cấu hình…"); status.setAttribute("role", "status"); host.append(status);
    const form = element("form"); host.append(form);
    const variantSelect = input("Size / nhiệt độ", element("select"), form); variantSelect.dataset.editorVariant = "";
    const brew = input("Đường / đá hợp lệ", element("select"), form); brew.dataset.editorBrew = "";
    // Hai select ẩn giữ mã cấu hình/cặp định mức gốc; khách thao tác bằng các trường riêng bên dưới.
    variantSelect.closest(".field").hidden = true; brew.closest(".field").hidden = true;
    const size = input("Kích cỡ", element("select"), form); size.dataset.editorSize = "";
    const temperature = input("Nóng / Lạnh", element("select"), form); temperature.dataset.editorTemperature = "";
    const sugar = input("Mức đường (%)", element("select"), form); sugar.dataset.editorSugar = "";
    const ice = input("Lượng đá (%)", element("select"), form); ice.dataset.editorIce = "";
    const topsHost = element("fieldset"); topsHost.append(element("legend", "Topping — số phần trên mỗi ly")); form.append(topsHost);
    const quantity = input("Số ly", element("input"), form); quantity.type = "number"; quantity.min = "1"; quantity.max = "99"; quantity.required = true; quantity.value = old?.quantity ?? 1;
    const note = input("Ghi chú", element("textarea"), form); note.maxLength = 500; note.value = old?.note ?? "";
    const price = element("p"); price.dataset.editorTotal = ""; form.append(price);
    const save = element("button", old ? "Lưu món và xác nhận giá" : "Thêm vào giỏ"); save.type = "submit"; save.disabled = true; save.dataset.editorSave = ""; form.append(save);
    const pay = element("button", "Thanh toán"); pay.type = "submit"; pay.disabled = true; pay.dataset.editorCheckout = ""; form.append(pay);
    const cancel = element("button", "Đóng / bỏ chỉnh sửa"); cancel.type = "button"; cancel.onclick = onCancel; form.append(cancel);
    const reload = element("button", "Kiểm tra cấu hình mới"); reload.type = "button";
    reload.onclick = async () => {
      try { const draft = candidate(); await window.FourChangCartEditor(host, productId, draft, onSave, onCancel); }
      catch (error) { status.textContent = error.message; }
    };
    form.append(reload);
    let variants = [], product, invalidTops = [], topInputs = [], submitting = false;
    const selected = () => variants.find(v => String(v.id) === variantSelect.value);
    /** Lấy nguyên mã cấu hình và cặp đường/đá hợp lệ từ server; không tự tạo giá hoặc tổ hợp mới. */
    function candidate() {
      const v = selected();
      if (!product || !v?.available || !brew.value) throw new Error("Chọn biến thể và cặp đường/đá còn hiệu lực.");
      const pair = JSON.parse(brew.value);
      if (v.temperature === "HOT" && pair[1] !== 0) throw new Error("Món nóng chỉ nhận cấu hình không đá.");
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
    /** Cập nhật số tiền và khóa cả hai hành động trong khi gửi để tránh thêm món hai lần. */
    function refresh() {
      try { price.textContent = "Tạm tính gồm topping: " + C.format(C.total(candidate())) + " · Giá được kiểm tra lại khi thanh toán."; save.disabled = submitting; pay.disabled = submitting; }
      catch (e) { price.textContent = e.message; save.disabled = true; pay.disabled = true; }
    }
    /** Dựng lựa chọn từ đúng các cấu hình có bán, tách size và nhiệt độ nhưng giữ mã biến thể gốc. */
    function renderTemperatures(remembered = temperature.value) {
      temperature.replaceChildren(option("", "Chọn nóng / lạnh"));
      for (const value of ["HOT", "ICED"]) {
        const matches = variants.filter(v => v.size === size.value && v.temperature === value);
        if (!matches.length) continue;
        const o = option(value, value === "HOT" ? "Nóng" : "Lạnh");
        o.disabled = !matches.some(v => v.available); temperature.append(o);
      }
      if ([...temperature.options].some(o => o.value === remembered && !o.disabled)) temperature.value = remembered;
      else { const choices = [...temperature.options].filter(o => o.value && !o.disabled); if (choices.length === 1) temperature.value = choices[0].value; }
    }
    /** Chỉ ánh xạ tổ hợp size/nhiệt độ duy nhất; tổ hợp thiếu hoặc trùng không được lưu giỏ. */
    function selectVariant() {
      const matches = variants.filter(v => v.available && v.size === size.value && v.temperature === temperature.value);
      variantSelect.value = matches.length === 1 ? String(matches[0].id) : "";
      renderOptions();
    }
    /** Hiện mức đường thuộc BOM; món nóng chỉ nhận cặp có đá 0, không cho giấu đá của công thức sai. */
    function renderSugarAndIce() {
      const v = selected();
      const pairs = v ? v.pairs.filter(p => v.temperature !== "HOT" || p[1] === 0) : [];
      const pair = pairs.find(p => JSON.stringify(p) === brew.value);
      sugar.replaceChildren(option("", "Chọn mức đường"));
      for (const value of [...new Set(pairs.map(p => p[0]))]) sugar.append(option(String(value), value + "%"));
      sugar.value = pair ? String(pair[0]) : "";
      ice.closest(".field").hidden = v?.temperature !== "ICED";
      renderIce(pair ? String(pair[1]) : "");
    }
    /** Lọc lượng đá theo mức đường hiện tại để chỉ cho chọn cặp có thật trong cấu hình. */
    function renderIce(remembered = ice.value) {
      const v = selected(); ice.replaceChildren(option("", "Chọn lượng đá"));
      if (!v || sugar.value === "") return;
      for (const value of [...new Set(v.pairs.filter(p => p[0] === Number(sugar.value)).map(p => p[1]))])
        ice.append(option(String(value), value + "%"));
      if ([...ice.options].some(o => o.value === remembered)) ice.value = remembered;
    }
    /** Đồng bộ trường riêng về cặp định mức gốc, giữ kiểm tra hợp lệ tập trung ở candidate(). */
    function selectPair() {
      const v = selected();
      const value = v?.temperature === "HOT" ? "0" : ice.value;
      const pair = sugar.value !== "" && value !== "" ? [Number(sugar.value), Number(value)] : null;
      brew.value = pair && v?.pairs.some(p => JSON.stringify(p) === JSON.stringify(pair)) ? JSON.stringify(pair) : "";
      refresh();
    }
    /** Nạp lại topping và tùy chọn theo biến thể; giữ nguyên các lựa chọn vẫn còn hợp lệ. */
    function renderOptions() {
      const previousPair = brew.value;
      const rememberedTops = topInputs.length ? [...topInputs.filter(x => Number(x.input.value) > 0)
        .map(x => ({ id: x.top.id, name: x.top.name, portions: Number(x.input.value) })), ...invalidTops] : null;
      const v = selected(); brew.replaceChildren(option("", "Chọn đường / đá"));
      topsHost.replaceChildren(element("legend", "Topping — số phần trên mỗi ly")); topInputs = []; invalidTops = [];
      if (!v) { renderSugarAndIce(); refresh(); return; }
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
      renderSugarAndIce(); refresh();
    }
    size.onchange = () => { renderTemperatures(); selectVariant(); };
    temperature.onchange = selectVariant;
    sugar.onchange = () => { renderIce(); selectPair(); };
    ice.onchange = selectPair;
    brew.onchange = () => { renderSugarAndIce(); refresh(); };
    variantSelect.onchange = () => { const v = selected(); size.value = v?.size ?? ""; renderTemperatures(v?.temperature ?? ""); renderOptions(); }; form.oninput = refresh; form.onchange = refresh;
    form.onsubmit = async event => {
      event.preventDefault(); if (save.disabled) return;
      submitting = true; save.disabled = true; pay.disabled = true;
      try {
        const item = candidate();
        // Đọc lại lựa chọn trước lưu nháp; không tự đổi giá hoặc topping mà khách chưa chấp thuận.
        const latest = normalize(await json(`/Cart?handler=Variants&productId=${productId}`, AbortSignal.timeout(10000)));
        const current = latest.find(v => v.id === item.variantId);
        if (!current?.available) throw new Error("Cấu hình vừa hết hoặc ngừng bán; giữ dữ liệu đang nhập để chọn lại.");
        if (current.context !== item.pricingContext) throw new Error("Giá hoặc cấu hình vừa thay đổi. Bấm Kiểm tra cấu hình mới để xem và xác nhận; dữ liệu đang nhập được giữ.");
        if (old && (old.pricingContext !== item.pricingContext || old.listedPrice !== item.listedPrice) &&
            !confirm("Giá hoặc cấu hình hiện hành khác bản nháp. Xác nhận lưu theo tạm tính đang hiển thị?")) return;
        await onSave(item, { checkout: event.submitter === pay }); status.textContent = "Đã lưu giỏ nháp; chưa tạo đơn hoặc giữ kho.";
      } catch (e) { status.textContent = e.message; }
      finally { submitting = false; refresh(); }
    };
    try {
      if (!C.integer(productId, 1, Number.MAX_SAFE_INTEGER)) throw new Error("Mã món cũ không hợp lệ; dữ liệu chưa bị xóa.");
      const signal = AbortSignal.timeout(10000);
      const catalog = await json("/Cart?handler=Catalog", signal);
      const matches = Array.isArray(catalog) ? catalog.filter(p => p?.productId === productId && p.available === true) : [];
      if (matches.length !== 1 || typeof matches[0].name !== "string") throw new Error("Món không còn được công bố hoặc nguồn không hợp lệ.");
      product = matches[0]; title.textContent = (old ? "Sửa món: " : "Cấu hình: ") + product.name; variants = normalize(await json(`/Cart?handler=Variants&productId=${productId}`, signal));
      variantSelect.replaceChildren(option("", "Chọn size / nhiệt độ"));
      for (const v of variants) { const o = option(String(v.id), `${v.size} / ${v.temperature === "HOT" ? "Nóng" : "Lạnh"} · ${C.format(v.price)}${v.available ? "" : " · Tạm hết"}`); o.disabled = !v.available; o.dataset.size = v.size; o.dataset.temperature = v.temperature; variantSelect.append(o); }
      if (old && variants.some(v => v.id === old.variantId && v.available)) variantSelect.value = String(old.variantId);
      size.replaceChildren(option("", "Chọn kích cỡ"));
      for (const value of [...new Set(variants.map(v => v.size))].sort((a,b) => { const rank = s => ({S:0,M:1,L:2}[s] ?? 99); return rank(a)-rank(b) || a.localeCompare(b); })) {
        const o = option(value, value); o.disabled = !variants.some(v => v.size === value && v.available); size.append(o);
      }
      size.value = selected()?.size ?? ""; renderTemperatures(selected()?.temperature ?? "");
      status.textContent = old ? "Giữ nguyên dòng cũ cho đến khi bạn lưu. Kiểm tra cấu hình và giá hiện hành." : "Chọn kích cỡ, đường/đá và topping theo khẩu vị của bạn.";
      renderOptions();
    } catch (e) { status.textContent = e.name === "TimeoutError" ? "Nguồn cấu hình hết thời gian chờ; giữ nguyên giỏ." : e.message; save.disabled = true; pay.disabled = true; }
  };
})();
