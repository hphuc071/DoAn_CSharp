/* UC-WEB-06/07: a read-only bridge to the synthetic QR preview.
 * Never creates an order, payment intent, GUID, or proof of a bank transfer.
 * The destination re-quotes through the existing antiforgery-protected Checkout handler.
 */
(() => {
  "use strict";
  const LINK = document.getElementById("checkout-demo-preview");
  if (!LINK) return; // Development-only link is server gated.
  const CART = "fourchang.cart-draft.v1";
  const PREVIEW = "fourchang.demo-qr-cart.v1";

  LINK.addEventListener("click", () => {
    try {
      const quote = window.fourchangCheckoutQuote;
      const cartRaw = localStorage.getItem(CART) || "[]";
      if (!quote || quote.cartRaw !== cartRaw ||
          quote.payload?.hinhThucNhanMon !== "TAKEAWAY" ||
          !Array.isArray(quote.payload.items) ||
          quote.payload.items.length < 1 || quote.payload.items.length > 50) {
        sessionStorage.removeItem(PREVIEW);
        return;
      }
      // Store only a price request for this tab, never personal receiver details.
      // This is client input: the preview MUST calculate its own server quote.
      sessionStorage.setItem(PREVIEW, JSON.stringify({
        version: 1,
        cartRaw,
        payload: quote.payload
      }));
    } catch {
      // Storage errors fail closed: the destination displays no price.
      try { sessionStorage.removeItem(PREVIEW); } catch { /* no storage access */ }
    }
  });
})();
