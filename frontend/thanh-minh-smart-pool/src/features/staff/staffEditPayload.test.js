import test from "node:test";
import assert from "node:assert/strict";
import { buildProfilePayload } from "./staffEditPayload.js";

test("profile payload includes only changed fields and the complete snapshot", () => {
  const snapshot = { expectedUpdatedAt: "employee", expectedUserUpdatedAt: "user", expectedProfileUpdatedAt: null, expectedProfileExists: false, expectedLegalProfileVersion: 3 };
  const initial = { gender: "MALE", personalEmail: "old@example.test", address: "Old" };
  const result = buildProfilePayload({ ...initial, gender: "FEMALE", address: "", status: "Inactive" }, initial, snapshot);
  assert.deepEqual(result, { ...snapshot, details: { gender: "FEMALE", address: null } });
  assert.equal(buildProfilePayload(initial, initial, snapshot), null);
});
