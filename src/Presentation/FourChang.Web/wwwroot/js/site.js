(() => {
    const $ = (s, root = document) => root.querySelector(s);
    const $$ = (s, root = document) => [...root.querySelectorAll(s)];
    const CART_KEY = 'fourchang_cart_v1';
    const fmt = n => new Intl.NumberFormat('vi-VN').format(Number(n || 0)) + 'đ';

    let toastTimer;
    function toast(message) {
        const t = $('#toast');
        if (!t) return;
        t.textContent = message;
        t.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => t.hidden = true, 4500);
    }

    function getCart() {
        try { return JSON.parse(localStorage.getItem(CART_KEY) || '[]'); }
        catch { return []; }
    }
    function saveCart(items) {
        localStorage.setItem(CART_KEY, JSON.stringify(items));
        renderMenuSidebarSummary();
    }

    let activeProduct = {
        variantId: 1,
        maMon: 1,
        name: 'Latte',
        basePrice: 45000,
        image: 2,
        variants: []
    };

    // UC-WEB-04 (Nhánh A2 & BR-11): Khóa Size/Nhiệt độ tạm hết hàng và khóa Đá = 0% khi chọn Nóng
    function syncVariantAvailabilityUI() {
        const rows = $$('.customizer .option-row');
        if (rows.length < 4) return;

        const sizeBtns = $$('button', rows[0]);
        const tempBtns = $$('button', rows[1]);
        const iceBtns = $$('button', rows[3]);

        if (activeProduct.variants.length > 0) {
            sizeBtns.forEach(btn => {
                const s = btn.textContent.trim();
                const hasAvailable = activeProduct.variants.some(v => v.kichCo === s && v.available !== false);
                btn.disabled = !hasAvailable;
                btn.title = hasAvailable ? '' : 'Size này tạm hết hàng';
                if (!hasAvailable && btn.classList.contains('selected')) {
                    btn.classList.remove('selected');
                }
            });
            if (!rows[0].querySelector('button.selected') && sizeBtns.some(b => !b.disabled)) {
                sizeBtns.find(b => !b.disabled).classList.add('selected');
            }
        }

        const tempLabel = rows[1]?.querySelector('button.selected')?.textContent.trim() || 'Lạnh';
        const isHot = tempLabel.includes('Nóng');
        if (isHot) {
            iceBtns.forEach(btn => {
                const isZero = parseInt(btn.textContent, 10) === 0;
                btn.disabled = !isZero;
                btn.classList.toggle('selected', isZero);
            });
        } else {
            iceBtns.forEach(btn => btn.disabled = false);
        }
    }

    $$('[data-choice]').forEach(group => group.addEventListener('click', e => {
        const button = e.target.closest('button');
        if (!button || button.disabled) return;
        $$('button', group).forEach(b => b.classList.toggle('selected', b === button));
        syncVariantAvailabilityUI();
        updateCustomizerPrice();
        renderMenuSidebarSummary();
    }));

    $$('[data-tabs]').forEach(tabs => tabs.addEventListener('click', e => {
        const b = e.target.closest('[data-tab]');
        if (!b) return;
        $$('[data-tab]', tabs).forEach(x => x.classList.toggle('selected', x === b));
        tabs.parentElement.querySelectorAll(':scope > .tab-panel').forEach(p => p.hidden = p.id !== b.dataset.tab);
        renderCheckoutSummary();
    }));

    $$('[data-qty]').forEach(b => b.addEventListener('click', () => {
        const q = $('#quantity');
        if (!q) return;
        q.textContent = Math.max(1, Math.min(99, Number(q.textContent) + Number(b.dataset.qty)));
        updateCustomizerPrice();
    }));

    function getSelectedCustomizations() {
        const rows = $$('.customizer .option-row');
        const size = rows[0]?.querySelector('button.selected')?.textContent.trim() || 'M';
        const tempLabel = rows[1]?.querySelector('button.selected')?.textContent.trim() || 'Lạnh';
        const temp = tempLabel.includes('Nóng') ? 'HOT' : 'ICED';
        const sugar = parseInt(rows[2]?.querySelector('button.selected')?.textContent || '50', 10);
        const ice = temp === 'HOT' ? 0 : parseInt(rows[3]?.querySelector('button.selected')?.textContent || '50', 10);
        const toppingName = $('.customizer .topping-options button.selected')?.textContent.trim() || 'Không';
        const toppingServings = Math.max(1, Math.min(2, Number($('.customizer input[type="number"]')?.value || 1)));
        const note = $('.customizer textarea')?.value.trim() || '';
        const qty = Number($('#quantity')?.textContent || 1);

        let matchedVariant = activeProduct.variants.find(v => v.kichCo === size && v.nhietDo === temp)
            || activeProduct.variants.find(v => v.kichCo === size)
            || activeProduct.variants[0];

        const unitPrice = matchedVariant ? Number(matchedVariant.giaBan) : activeProduct.basePrice + (size === 'L' ? 6000 : size === 'S' ? -4000 : 0);
        const variantId = matchedVariant ? matchedVariant.maCauHinhMon : activeProduct.variantId;

        let toppingId = 0;
        let toppingPrice = 0;
        if (toppingName !== 'Không') {
            toppingPrice = 10000;
            if (matchedVariant?.toppings?.length) {
                const foundTop = matchedVariant.toppings.find(t => t.tenMonThem.toLowerCase().includes(toppingName.toLowerCase()));
                if (foundTop) {
                    toppingId = foundTop.maMonThem;
                    toppingPrice = Number(foundTop.giaMoiPhan);
                }
            }
        }

        return {
            variantId,
            maMon: activeProduct.maMon,
            name: activeProduct.name,
            image: activeProduct.image,
            size,
            temp,
            tempLabel,
            sugar,
            ice,
            toppingId,
            toppingName,
            toppingServings: toppingName === 'Không' ? 0 : toppingServings,
            toppingPrice,
            unitPrice,
            qty,
            note
        };
    }

    function updateCustomizerPrice() {
        if (!$('#selected-price')) return;
        const c = getSelectedCustomizations();
        const lineTotal = (c.unitPrice + c.toppingPrice * c.toppingServings) * c.qty;
        $('#selected-price').textContent = fmt(lineTotal);
    }

    $$('[data-product]').forEach(b => b.addEventListener('click', async () => {
        activeProduct = {
            variantId: Number(b.dataset.id || 1),
            maMon: Number(b.dataset.mamon || 1),
            name: b.dataset.product,
            basePrice: Number(b.dataset.price || 45000),
            image: Number(b.dataset.image || 2),
            variants: []
        };
        $('#selected-name').textContent = activeProduct.name;
        $('.selected-photo').className = 'drink-photo selected-photo sprite-' + activeProduct.image;
        $('.selected-photo').setAttribute('aria-label', activeProduct.name);
        if ($('#quantity')) $('#quantity').textContent = '1';

        try {
            const res = await fetch(`/api/menu/${activeProduct.maMon}/variants`);
            if (res.ok) activeProduct.variants = await res.json();
        } catch { }
        syncVariantAvailabilityUI();
        updateCustomizerPrice();
        toast('Đang tùy chỉnh món ' + activeProduct.name);
    }));

    const addBtn = $('.customizer .add-row .button.primary');
    if (addBtn) {
        addBtn.removeAttribute('data-preview');
        addBtn.addEventListener('click', e => {
            e.preventDefault();
            e.stopImmediatePropagation();
            const item = getSelectedCustomizations();
            const cart = getCart();
            const key = `${item.variantId}_${item.sugar}_${item.ice}_${item.toppingName}_${item.toppingServings}_${item.note}`;
            const existing = cart.find(x => x.key === key);
            if (existing) existing.qty += item.qty;
            else cart.push({ key, ...item });
            saveCart(cart);
            toast(`Đã thêm ${item.qty}x ${item.name} (Size ${item.size}) vào giỏ hàng!`);
        });
    }

    function renderMenuSidebarSummary() {
        const box = $('.fulfillment-summary');
        if (!box) return;
        const cart = getCart();
        const subtotal = cart.reduce((s, i) => s + (i.unitPrice + i.toppingPrice * i.toppingServings) * i.qty, 0);
        const modeBtn = box.querySelector('.segmented button.selected')?.textContent.trim() || 'Giao hàng';
        const ship = modeBtn.includes('Giao') && subtotal > 0 ? 15000 : 0;
        const dds = box.querySelectorAll('.totals dd');
        if (dds.length >= 3) {
            dds[0].textContent = fmt(subtotal);
            dds[1].textContent = fmt(ship);
            dds[2].textContent = fmt(subtotal + ship);
        }
    }

    function renderCartPage() {
        if (location.pathname.toLowerCase() !== '/cart') return;
        const panel = $('.two-columns > section.panel');
        const aside = $('.two-columns > aside.panel');
        if (!panel || !aside) return;

        const cart = getCart();
        $$('.line-item', panel).forEach(el => el.remove());
        const noteField = panel.querySelector('label.field');

        if (cart.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'muted';
            empty.textContent = 'Giỏ hàng của bạn đang trống. Hãy quay lại thực đơn để chọn món nhé!';
            panel.insertBefore(empty, noteField);
        } else {
            cart.forEach((item, idx) => {
                const div = document.createElement('div');
                div.className = 'line-item';
                const topText = item.toppingName === 'Không' ? 'Không topping' : `${item.toppingName} x${item.toppingServings}`;
                const linePrice = (item.unitPrice + item.toppingPrice * item.toppingServings) * item.qty;
                div.innerHTML = `
          <div class="drink-photo sprite-${item.image || 2}"></div>
          <div class="item-content">
            <h3>${item.name} · Size ${item.size}</h3>
            <p>${item.tempLabel} · ${item.sugar}% đường · ${item.ice}% đá · ${topText}</p>
          </div>
          <div class="item-actions">
            <strong>${fmt(linePrice)}</strong>
            <span class="quantity">
              <button type="button" data-cart-minus="${idx}">−</button>
              <output>${item.qty}</output>
              <button type="button" data-cart-plus="${idx}">+</button>
            </span>
            <button type="button" class="text-button" data-cart-remove="${idx}">Xóa món</button>
          </div>`;
                panel.insertBefore(div, noteField);
            });
        }

        const totals = aside.querySelector('dl.totals');
        if (totals) {
            const subtotal = cart.reduce((s, i) => s + (i.unitPrice + i.toppingPrice * i.toppingServings) * i.qty, 0);
            totals.innerHTML = cart.map(i => `<div><dt>${i.name} ${i.size} × ${i.qty}</dt><dd>${fmt((i.unitPrice + i.toppingPrice * i.toppingServings) * i.qty)}</dd></div>`).join('')
                + `<div class="total"><dt>Tạm tính</dt><dd>${fmt(subtotal)}</dd></div>`;
        }

        panel.querySelectorAll('[data-cart-minus]').forEach(b => b.addEventListener('click', () => {
            const c = getCart(); const i = Number(b.dataset.cartMinus);
            if (c[i].qty > 1) c[i].qty--; else c.splice(i, 1);
            saveCart(c); renderCartPage();
        }));
        panel.querySelectorAll('[data-cart-plus]').forEach(b => b.addEventListener('click', () => {
            const c = getCart(); const i = Number(b.dataset.cartPlus);
            c[i].qty++; saveCart(c); renderCartPage();
        }));
        panel.querySelectorAll('[data-cart-remove]').forEach(b => b.addEventListener('click', () => {
            const c = getCart(); c.splice(Number(b.dataset.cartRemove), 1);
            saveCart(c); renderCartPage();
        }));
    }

    function renderCheckoutSummary() {
        if (location.pathname.toLowerCase() !== '/checkout') return;
        const aside = $('.two-columns > aside.panel');
        if (!aside) return;
        const cart = getCart();
        const subtotal = cart.reduce((s, i) => s + (i.unitPrice + i.toppingPrice * i.toppingServings) * i.qty, 0);
        const activeTab = $('[data-tabs] [data-tab].selected')?.dataset.tab || 'delivery';
        const ship = activeTab === 'delivery' && subtotal > 0 ? 15000 : 0;

        const muted = aside.querySelector('p.muted');
        if (muted) muted.textContent = cart.length ? cart.map(i => `${i.name} ${i.size} × ${i.qty}`).join(' · ') : 'Chưa có món trong giỏ';
        const firstDd = aside.querySelector('dl.totals div:first-child dd');
        if (firstDd) firstDd.textContent = fmt(subtotal);
        if ($('#shipping-fee')) $('#shipping-fee').textContent = fmt(ship);
        if ($('#checkout-total')) $('#checkout-total').textContent = fmt(subtotal + ship);
    }

    if (location.pathname.toLowerCase() === '/checkout') {
        const payBtn = $('a.button.primary.full[href="/Checkout/Payment"]');
        if (payBtn) {
            payBtn.addEventListener('click', async e => {
                e.preventDefault();
                const cart = getCart();
                if (cart.length === 0) {
                    toast('Giỏ hàng đang trống, vui lòng chọn món trước khi đặt hàng.');
                    return;
                }
                const activeTab = $('[data-tabs] [data-tab].selected')?.dataset.tab || 'delivery';
                const fulfillmentType = activeTab === 'dinein' ? 'DINE_IN' : activeTab === 'takeaway' ? 'TAKEAWAY' : 'DELIVERY';
                const panel = $(`#${activeTab}`);
                const inputs = panel ? $$('input', panel) : [];

                const payload = {
                    hinhThucNhanMon: fulfillmentType,
                    maBanHienThi: fulfillmentType === 'DINE_IN' ? (inputs[0]?.value || 'TABLE-05') : null,
                    tenNguoiNhan: fulfillmentType !== 'DINE_IN' ? (inputs[0]?.value || 'Khách hàng') : null,
                    dienThoaiNguoiNhan: fulfillmentType !== 'DINE_IN' ? (inputs[1]?.value || '0900000001') : null,
                    diaChiGiao: fulfillmentType === 'DELIVERY' ? (inputs[2]?.value || '') : null,
                    khoangCachKm: fulfillmentType === 'DELIVERY' ? 2.4 : null,
                    ghiChu: panel?.querySelector('textarea')?.value || '',
                    items: cart.map(i => ({
                        maCauHinhMon: i.variantId,
                        soLuong: i.qty,
                        phanTramDuong: i.sugar,
                        phanTramDa: i.ice,
                        ghiChu: i.note,
                        toppings: i.toppingId > 0 ? [{ maMonThem: i.toppingId, soPhanMoiLy: i.toppingServings }] : []
                    }))
                };

                try {
                    const res = await fetch('/api/customer/orders', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(payload)
                    });
                    const data = await res.json();
                    if (!res.ok) {
                        toast(data.message || 'Không thể tạo đơn hàng.');
                        return;
                    }
                    localStorage.setItem('fourchang_last_order', JSON.stringify(data));
                    localStorage.removeItem(CART_KEY);
                    location.href = `/Checkout/Payment?orderId=${data.maDonHang}&code=${data.maDonHienThi}&total=${data.tongCong}`;
                } catch {
                    toast('Lỗi kết nối máy chủ khi tạo đơn hàng.');
                }
            });
        }
    }

    // UC-WEB-04 (Bước 3 & Bước 4): Tìm kiếm tiếng Việt không dấu (BR-31), lọc và sắp xếp
    const search = $('#menu-search');
    if (search) {
        let category = 'Tất cả';
        const cards = $$('.product-card');
        function filter() {
            const normalize = s => (s || '').toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
            const keyword = normalize(search.value);
            const price = $('#price-filter').value;
            let count = 0;
            cards.forEach(c => {
                const ok = normalize(c.dataset.name).includes(keyword)
                    && (category === 'Tất cả' || c.dataset.category === category)
                    && ($('#stock-filter').value === 'all' || c.dataset.available === ($('#stock-filter').value === 'available' ? 'true' : 'false'))
                    && (price === 'all' || (price === 'under' ? Number(c.dataset.price) < 45000 : Number(c.dataset.price) >= 45000));
                c.hidden = !ok;
                if (ok) count++;
            });
            $('#menu-empty').hidden = count > 0;
        }
        search.addEventListener('input', filter);
        $('#stock-filter').addEventListener('change', filter);
        $('#price-filter').addEventListener('change', filter);
        $('#categories').addEventListener('click', e => {
            const b = e.target.closest('button');
            if (!b) return;
            category = b.dataset.category;
            $$('button', $('#categories')).forEach(x => x.classList.toggle('selected', x === b));
            filter();
        });
        $('#reset-filter').addEventListener('click', () => {
            search.value = ''; category = 'Tất cả'; $('#price-filter').value = 'all'; $('#stock-filter').value = 'all';
            if ($('#menu-sort')) $('#menu-sort').value = 'default';
            $$('button', $('#categories')).forEach((x, i) => x.classList.toggle('selected', i === 0));
            $('#product-grid').hidden = false; filter();
        });
        $('#menu-sort').addEventListener('change', e => {
            const mode = e.target.value;
            const sorted = [...cards];
            if (mode === 'asc' || mode === 'desc') {
                sorted.sort((a, b) => (Number(a.dataset.price) - Number(b.dataset.price)) * (mode === 'asc' ? 1 : -1));
            } else if (mode === 'newest') {
                sorted.sort((a, b) => Number(b.dataset.mamon || 0) - Number(a.dataset.mamon || 0));
            } else if (mode === 'bestseller') {
                sorted.sort((a, b) => Number(a.dataset.mamon || 0) - Number(b.dataset.mamon || 0));
            }
            sorted.forEach(c => $('#product-grid').append(c));
        });
    }

    renderMenuSidebarSummary();
    renderCartPage();
    renderCheckoutSummary();
})();
