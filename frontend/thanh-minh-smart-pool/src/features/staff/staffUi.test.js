import assert from "node:assert/strict";
import test from "node:test";
import {
  isEmployeeBirthDateEligible,
  latestEmployeeBirthDate,
} from "./staffUi.js";

const today = "2026-11-26";

test("employee birth-date cutoff is today minus 18 calendar years", () => {
  assert.equal(latestEmployeeBirthDate(today).format("YYYY-MM-DD"), "2008-11-26");
});

test("employee age validation accepts the 18th birthday and older dates", () => {
  assert.equal(isEmployeeBirthDateEligible("2008-11-26", today), true);
  assert.equal(isEmployeeBirthDateEligible("1990-01-01", today), true);
});

test("employee age validation rejects birthdays after the 18th birthday", () => {
  assert.equal(isEmployeeBirthDateEligible("2008-11-27", today), false);
  assert.equal(isEmployeeBirthDateEligible("2026-11-27", today), false);
});
