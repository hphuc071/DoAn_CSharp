"use strict";
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const root = path.join(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const checkout = read("src/Presentation/FourChang.Web/wwwroot/js/checkout-order.js");
const page = read("src/Presentation/FourChang.Web/Pages/Checkout/Index.cshtml");
const api = read("src/Backend/FourChang.Api/Features/Customer/Orders/CustomerOrdersEndpoints.cs");
const dto = read("src/Shared/FourChang.Presentation.Shared/Contracts/Customer/MenuContracts.cs");
const policy = read("src/Backend/FourChang.Api/Features/Customer/Orders/TakeawayPickupSchedule.cs");
test("Schedule options require an explicit Web rollout; asap remains selectable", () => {
  assert.match(page, /value="asap"/);
  assert.match(page, /Model.TakeawaySchedulingEnabled/);
  assert.match(page, /type="datetime-local"/);
});
test("Legacy DTO hash excludes absent pickup, but an explicit pickup is bound to the request", () => {
  assert.match(dto, /JsonIgnoreCondition.WhenWritingNull/);
  assert.match(dto, /DateTimeOffset\? ThoiDiemHenNhan = null/);
  assert.match(checkout, /thoiDiemHenNhan: scheduledUtc/);
});
test("Backend persists the scheduled UTC snapshot only when the approved policy accepts it", () => {
  assert.match(api, /TakeawayPickupSchedule.Validate/);
  assert.match(api, /ThoiDiemHenNhan = req.ThoiDiemHenNhan\?\.UtcDateTime/);
  assert.match(policy, /Orders:TakeawaySchedulingPolicyApproved/);
  assert.match(policy, /utc <= paymentDeadlineUtc/);
  assert.match(policy, /TimeZoneInfo.ConvertTime/);
});
test("No new payment, bank, expiry or automatic inventory write path was introduced", () => {
  assert.doesNotMatch(checkout, /fetch\(["']\/api\//);
  assert.doesNotMatch(policy, /SaveChanges|ExecuteSql|GiaoDichNganHang/);
});

function runPickup(mode, value, enabled) {
  const begin = checkout.indexOf("  function pickupUtc() {");
  const end = checkout.indexOf("  const status =", begin);
  assert.ok(begin >= 0 && end > begin);
  const code = `(() => { const when = { value: ${JSON.stringify(mode)} };
    const pickup = { value: ${JSON.stringify(value)} };
    const scheduleEnabled = ${enabled};
    ${checkout.slice(begin, end)}
    return pickupUtc(); })()`;
  return vm.runInNewContext(code);
}
test("Actual checkout schedule parser accepts a local time and converts it explicitly to UTC", () => {
  const time = "2026-11-11T15:30";
  assert.equal(runPickup("asap", "", false), null);
  assert.equal(runPickup("scheduled", time, true), new Date(time).toISOString());
});
test("Actual checkout schedule parser fails closed on disabled, missing and invalid times", () => {
  assert.throws(() => runPickup("scheduled", "2026-11-11T15:30", false), /chưa được mở/);
  assert.throws(() => runPickup("scheduled", "", true), /Chọn ngày và giờ/);
  assert.throws(() => runPickup("scheduled", "2026-02-31T12:30", true), /không tồn tại/);
  assert.throws(() => runPickup("unexpected", "2026-11-11T15:30", true), /chưa được mở/);
});
