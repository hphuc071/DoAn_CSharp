/* ADM-02/05: cập nhật menu từ server; không xóa giỏ, không ghi đè form khách đang nhập. */
(() => {
  "use strict";
  const grid = document.getElementById("product-grid");
  if (!grid) return;
  let loading = false;
  const money = value => new Intl.NumberFormat("vi-VN").format(value) + "đ";
  const status = document.createElement("p");
  status.className = "muted"; status.setAttribute("role", "status"); status.hidden = true;
  grid.before(status);

  /** Tạo card theo layout hiện có bằng textContent để nội dung từ database không chạy HTML/script. */
  function createCard() {
    const card = document.createElement("article"); card.className = "product-card";
    const photo = document.createElement("div"); photo.className = "drink-photo"; photo.setAttribute("role", "img");
    const badge = document.createElement("span"); badge.className = "product-badge gray";
    const info = document.createElement("div"); info.className = "product-info";
    const content = document.createElement("div"); content.append(document.createElement("h3"), document.createElement("strong"));
    const button = document.createElement("button"); button.type = "button"; button.className = "round-add"; button.textContent = "+";
    info.append(content, button); card.append(photo, badge, info); return card;
  }

  /** Đổi phần hiển thị giá/còn hết, giữ nguyên ID để giỏ tiếp tục dùng mã cấu hình server. */
  function updateCard(card, dish) {
    const variant = dish.cauHinh.find(x => x.laMacDinh) ?? dish.cauHinh[0];
    const available = dish.dangBan && dish.cauHinh.some(x => x.conHang);
    Object.assign(card.dataset, { mamon: String(dish.maMon), name: dish.tenMon, category: dish.tenDanhMuc, price: String(variant.giaBan), available: String(available) });
    card.classList.toggle("sold-out", !available);
    card.querySelector("h3").textContent = dish.tenMon;
    card.querySelector("strong").textContent = money(variant.giaBan);
    let badge = card.querySelector(".product-badge");
    if (!badge) { badge = document.createElement("span"); badge.className = "product-badge gray"; card.append(badge); }
    badge.hidden = available; badge.textContent = "Tạm hết";
    const button = card.querySelector(".round-add"); button.disabled = !available;
    Object.assign(button.dataset, { product: dish.tenMon, productId: String(dish.maMon), price: String(variant.giaBan) });
    button.setAttribute("aria-label", "Chọn " + dish.tenMon);
    const photo = card.querySelector(".drink-photo"); photo.setAttribute("aria-label", dish.tenMon);
    if (/^sprite:[0-9]+$/.test(dish.duongDanAnh ?? "")) { photo.replaceChildren(); photo.className = "drink-photo sprite-" + dish.duongDanAnh.slice(7); }
    else if (dish.duongDanAnh?.startsWith("/") || dish.duongDanAnh?.startsWith("https://")) {
      photo.className = "drink-photo"; const image = document.createElement("img"); image.src = dish.duongDanAnh; image.alt = dish.tenMon;
      image.style.cssText = "width:100%;height:100%;object-fit:cover"; photo.replaceChildren(image);
    }
  }

  /** Tải snapshot hiện hành; xóa khỏi danh sách chỉ khi Admin chủ động bỏ công bố/danh mục bị ẩn. */
  async function refresh() {
    if (loading || document.hidden) return;
    loading = true;
    try {
      const response = await fetch("/catalog/snapshot", { cache: "no-store", headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error("Chưa lấy được cập nhật thực đơn; nội dung đang hiển thị có thể cũ.");
      const snapshot = await response.json();
      const dishes = snapshot.mon.filter(x => x.cauHinh.length > 0);
      const ids = new Set(dishes.map(x => String(x.maMon)));
      grid.querySelectorAll(".product-card").forEach(card => { if (!ids.has(card.dataset.mamon)) card.remove(); });
      for (const dish of dishes) {
        let card = [...grid.querySelectorAll(".product-card")].find(x => x.dataset.mamon === String(dish.maMon));
        if (!card) { card = createCard(); grid.append(card); }
        updateCard(card, dish);
      }
      const categories = document.getElementById("categories");
      if (categories) {
        const selected = categories.querySelector(".selected")?.dataset.category ?? "Tất cả";
        categories.replaceChildren();
        for (const name of ["Tất cả", ...(snapshot.danhMuc ?? []).map(x => x.tenDanhMuc)]) {
          const button = document.createElement("button"); button.type = "button"; button.textContent = name; button.dataset.category = name;
          button.className = name === selected ? "selected" : ""; categories.append(button);
        }
        if (!categories.querySelector(".selected")) categories.firstElementChild?.classList.add("selected");
      }
      const banner = snapshot.banner?.[0];
      const heroTitle = document.querySelector(".hero h1"); const heroCopy = document.querySelector(".hero p");
      if (heroTitle) heroTitle.textContent = banner?.tenBanner ?? "Một chút cà phê, một ngày dễ chịu.";
      if (heroCopy) heroCopy.textContent = banner?.noiDung ?? "Cà phê ngon · Không gian ấm · Năng lượng tích cực";
      const heroLink = document.querySelector(".hero a");
      if (heroLink) heroLink.setAttribute("href", banner?.lienKet ?? "#menu");
      const heroPhoto = document.querySelector(".hero-cup");
      if (heroPhoto) {
        const imagePath = banner?.duongDanAnh;
        if (/^sprite:[0-9]+$/.test(imagePath ?? "")) { heroPhoto.replaceChildren(); heroPhoto.className = "hero-cup drink-photo sprite-" + imagePath.slice(7); }
        else if (imagePath?.startsWith("/") || imagePath?.startsWith("https://")) {
          const image = document.createElement("img"); image.src = imagePath; image.alt = banner.tenBanner;
          image.style.cssText = "width:100%;height:100%;object-fit:contain"; heroPhoto.className = "hero-cup"; heroPhoto.replaceChildren(image);
        } else { heroPhoto.replaceChildren(); heroPhoto.className = "hero-cup drink-photo sprite-2"; }
      }
      const count = document.querySelector(".section-heading .muted"); if (count) count.textContent = dishes.length + " món được chọn lọc";
      const pageCount = document.querySelector(".pagination span"); if (pageCount) pageCount.textContent = "Trang 1 / 1 · " + dishes.length + " món";
      status.hidden = true;
      window.dispatchEvent(new Event("fourchang:catalog-refresh"));
    } catch (error) { status.hidden = false; status.textContent = error.message; }
    finally { loading = false; }
  }
  // SSE chỉ báo có thay đổi, mọi mã/giá vẫn lấy từ snapshot; polling là đường dự phòng khi stream lỗi.
  const events = new EventSource("/catalog/events");
  events.onmessage = refresh;
  events.onerror = () => { status.hidden = false; status.textContent = "Đang nối lại cập nhật thực đơn; dữ liệu có thể cũ."; };
  const timer = setInterval(refresh, 10000);
  document.addEventListener("visibilitychange", () => { if (!document.hidden) refresh(); });
  window.addEventListener("pagehide", () => { events.close(); clearInterval(timer); }, { once: true });
  refresh();
})();
