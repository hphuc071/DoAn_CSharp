
(() => {
  "use strict";

  const STORAGE_KEY = "fourchang.cart-draft.v1";

  const lines = document.getElementById("cart-lines");
  const empty = document.getElementById("cart-empty");
  const count = document.getElementById("cart-count");
  const total = document.getElementById("cart-base-total");
  const clear = document.getElementById("clear-cart");
  const badge = document.getElementById("cart-badge");

  if (!lines) return;

  function readCart() {
    try {
      const stored = JSON.parse(
        localStorage.getItem(STORAGE_KEY) || "[]"
      );
      return Array.isArray(stored)
        ? stored.filter(x => x && typeof x === "object")
        : [];
    } catch {
      return [];
    }
  }

  let cart = readCart();

  const money = amount =>
    new Intl.NumberFormat("vi-VN").format(amount) + "đ";

  function save() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(cart));
      render();
    } catch {
      alert("Không thể lưu giỏ hàng trên trình duyệt.");
    }
  }

  function makeButton(label, className, action) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = className;
    button.textContent = label;
    button.addEventListener("click", action);
    return button;
  }

  function renderItem(item, index) {
    const row = document.createElement("div");
    row.className = "line-item";

    const content = document.createElement("div");
    content.className = "item-content";

    const title = document.createElement("h3");
    title.textContent =
      `${item.name || "Chưa rõ món"} · Size ${item.size || "?"}`;

    const description = document.createElement("p");
    description.textContent = [
      item.temperature,
      item.sugar ? `${item.sugar} đường` : "",
      item.ice ? `${item.ice} đá` : "",
      item.topping && item.topping !== "Không"
        ? `${item.topping} × ${item.toppingQuantity} phần/ly`
        : "Không topping"
    ].filter(Boolean).join(" · ");

    content.append(title, description);

    if (item.note) {
      const note = document.createElement("p");
      note.textContent = "Ghi chú: " + item.note;
      content.append(note);
    }

    const priceInfo = document.createElement("p");
    const basePrice = Number(item.listedPrice);

    priceInfo.textContent =
      item.listedPrice != null &&
      Number.isFinite(basePrice) &&
      basePrice >= 0
        ? "Giá cơ sở lúc chọn: " + money(basePrice)
        : "Chưa có giá tham khảo";

    content.append(priceInfo);

    const actions = document.createElement("div");
    actions.className = "item-actions";

    const priceStatus = document.createElement("small");
    priceStatus.textContent = "Giá cuối chờ API xác nhận";

    const quantityBox = document.createElement("span");
    quantityBox.className = "quantity";

    const qty = Number(item.quantity) || 1;

    const output = document.createElement("output");
    output.textContent = String(qty);

    const minus = makeButton("−", "", () => {
      if (qty <= 1) return;
      cart[index].quantity = qty - 1;
      save();
    });

    const plus = makeButton("+", "", () => {
      if (qty >= 99) {
        alert("Tối đa 99 ly trên mỗi dòng.");
        return;
      }
      cart[index].quantity = qty + 1;
      save();
    });

    quantityBox.append(minus, output, plus);

    const remove = makeButton(
      "Xóa món",
      "text-button",
      () => {
        cart.splice(index, 1);
        save();
      }
    );

    actions.append(priceStatus, quantityBox, remove);
    row.append(content, actions);

    return row;
  }

  function render() {
    lines.replaceChildren();

    const itemCount = cart.reduce(
      (sum, item) => sum + (Number(item.quantity) || 0),
      0
    );

    count.textContent = `${itemCount} ly trong giỏ nháp`;

    if (badge) {
      badge.textContent = String(itemCount);
    }

    empty.hidden = cart.length > 0;
    clear.hidden = cart.length === 0;

    cart.forEach((item, index) => {
      lines.appendChild(renderItem(item, index));
    });

    const validPrices = cart.every(item =>
      item.listedPrice != null &&
      Number.isFinite(Number(item.listedPrice)) &&
      Number(item.listedPrice) >= 0
    );

    if (!cart.length) {
      total.textContent = "0đ";
    } else if (!validPrices) {
      total.textContent = "Chưa xác định";
    } else {
      const baseTotal = cart.reduce(
        (sum, item) =>
          sum + Number(item.listedPrice) *
          (Number(item.quantity) || 0),
        0
      );

      total.textContent = money(baseTotal);
    }
  }

  clear.addEventListener("click", () => {
    if (!confirm("Bạn muốn xóa toàn bộ giỏ nháp?")) {
      return;
    }

    cart = [];
    save();
  });

  window.addEventListener("storage", event => {
    if (event.key === STORAGE_KEY) {
      cart = readCart();
      render();
    }
  });

  render();
})();
