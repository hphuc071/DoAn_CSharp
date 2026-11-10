"use strict";
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.join(__dirname, "..");
const script = fs.readFileSync(path.join(root, "src/Presentation/FourChang.Web/wwwroot/js/orders-auto-refresh.js"), "utf8");

function fixture(options = {}) {
  const timers = [];
  const events = {};
  const state = { navigations: [], reloads: 0, hidden: !!options.hidden, online: options.online !== false, focused: !!options.focused };
  const document = {
    querySelector(selector) {
      if (selector === "[data-order-auto-refresh]") return options.snapshot === false ? null : {};
      if (selector === "[data-order-refresh-target]")
        return options.link === false ? null : { href: options.href || "https://test.example/Orders?Status=READY" };
      return null;
    },
    addEventListener(name, fn) { events[name] = fn; },
    get hidden() { return state.hidden; },
    get activeElement() { return state.focused ? { closest: selector => selector === "form" ? {} : null } : null; }
  };
  const navigator = { get onLine() { return state.online; } };
  const window = {
    location: { href: "https://test.example/Orders", origin: "https://test.example",
      reload() { state.reloads++; }, replace(url) { state.navigations.push(url); } },
    setTimeout(fn, delay) { timers.push({ fn, delay }); }
  };
  vm.runInNewContext(script, { document, navigator, window, URL });
  return {
    events, state, timers,
    tick() { const timer = timers.shift(); assert.ok(timer, "expected pending timer"); assert.equal(timer.delay, 30000); timer.fn(); }
  };
}

test("UC08: no snapshot means no background polling", () => {
  const f = fixture({ snapshot: false });
  assert.equal(f.timers.length, 0);
});

test("UC08: visible online snapshot navigates to a safe GET URL without replaying POST", () => {
  const f = fixture();
  assert.equal(f.timers.length, 1);
  f.tick();
  assert.equal(f.state.reloads, 0); // Must not replay the current document if it came from POST.
  assert.deepEqual(f.state.navigations, ["https://test.example/Orders?Status=READY"]);
  assert.equal(f.timers.length, 0);
});

test("UC08: offline or hidden tab never forces a navigation", () => {
  const f = fixture({ online: false, hidden: true });
  f.tick();
  assert.equal(f.state.navigations.length, 0);
  f.state.hidden = false;
  f.tick();
  assert.equal(f.state.navigations.length, 0);
  f.state.online = true;
  f.tick();
  assert.equal(f.state.reloads, 0); // Must not replay the current document if it came from POST.
  assert.deepEqual(f.state.navigations, ["https://test.example/Orders?Status=READY"]);
});

test("UC08: editing a cancellation reason or list filter blocks periodic reload", () => {
  const f = fixture();
  f.events.input({ target: { closest: selector => selector === "form" ? {} : null } });
  f.tick();
  assert.equal(f.state.navigations.length, 0);
  f.tick();
  assert.equal(f.state.navigations.length, 0);
});

test("UC08: focusing a form blocks refresh even before typing", () => {
  const f = fixture({ focused: true });
  f.tick();
  assert.equal(f.state.navigations.length, 0);
  f.state.focused = false;
  f.tick();
  assert.equal(f.state.reloads, 0); // Must not replay the current document if it came from POST.
  assert.deepEqual(f.state.navigations, ["https://test.example/Orders?Status=READY"]);
});

test("UC08: refuses an external or malformed refresh target", () => {
  for (const href of ["https://evil.example/Orders", "javascript:alert(1)", "http://[bad"]) {
    const f = fixture({ href });
    f.tick();
    assert.equal(f.state.navigations.length, 0);
  }
});

test("UC08: missing read-only link disables polling, even with snapshot present", () => {
  const f = fixture({ link: false });
  assert.equal(f.timers.length, 0);
});

test("UC08: both Orders views opt in without inline bearer tokens or financial POST", () => {
  for (const page of ["Index.cshtml", "Details.cshtml"]) {
    const html = fs.readFileSync(path.join(root, "src/Presentation/FourChang.Web/Pages/Orders", page), "utf8");
    assert.match(html, /data-order-auto-refresh/);
    assert.match(html, /~\/js\/orders-auto-refresh\.js/);
  }
  assert.doesNotMatch(script, /fetch\(|XMLHttpRequest|localStorage|sessionStorage|\.post\(|Bearer|CancelCustomerOrderAsync|location\.reload\(/);
});
