import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { test } from "node:test";
import vm from "node:vm";

async function loadHelpers() {
  const source = await readFile(
    resolve(import.meta.dirname, "..", "src", "background.js"),
    "utf8",
  );
  const context = {
    URL,
    Date,
    TextDecoder,
    Uint8Array,
    btoa: (value) => Buffer.from(value, "binary").toString("base64"),
    atob: (value) => Buffer.from(value, "base64").toString("binary"),
    crypto: globalThis.crypto,
    setTimeout: () => 0,
    chrome: {
      runtime: {
        getManifest: () => ({ manifest_version: 3 }),
      },
      action: {
        onClicked: { addListener: () => undefined },
      },
    },
  };
  vm.createContext(context);
  vm.runInContext(source, context, { filename: "background.js" });
  return context.VaultProspectorBrowserTest;
}

test("canonical origin accepts exact HTTPS origins", async () => {
  const helpers = await loadHelpers();
  assert.equal(
    helpers.canonicalHttpsOrigin("https://login.example.com"),
    "https://login.example.com",
  );
  assert.equal(
    helpers.canonicalHttpsOrigin("https://login.example.com:8443"),
    "https://login.example.com:8443",
  );
});

test("canonical origin rejects unsafe forms", async () => {
  const helpers = await loadHelpers();
  for (const origin of [
    "http://login.example.com",
    "https://user@login.example.com",
    "https://login.example.com/path",
    "https://login.example.com.",
    "https://localhost",
  ]) {
    assert.throws(() => helpers.canonicalHttpsOrigin(origin));
  }
});

test("request binds browser-derived context", async () => {
  const helpers = await loadHelpers();
  const request = helpers.createRequest(
    { id: 42, url: "https://login.example.com" },
    {
      frameId: 7,
      result: {
        documentId: "document-token",
        fieldToken: "field-token",
        fieldPurpose: "password",
        frameOrigin: "https://accounts.example.com",
      },
    },
    "chromium",
    new Date("2026-07-23T16:00:00.000Z"),
  );

  assert.equal(request.tabId, 42);
  assert.equal(request.frameId, 7);
  assert.equal(request.topOrigin, "https://login.example.com");
  assert.equal(request.frameOrigin, "https://accounts.example.com");
  assert.equal(request.fieldPurpose, "password");
});

test("field purpose excludes password-creation and unlabelled username fields", async () => {
  const helpers = await loadHelpers();

  assert.equal(helpers.fieldPurposeFor("password", "current-password"), "password");
  assert.equal(helpers.fieldPurposeFor("password", "new-password"), null);
  assert.equal(helpers.fieldPurposeFor("email", "username"), "username");
  assert.equal(helpers.fieldPurposeFor("text", ""), null);
  assert.equal(helpers.fieldPurposeFor("text", "one-time-code"), "oneTimeCode");
});

test("native response rejects extra fields and mismatched requests", async () => {
  const helpers = await loadHelpers();
  const response = {
    protocolVersion: 1,
    requestId: "request-id",
    result: "denied",
    transactionNonce: null,
    mappingId: null,
    valueUtf8: null,
  };

  assert.deepEqual(
    { ...helpers.validateNativeResponse(response, "request-id") },
    { approved: false },
  );
  assert.throws(() =>
    helpers.validateNativeResponse({ ...response, extra: true }, "request-id"));
  assert.throws(() =>
    helpers.validateNativeResponse(response, "different-request"));
  assert.throws(() =>
    helpers.validateNativeResponse(
      { ...response, valueUtf8: "c2hvdWxkLW5vdC1hcHBlYXI=" },
      "request-id",
    ));
});

test("approved response requires complete bounded fields", async () => {
  const helpers = await loadHelpers();
  const response = {
    protocolVersion: 1,
    requestId: "request-id",
    result: "approved",
    transactionNonce: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
    mappingId: "5b75f934-2867-44eb-b53b-d909f9068353",
    valueUtf8: "c3ludGhldGlj",
  };

  assert.equal(
    helpers.validateNativeResponse(response, "request-id").approved,
    true,
  );
  assert.throws(() =>
    helpers.validateNativeResponse({ ...response, valueUtf8: "" }, "request-id"));
});
