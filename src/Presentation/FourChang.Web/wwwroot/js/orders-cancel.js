// UC-WEB-08: Nút hủy chỉ hiển thị khi server trả trạng thái chờ xác nhận.
// Quyền hủy vẫn phải được kiểm tra lại bởi Backend, không tin dữ liệu trình duyệt.
(() => {
  "use strict";
  const list = document.getElementById("orders-list");
  const status = document.getElementById("orders-refresh-status");
  const csrf = document.querySelector(
    '#orders-cancel-antiforgery input[name="__RequestVerificationToken"]'
  )?.value;
  if (!list) return;

  function decorate() {
    list.querySelectorAll("article[data-order-id]").forEach(article => {
      const id = Number(article.dataset.orderId);
      if (article.dataset.orderStatus !== "PENDING_CONFIRMATION" ||
          !Number.isSafeInteger(id) || id <= 0 ||
          article.querySelector('[data-action="cancel-order"]')) return;
      const button = document.createElement("button");
      button.type = "button";
      button.className = "button secondary";
      button.dataset.action = "cancel-order";
      button.textContent = "Hủy đơn";
      article.querySelector(".item-content")?.append(button);
    });
  }

  const observer = new MutationObserver(decorate);
  observer.observe(list, { childList: true, subtree: false });
  decorate();

  let submitting = false;
  list.addEventListener("click", async event => {
    const button = event.target.closest('[data-action="cancel-order"]');
    if (!button || submitting) return;
    const article = button.closest("article[data-order-id]");
    const orderId = Number(article?.dataset.orderId);
    if (!Number.isSafeInteger(orderId) || orderId <= 0) return;

    const reasonInput = prompt("Nhập lý do hủy đơn (3–400 ký tự):");
    if (reasonInput === null) return;
    const reason = reasonInput.trim();
    if (reason.length < 3 || reason.length > 400) {
      alert("Lý do phải có từ 3 đến 400 ký tự.");
      return;
    }
    if (!confirm("Xác nhận hủy đơn này? Thao tác không thể tự hoàn tác.")) return;
    if (!csrf) {
      alert("Thiếu mã xác thực yêu cầu. Vui lòng tải lại trang.");
      return;
    }

    submitting = true;
    button.disabled = true;
    try {
      const response = await fetch("/Orders?handler=Cancel", {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        headers: {
          "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
          "RequestVerificationToken": csrf,
          "Accept": "application/json"
        },
        body: new URLSearchParams({ orderId: String(orderId), reason })
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (response.status === 401) {
          throw new Error("Phiên đăng nhập đã hết hạn.");
        }
        throw new Error(data.message || "Không thể hủy đơn. Hãy tải lại trạng thái.");
      }
      if (status) status.textContent = data.thongBao || "Đã hủy đơn.";
      // Tải lại từ Backend để tránh hiển thị trạng thái cũ.
      location.reload();
    } catch (error) {
      if (status) status.textContent = error.message;
      alert(error.message);
      button.disabled = false;
    } finally {
      submitting = false;
    }
  });

  window.addEventListener("pagehide", () => observer.disconnect(), { once: true });
})();
