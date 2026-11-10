/** UC05/UC06: quote must never approve a local draft the cart renderer rejects.
 * Offline VM tests. No HTTP/SQL/payment calls are performed.
 * Run: node --test tools/verify-uc05-08-quote-snapshot.cjs
 */
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const code = fs.readFileSync(path.join(__dirname,
  '../src/Presentation/FourChang.Web/wwwroot/js/checkout-quote.js'), 'utf8');

function setup(changes = {}) {
  const original = {
    productId: 1, variantId: 2, quantity: 2, listedPrice: 25000,
    name: 'Trà', sugar: '100%', ice: '100%', note: '', draftVersion: 2, toppings: []
  };
  const raw = JSON.stringify([{...original, ...changes}]);
  const listeners = {};
  const calls = [];
  const elements = new Map();
  const element = id => {
    if (!elements.has(id)) elements.set(id, {
      textContent: '', parentElement: {querySelector: () => ({textContent: ''})}
    });
    return elements.get(id);
  };
  const tabs = {
    querySelector: () => ({dataset: {tab:'takeaway'}}),
    addEventListener: () => {}
  };
  const document = {
    getElementById: element,
    querySelector(selector) {
      if (selector === '[data-tabs]') return tabs;
      if (selector.includes('checkout-quote-antiforgery')) return {value:'test-antiforgery-token'};
      return null;
    }
  };
  const window = {
    fourchangCheckoutQuote: null,
    addEventListener(type, callback) {listeners[type] = callback;},
    dispatchEvent() {}
  };
  async function fetch(url) {
    calls.push(url);
    if (url.includes('handler=Variants')) return {ok: true, json: async () => [
      {maCauHinhMon:2,available:true,tuyChonPhaChe:[{phanTramDuong:100,phanTramDa:100}],toppings:[]}
    ]};
    if (url.includes('handler=Quote')) return {ok:true,json:async () => ({tienHang:50000,phiGiaoHang:0,tongCong:50000})};
    throw Error('Unexpected URL ' + url);
  }
  vm.runInNewContext(code, {document, window, localStorage:{getItem:()=>raw},
    fetch, AbortController, Event, queueMicrotask, Number, JSON, String, Intl, Map, Set,
    console, Error, Array, encodeURIComponent}, {filename:'checkout-quote.js'});
  return {calls, element, window, refresh: () => listeners.pageshow()};
}

test('UC05-06: invalid productId string never triggers a server quote', async () => {
  const env = setup({productId:'1'});
  await env.refresh();
  assert.equal(env.calls.length, 0);
  assert.equal(env.window.fourchangCheckoutQuote, null);
  assert.match(env.element('checkout-price-warning').textContent, /không hợp lệ/);
});

for (const [label, changes] of [
  ['variantId string', {variantId:'2'}],
  ['quantity string', {quantity:'2'}],
  ['listedPrice string', {listedPrice:'25000'}],
  ['topping price string', {toppings:[{id:9,portions:1,price:'5000'}]}]
]) {
  test(`UC05-06: ${label} cannot reach POST Quote`, async () => {
    const env = setup(changes);
    await env.refresh();
    assert.ok(!env.calls.some(url => url.includes('handler=Quote')));
    assert.equal(env.window.fourchangCheckoutQuote, null);
  });
}

test('UC05-06: valid numeric snapshot still gets authoritative quote', async () => {
  const env = setup();
  await env.refresh();
  assert.ok(env.calls.some(url => url.includes('handler=Quote')));
  assert.equal(env.window.fourchangCheckoutQuote.price.tongCong, 50000);
  assert.equal(env.window.fourchangCheckoutQuote.payload.items[0].soLuong, 2);
});
