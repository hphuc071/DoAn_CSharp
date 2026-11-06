/*
 * UC-WEB-05 -> UC-WEB-06: read-only synchronization of the local cart draft.
 * No POST requests, no payment instructions, and no order creation.
 * The Backend remains authoritative for variant, topping, stock, fees and totals.
 */
(() => {
    "use strict";

    const KEY = "fourchang.cart-draft.v1";
    const countElement = document.getElementById("checkout-cart-count");
    const itemsElement = document.getElementById("checkout-items");
    const subtotalElement = document.getElementById("checkout-base-subtotal");
    const shippingElement = document.getElementById("shipping-fee");
    const totalElement = document.getElementById("checkout-total");
    const warningElement = document.getElementById("checkout-price-warning");
    const tabsElement = document.querySelector("[data-tabs]");

    if (!countElement || !itemsElement || !subtotalElement || !shippingElement || !totalElement) {
        return;
    }

    const money = amount => new Intl.NumberFormat("vi-VN").format(amount) + "đ";

    function readCart() {
        try {
            const result = JSON.parse(localStorage.getItem(KEY) || "[]");
            return Array.isArray(result) ? result.filter(x => x && typeof x === "object") : [];
        } catch {
            return [];
        }
    }

    function validPrice(item) {
        const amount = Number(item.listedPrice);
        return item.listedPrice !== null && item.listedPrice !== undefined &&
            Number.isFinite(amount) && amount >= 0;
    }

    function validQuantity(item) {
        const n = Number(item.quantity);
        return Number.isSafeInteger(n) && n >= 1 && n <= 99;
    }

    function appendLine(description, amount) {
        const row = document.createElement("div");
        const title = document.createElement("dt");
        const value = document.createElement("dd");
        title.textContent = description;
        value.textContent = amount;
        row.append(title, value);
        itemsElement.append(row);
    }

    function render() {
        const cart = readCart();
        const activeMode = tabsElement?.querySelector("[data-tab].selected")?.dataset.tab || "delivery";
        itemsElement.replaceChildren();

        if (!cart.length) {
            countElement.textContent = "Giỏ hàng đang trống.";
            appendLine("Chưa có món", "0đ");
            subtotalElement.textContent = "0đ";
            shippingElement.textContent = activeMode === "delivery" ? "Chưa báo giá" : "0đ";
            totalElement.textContent = "Chưa thể đặt đơn";
            warningElement.textContent = "Hãy quay lại Giỏ hàng để chọn món trước khi đặt đơn.";
            return;
        }

        let baseSubtotal = 0;
        let totalQuantity = 0;
        let hasTopping = false;
        let invalid = false;

        cart.forEach(item => {
            const qtyOk = validQuantity(item);
            const priceOk = validPrice(item);
            const qty = qtyOk ? Number(item.quantity) : 0;
            const price = priceOk ? Number(item.listedPrice) : 0;
            const name = typeof item.name === "string" ? item.name : "Chưa rõ món";
            const size = typeof item.size === "string" ? item.size : "?";
            const toppingName = typeof item.topping === "string" ? item.topping : "Không";
            const toppingQty = Number(item.toppingQuantity) || 0;
            const containsTopping = toppingName !== "Không" && toppingQty > 0;

            if (!qtyOk || !priceOk || !Number.isSafeInteger(Number(item.variantId)) ||
                Number(item.variantId) <= 0) {
                invalid = true;
            }

            hasTopping = hasTopping || containsTopping;
            totalQuantity += qty;
            baseSubtotal += qty * price;

            const label = `${name} · ${size} × ${qtyOk ? qty : "?"}` +
                (containsTopping ? ` · ${toppingName} × ${toppingQty}/ly` : "");
            appendLine(label, qtyOk && priceOk ? money(qty * price) + (containsTopping ? " + topping" : "") : "Chưa xác định");
        });

        countElement.textContent = `${totalQuantity} ly · ${cart.length} dòng giỏ nháp`;
        subtotalElement.textContent = invalid ? "Chưa xác định" : money(baseSubtotal);
        shippingElement.textContent = activeMode === "delivery" ? "Chưa báo giá" : "0đ";
        totalElement.textContent = "Chờ Backend xác nhận";

        if (invalid) {
            warningElement.textContent = "Giỏ có số lượng, biến thể hoặc giá tham khảo không hợp lệ. Quay lại Giỏ hàng để kiểm tra.";
        } else if (hasTopping) {
            warningElement.textContent = "Tạm tính chưa gồm topping. Cần tra cứu mã topping và xác nhận giá với Backend trước khi tạo đơn.";
        } else if (activeMode === "delivery") {
            warningElement.textContent = "Tạm tính chưa gồm phí giao. Chưa có bước tính khoảng cách và báo phí từ Backend.";
        } else {
            warningElement.textContent = "Đây là giá cơ sở lúc chọn món; giá chính thức, tồn kho và cấu hình vẫn cần Backend xác nhận.";
        }
    }

    tabsElement?.addEventListener("click", event => {
        if (event.target.closest("[data-tab]")) {
            // site.js updates the selected tab and its old demo totals in this event;
            // render after those handlers to overwrite demo prices safely.
            queueMicrotask(render);
        }
    });
    window.addEventListener("storage", event => {
        if (event.key === KEY) render();
    });
    window.addEventListener("pageshow", render);
    render();
})();
