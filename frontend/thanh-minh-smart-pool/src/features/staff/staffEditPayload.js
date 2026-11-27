export const detailFields = [
  "fullName", "phone", "address", "dateOfBirth", "cccdNumber",
  "permanentAddress", "temporaryAddress", "bankName", "bankAccountNumber",
  "taxIdentifierKind", "taxIdentifier", "gender", "personalEmail",
];

export function comparableValue(name, value) {
  if (name === "dateOfBirth") return value?.format?.("YYYY-MM-DD") || null;
  return typeof value === "string" ? value.trim() : (value ?? null);
}

export function buildProfilePayload(values, initial, snapshot) {
  const details = {};
  for (const name of detailFields) {
    const next = comparableValue(name, values[name]);
    if (next === comparableValue(name, initial[name])) continue;
    details[name] = name === "address" && next === "" ? null : next;
  }
  if (!Object.keys(details).length) return null;
  return {
    details,
    expectedUpdatedAt: snapshot.expectedUpdatedAt,
    expectedUserUpdatedAt: snapshot.expectedUserUpdatedAt,
    expectedProfileUpdatedAt: snapshot.expectedProfileUpdatedAt,
    expectedProfileExists: snapshot.expectedProfileExists,
    expectedLegalProfileVersion: snapshot.expectedLegalProfileVersion,
  };
}
