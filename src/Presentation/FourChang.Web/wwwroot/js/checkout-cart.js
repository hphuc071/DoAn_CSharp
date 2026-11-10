/*
 * UC-WEB-05 -> UC-WEB-06: read-only synchronization of the local cart draft.
 * This renderer never submits orders; checkout-order.js owns order creation.
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

    if (!countElement || !itemsElement || !subtotalElement || !shippingElement || !totalElement || !warningElement) {
        return;
    }

    const money = amount => new Intl.NumberFormat("vi-VN").format(amount) + "đ";

    function readCart() {
        // Never silently drop malformed draft rows or pretend a broken draft is empty.
        // UC05 requires preserving the original draft for repair before Checkout.
        const stored = localStorage.getItem(KEY);
        const result = stored === null ? [] : JSON.parse(stored);
        if (!Array.isArray(result) || result.some(x => !x || typeof x !== "object" || Array.isArray(x)))
            throw new Error("Giỏ nháp không hợp lệ.");
        return result;
    }

    function validPrice(item) {
        // Match cart-draft.js: a numeric string is not a verified price snapshot.
        return Number.isSafeInteger(item.listedPrice) && item.listedPrice >= 0;
    }

    function validQuantity(item) {
        return Number.isSafeInteger(item.quantity) && item.quantity >= 1 && item.quantity <= 99;
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
        const activeMode = tabsElement?.querySelector("[data-tab].selected")?.dataset.tab || "delivery";
        itemsElement.replaceChildren();
        let cart;
        try {
            cart = readCart();
        } catch {
            countElement.textContent = "Không đọc được giỏ nháp; cần kiểm tra lại.";
            appendLine("Giỏ nháp cần sửa", "Không xác định");
            subtotalElement.textContent = "Không xác định";
            shippingElement.textContent = "Chưa báo giá";
            totalElement.textContent = "Chưa thể đặt đơn";
            warningElement.textContent = "Giỏ lưu trên máy không hợp lệ hoặc không truy cập được. Chưa xóa hay ghi đè dữ liệu; quay lại Giỏ hàng để kiểm tra trước khi đặt.";
            return;
        }

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
            let price = priceOk ? Number(item.listedPrice) : 0;
            const name = typeof item.name === "string" ? item.name : "Chưa rõ món";
            const size = typeof item.size === "string" ? item.size : "?";
            const toppingName = typeof item.topping === "string" ? item.topping : "Không";
            const toppingQty = Number(item.toppingQuantity) || 0;
            let containsTopping = toppingName !== "Không" && toppingQty > 0;
            let toppingLabel = containsTopping ? ` · ${toppingName} × ${toppingQty}/ly` : "";
            let snapshotOk = true;
            if (item.draftVersion === 2) {
                snapshotOk = Array.isArray(item.toppings) && item.toppings.length <= 10 &&
                    new Set(item.toppings.map(t => t?.id)).size === item.toppings.length;
                if (snapshotOk) {
                    for (const top of item.toppings) {
                        if (!top || !Number.isSafeInteger(top.id) || top.id <= 0 ||
                            !Number.isSafeInteger(top.portions) || top.portions < 1 ||
                            !Number.isSafeInteger(top.price) || top.price < 0 ||
                            !Number.isSafeInteger(top.price * top.portions) ||
                            !Number.isSafeInteger(price + top.price * top.portions)) { snapshotOk = false; break; }
                        price += top.price * top.portions;
                    }
                    toppingLabel = item.toppings.map(t => ` · ${t?.name || "?"} × ${t?.portions || "?"}/ly`).join("");
                }
                containsTopping = false; // All V2 topping prices are included, or the row is invalid.
            }

            if (!qtyOk || !priceOk || !snapshotOk || !Number.isSafeInteger(qty * price) ||
                !Number.isSafeInteger(baseSubtotal + qty * price) || !Number.isSafeInteger(item.variantId) ||
                item.variantId <= 0) {
                invalid = true;
            }

            hasTopping = hasTopping || containsTopping;
            totalQuantity += qty;
            baseSubtotal += qty * price;

            const label = `${name} · ${size} × ${qtyOk ? qty : "?"}` +
                toppingLabel;
            appendLine(label, qtyOk && priceOk && snapshotOk && Number.isSafeInteger(qty * price)
                ? money(qty * price) + (containsTopping ? " + topping" : "") : "Chưa xác định");
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
            // Render after site.js selects the active tab; no demo fee is used.
            queueMicrotask(render);
        }
    });
    window.addEventListener("storage", event => {
        if (event.key === KEY || event.key === null) render();
    });
    window.addEventListener("pageshow", render);
    window.addEventListener("checkout:refresh", render);
    // Re-render after an edit from this tab; storage fires only in other tabs.
    window.addEventListener("cart:changed", render);
    render();
})();
