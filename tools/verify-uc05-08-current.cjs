/** Regression checks for the current user's UC05-UC08 Checkout snapshot.
 * Run: node --test tools/verify-uc05-08-current.cjs
 * UI-only simulations; .NET/SQL/payment-provider tests must run separately.
 */
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const code = fs.readFileSync(path.join(__dirname, '../src/Presentation/FourChang.Web/wwwroot/js/checkout-cart.js'), 'utf8');

function renderCheckout(getItem) {
  const nodes = new Map();
  const node = id => {
    if (!nodes.has(id)) nodes.set(id, { textContent: '', children: [], replaceChildren(...children){ this.children = children; }, append(...children){ this.children.push(...children); } });
    return nodes.get(id);
  };
  const tabs = { querySelector: () => ({ dataset: { tab: 'takeaway' } }), addEventListener: () => {} };
  const document = {
    getElementById: node,
    querySelector: selector => selector === '[data-tabs]' ? tabs : null,
    createElement: tag => ({ tag, textContent: '', children: [], append(...children) { this.children.push(...children); } })
  };
  const window = { addEventListener() {} };
  vm.runInNewContext(code, { window, document, localStorage: { getItem }, queueMicrotask, Intl, Number, JSON, console }, { filename: 'checkout-cart.js' });
  return { node };
}

test('UC05-06: JSON hỏng phải báo lỗi, không hiển thị giỏ trống/0đ', () => {
  const { node } = renderCheckout(() => '{invalid JSON');
  assert.match(node('checkout-cart-count').textContent, /Không đọc được giỏ/);
  assert.equal(node('checkout-base-subtotal').textContent, 'Không xác định');
  assert.equal(node('checkout-total').textContent, 'Chưa thể đặt đơn');
  assert.match(node('checkout-price-warning').textContent, /Chưa xóa hay ghi đè/);
});

test('UC05-06: dữ liệu khác mảng hoặc có dòng null không bị bỏ âm thầm', () => {
  for (const raw of ['{}', '[null]', '[{"quantity":1},null]', '""']) {
    const { node } = renderCheckout(() => raw);
    assert.equal(node('checkout-base-subtotal').textContent, 'Không xác định', `raw=${raw}`);
    assert.equal(node('checkout-items').children[0].children[1].textContent, 'Không xác định', `raw=${raw}`);
  }
});

test('UC05-06: không nhận giá/số lượng/mã biến thể là chuỗi', () => {
  const baseline = { variantId: 12, quantity: 2, listedPrice: 25000, name:'Trà', size:'M', draftVersion:2, toppings:[] };
  for (const variant of [ { listedPrice:'25000' }, { quantity:'2' }, { variantId:'12' } ]) {
    const { node } = renderCheckout(() => JSON.stringify([{...baseline, ...variant}]));
    assert.equal(node('checkout-base-subtotal').textContent, 'Chưa xác định');
    assert.match(node('checkout-price-warning').textContent, /không hợp lệ/);
  }
});

test('UC05-06: giỏ hợp lệ vẫn hiện đúng tạm tính gồm topping', () => {
  const cart = [{ variantId:12, quantity:2, listedPrice:25000, name:'Trà', size:'M', draftVersion:2,
    toppings:[{id:8,name:'Trân châu',portions:2,price:5000}] }];
  const { node } = renderCheckout(() => JSON.stringify(cart));
  assert.equal(node('checkout-cart-count').textContent, '2 ly · 1 dòng giỏ nháp');
  assert.equal(node('checkout-base-subtotal').textContent, '70.000đ');
  assert.equal(node('checkout-total').textContent, 'Chờ Backend xác nhận');
});
