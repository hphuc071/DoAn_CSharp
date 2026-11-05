
(() => {
  "use strict";

  const KEY = "fourchang.cart-draft.v1";
  const add = document.getElementById("add-to-cart");
  const customizer = document.querySelector(".customizer");

  if (!add || !customizer) return;

  const groups = [
    ...customizer.querySelectorAll(".option-row [data-choice]")
  ];

  const priceLabel = document.getElementById("selected-price");

  let product = null;
  let variants = [];
  let requestId = 0;

  add.disabled = true;

  const selected = group =>
    group?.querySelector("button.selected")?.textContent.trim() || "";

  const money = value =>
    new Intl.NumberFormat("vi-VN").format(value) + "đ";

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

  function readCart() {
    try {
      const result = JSON.parse(
        localStorage.getItem(KEY) || "[]"
      );
      return Array.isArray(result) ? result : [];
    } catch {
      return [];
    }
  }

  function updateBadge() {
    const badge = document.getElementById("cart-badge");
    if (!badge) return;

    const count = readCart().reduce(
      (sum, item) => sum + (Number(item.quantity) || 0),
      0
    );

    badge.textContent = String(count);
  }

  async function getJson(url) {
    const response = await fetch(url, {
      headers: { Accept: "application/json" },
      cache: "no-store"
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    return response.json();
  }

  function updateVariant() {
    if (!product) return;

    const size = selected(groups[0]);
    const temperature =
      selected(groups[1]) === "Nóng" ? "HOT" : "ICED";

    const variant = variants.find(v =>
      String(v.kichCo).toUpperCase() === size.toUpperCase() &&
      String(v.nhietDo).toUpperCase() === temperature
    );

    product.variantId = variant
      ? Number(variant.maCauHinhMon)
      : null;

    product.listedPrice = variant
      ? Number(variant.giaBan)
      : null;

    add.disabled =
      !Number.isSafeInteger(product.variantId) ||
      product.variantId <= 0 ||
      !Number.isFinite(product.listedPrice) ||
      product.listedPrice < 0;

    priceLabel.textContent = add.disabled
      ? "Cấu hình chưa được bán"
      : money(product.listedPrice);
  }

  // Khi khách chọn món, truy vấn mã và các biến thể từ API.
  document.querySelectorAll(".product-card [data-product]")
    .forEach(button => {
      button.addEventListener("click", async () => {
        const current = ++requestId;

        product = null;
        variants = [];
        add.disabled = true;

        try {
          const catalog = await getJson(
            "/Cart?handler=Catalog"
          );

          if (current !== requestId) return;

          const matches = catalog.filter(
            item => item.name === button.dataset.product
          );

          if (matches.length !== 1 || !matches[0].available) {
            throw new Error("Không tìm thấy món hợp lệ.");
          }

          const found = matches[0];

          const list = await getJson(
            "/Cart?handler=Variants&productId=" +
            encodeURIComponent(found.productId)
          );

          if (current !== requestId) return;

          variants = Array.isArray(list) ? list : [];

          product = {
            productId: Number(found.productId),
            name: found.name,
            variantId: null,
            listedPrice: null
          };

          updateVariant();

          if (add.disabled) {
            notify(
              "Size hoặc nóng/lạnh hiện chọn chưa có biến thể được bán."
            );
          }
        } catch {
          if (current !== requestId) return;

          add.disabled = true;

          notify(
            "Chưa lấy được dữ liệu món từ API. " +
            "Kiểm tra Backend rồi thử lại."
          );
        }
      });
    });

  // Khi đổi size hoặc nóng/lạnh, chọn lại variantId.
  groups.slice(0, 2).forEach(group => {
    group.addEventListener("click", event => {
      if (event.target.closest("button")) {
        updateVariant();
      }
    });
  });

  add.addEventListener("click", () => {
    if (!product || !product.variantId || add.disabled) {
      notify("Vui lòng chọn món và biến thể hợp lệ.");
      return;
    }

    const size = selected(groups[0]);
    const temperature = selected(groups[1]);
    const sugar = selected(groups[2]);
    const ice = selected(groups[3]);

    const topping = selected(
      customizer.querySelector(".topping-options")
    );

    const toppingInput = customizer.querySelector(
      'input[aria-label="Số phần topping trên ly"]'
    );

    const toppingQuantity = topping === "Không"
      ? 0
      : Number(toppingInput?.value);

    const quantity = Number(
      document.getElementById("quantity")?.textContent
    );

    const note = customizer.querySelector("textarea")
      ?.value.trim() || "";

    if (![size, temperature, sugar, ice, topping].every(Boolean)) {
      notify("Vui lòng chọn đầy đủ tùy chỉnh.");
      return;
    }

    if (!Number.isInteger(quantity) ||
        quantity < 1 || quantity > 99) {
      notify("Số ly phải từ 1 đến 99.");
      return;
    }

    if (topping !== "Không" &&
        (!Number.isInteger(toppingQuantity) ||
         toppingQuantity < 1 || toppingQuantity > 2)) {
      notify("Topping phải từ 1 đến 2 phần/ly.");
      return;
    }

    const key = JSON.stringify([
      product.productId,
      product.variantId,
      sugar,
      ice,
      topping,
      toppingQuantity,
      note
    ]);

    const cart = readCart();
    const existing = cart.find(item => item.key === key);

    if (existing) {
      const before = Number(existing.quantity);

      if (!Number.isInteger(before) ||
          before < 1 || before + quantity > 99) {
        notify("Một dòng giỏ chỉ được tối đa 99 ly.");
        return;
      }

      existing.quantity = before + quantity;
      existing.listedPrice = product.listedPrice;
      existing.needsRevalidation = true;
    } else {
      cart.push({
        key,
        productId: product.productId,
        variantId: product.variantId,
        name: product.name,
        size,
        temperature,
        sugar,
        ice,
        topping,
        toppingQuantity,
        quantity,
        note,
        listedPrice: product.listedPrice,
        needsRevalidation: true
      });
    }

    try {
      localStorage.setItem(KEY, JSON.stringify(cart));
      updateBadge();

      notify(
        `Đã thêm ${quantity} ly ${product.name} vào giỏ nháp.`
      );
    } catch {
      notify("Không lưu được giỏ nháp trên trình duyệt.");
    }
  });

  updateBadge();
})();
