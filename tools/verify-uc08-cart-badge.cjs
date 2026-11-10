"use strict";
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const source = fs.readFileSync(path.join(__dirname, "..", "src", "Presentation", "FourChang.Web", "wwwroot", "js", "cart-badge.js"), "utf8");
function setup(value) {
  const handlers = {};
  const badge = {
    textContent: "—", title: "",
    removeAttribute(name) { if (name === "title") this.title = ""; }
  };
  const state = { value };
  const globals = {
    document: { getElementById(id) { return id === "cart-badge" ? badge : null; } },
    localStorage: { getItem() { return state.value; } },
    window: { addEventListener(name, handler) { handlers[name] = handler; } }
  };
  vm.runInNewContext(source, globals);
  return { badge, state, handlers };
}
test("Header badge counts saved cart quantity, not a fixed 2", () => {
  const s = setup(JSON.stringify([{ quantity: 2 }, { quantity: 3 }]));
  assert.equal(s.badge.textContent, "5");
  s.state.value = null;
  s.handlers["cart:changed"]();
  assert.equal(s.badge.textContent, "0");
});
test("Invalid cart does not display a misleading count", () => {
  assert.equal(setup("{broken").badge.textContent, "!");
  assert.equal(setup(JSON.stringify([{ quantity: "2" }])).badge.textContent, "!");
  assert.equal(setup(JSON.stringify([{ quantity: null }])).badge.textContent, "!");
});
test("Storage changes and back-forward restores refresh badge", () => {
  const s = setup("[]");
  s.state.value = JSON.stringify([{quantity: 1}]);
  s.handlers.storage({key: "fourchang.cart-draft.v1"});
  assert.equal(s.badge.textContent, "1");
  s.state.value = "[]";
  s.handlers.pageshow({});
  assert.equal(s.badge.textContent, "0");
});
