
(() => {
  const STORAGE_KEY = "fourchang.cart-draft.v1";
  const addButton = document.getElementById("add-to-cart");

  // Chỉ xử lý trên trang có phần tùy chỉnh món.
  if (!addButton) return;

  let activeProduct = null;

  // Nhận món khách đã chọn từ trang thực đơn.
  document.querySelectorAll(".product-card [data-product]")
    .forEach(button => {
      button.addEventListener("click", () => {
        activeProduct = {
          name: button.dataset.product,
          productId: button.dataset.productId
            ? Number(button.dataset.productId)
            : null,
          listedPrice: Number(button.dataset.price)
        };
      });
    });

  function readCart() {
    try {
      const data = JSON.parse(
        localStorage.getItem(STORAGE_KEY) || "[]"
      );
      return Array.isArray(data) ? data : [];
    } catch {
      return [];
    }
  }

  function notify(message) {
    const toast = document.getElementById("toast");
    if (!toast) {
      alert(message);
      return;
    }

    toast.textContent = message;
    toast.hidden = false;

    clearTimeout(notify.timer);
    notify.timer = setTimeout(() => {
      toast.hidden = true;
    }, 3500);
  }

  function updateBadge() {
    const badge = document.getElementById("cart-badge");
    if (!badge) return;

    const total = readCart().reduce(
      (sum, item) => sum + item.quantity, 0
    );

    badge.textContent = total;
  }

  function selectedText(element) {
    return element
      ?.querySelector("button.selected")
      ?.textContent.trim() || "";
  }

  addButton.addEventListener("click", () => {
    if (!activeProduct) {
      notify("Vui lòng chọn món từ thực đơn trước.");
      return;
    }

    const choices = document.querySelectorAll(
      ".customizer .option-row [data-choice]"
    );

    const size = selectedText(choices[0]);
    const temperature = selectedText(choices[1]);
    const sugar = selectedText(choices[2]);
    const ice = selectedText(choices[3]);

    const topping = selectedText(
      document.querySelector(".topping-options")
    );

    const toppingInput = document.querySelector(
      'input[aria-label="Số phần topping trên ly"]'
    );

    const toppingQuantity = topping === "Không"
      ? 0
      : Number(toppingInput?.value);

    const quantity = Number(
      document.getElementById("quantity")?.textContent
    );

    const note = document.querySelector(
      ".customizer textarea"
    )?.value.trim() || "";

    if (!size || !temperature || !sugar || !ice || !topping) {
      notify("Vui lòng chọn đầy đủ tùy chỉnh món.");
      return;
    }

    if (!Number.isInteger(quantity) ||
        quantity < 1 || quantity > 99) {
      notify("Số lượng ly phải từ 1 đến 99.");
      return;
    }

    if (topping !== "Không" &&
        (!Number.isInteger(toppingQuantity) ||
         toppingQuantity < 1 || toppingQuantity > 2)) {
      notify("Số phần topping phải từ 1 đến 2.");
      return;
    }

    const key = JSON.stringify([
      activeProduct.productId ?? activeProduct.name,
      size, temperature, sugar, ice,
      topping, toppingQuantity, note,
      activeProduct.listedPrice
    ]);

    const cart = readCart();
    const existing = cart.find(item => item.key === key);

    if (existing) {
      if (existing.quantity + quantity > 99) {
        notify("Mỗi dòng giỏ chỉ được tối đa 99 ly.");
        return;
      }
      existing.quantity += quantity;
    } else {
      cart.push({
        key,
        productId: activeProduct.productId,
        variantId: null,
        name: activeProduct.name,
        size,
        temperature,
        sugar,
        ice,
        topping,
        toppingQuantity,
        quantity,
        note,
        listedPrice: activeProduct.listedPrice,
        needsRevalidation: true
      });
    }

    try {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify(cart)
      );
      updateBadge();
      notify(`Đã lưu ${quantity} ly ${activeProduct.name} vào giỏ nháp.`);
    } catch {
      notify("Không lưu được giỏ hàng trên trình duyệt.");
    }
  });

  updateBadge();
})();
