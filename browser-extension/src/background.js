"use strict";

const api = globalThis.browser ?? globalThis.chrome;
const NATIVE_HOST = "com.hybridsolutionscloud.vaultprospector";
const PROTOCOL_VERSION = 1;
const REQUEST_LIFETIME_MS = 30_000;
const ALLOWED_RESPONSE_KEYS = Object.freeze([
  "mappingId",
  "protocolVersion",
  "requestId",
  "result",
  "transactionNonce",
  "valueUtf8",
]);

function browserFamily() {
  return api.runtime.getManifest().browser_specific_settings ? "firefox" : "chromium";
}

function canonicalHttpsOrigin(value) {
  let url;
  try {
    url = new URL(value);
  } catch {
    throw new Error("The active page does not have a supported origin.");
  }

  if (
    url.protocol !== "https:" ||
    url.username !== "" ||
    url.password !== "" ||
    url.pathname !== "/" ||
    url.search !== "" ||
    url.hash !== "" ||
    !url.hostname.includes(".") ||
    url.hostname.endsWith(".")
  ) {
    throw new Error("The active page does not have a supported HTTPS origin.");
  }

  return url.origin;
}

function randomToken() {
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  let binary = "";
  for (const value of bytes) {
    binary += String.fromCharCode(value);
  }
  bytes.fill(0);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
}

function fieldPurposeFor(type, autocomplete) {
  const normalizedType = String(type).toLowerCase();
  const normalizedAutocomplete = String(autocomplete).toLowerCase();
  if (normalizedAutocomplete === "new-password") {
    return null;
  }
  if (
    normalizedType === "password" ||
    normalizedAutocomplete === "current-password"
  ) {
    return "password";
  }
  if (normalizedAutocomplete === "one-time-code") {
    return "oneTimeCode";
  }
  if (
    ["text", "email"].includes(normalizedType) &&
    ["username", "email"].includes(normalizedAutocomplete)
  ) {
    return "username";
  }
  return null;
}

function createRequest(tab, frame, family, now = new Date()) {
  if (!Number.isSafeInteger(tab.id) || tab.id < 0) {
    throw new Error("The active tab is unavailable.");
  }
  if (!Number.isSafeInteger(frame.frameId) || frame.frameId < 0) {
    throw new Error("The active frame is unavailable.");
  }

  return {
    protocolVersion: PROTOCOL_VERSION,
    operation: "requestFill",
    requestId: crypto.randomUUID(),
    browserFamily: family,
    tabId: tab.id,
    frameId: frame.frameId,
    documentId: frame.result.documentId,
    gestureNonce: randomToken(),
    createdAtUtc: now.toISOString(),
    topOrigin: canonicalHttpsOrigin(new URL(tab.url).origin),
    frameOrigin: canonicalHttpsOrigin(frame.result.frameOrigin),
    fieldPurpose: frame.result.fieldPurpose,
    fieldToken: frame.result.fieldToken,
  };
}

function validateNativeResponse(response, requestId) {
  if (response === null || typeof response !== "object" || Array.isArray(response)) {
    throw new Error("The native host returned an invalid response.");
  }
  const keys = Object.keys(response).sort();
  if (
    keys.length !== ALLOWED_RESPONSE_KEYS.length ||
    keys.some((key, index) => key !== ALLOWED_RESPONSE_KEYS[index])
  ) {
    throw new Error("The native host returned an unexpected response shape.");
  }
  if (response.protocolVersion !== PROTOCOL_VERSION || response.requestId !== requestId) {
    throw new Error("The native host response does not match this request.");
  }
  if (response.result !== "approved") {
    if (
      response.transactionNonce !== null ||
      response.mappingId !== null ||
      response.valueUtf8 !== null
    ) {
      throw new Error("A denied native response contained sensitive fields.");
    }
    return Object.freeze({ approved: false });
  }
  if (
    typeof response.transactionNonce !== "string" ||
    !/^[A-Za-z0-9+/]{43}=$/.test(response.transactionNonce) ||
    typeof response.mappingId !== "string" ||
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      response.mappingId,
    ) ||
    typeof response.valueUtf8 !== "string" ||
    response.valueUtf8.length === 0 ||
    response.valueUtf8.length > 43_692
  ) {
    throw new Error("The approved native response is incomplete.");
  }

  return {
    approved: true,
    transactionNonce: response.transactionNonce,
    mappingId: response.mappingId,
    valueUtf8: response.valueUtf8,
  };
}

function decodeValue(encoded) {
  const binary = atob(encoded);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  try {
    return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  } finally {
    bytes.fill(0);
  }
}

function inspectFocusedField() {
  const stateKey = "__vaultProspectorOneShotFillState";
  const now = Date.now();
  let state = globalThis[stateKey];
  if (!state || typeof state !== "object") {
    state = {
      documentId: crypto.randomUUID(),
      fields: new Map(),
    };
    Object.defineProperty(globalThis, stateKey, {
      value: state,
      configurable: false,
      enumerable: false,
      writable: false,
    });
  }

  for (const [token, entry] of state.fields) {
    if (entry.expiresAt < now) {
      state.fields.delete(token);
    }
  }

  const element = document.activeElement;
  if (!(element instanceof HTMLInputElement) || !element.isConnected) {
    return null;
  }
  const style = getComputedStyle(element);
  const bounds = element.getBoundingClientRect();
  if (
    element.disabled ||
    element.readOnly ||
    element.type === "hidden" ||
    style.display === "none" ||
    style.visibility === "hidden" ||
    bounds.width < 1 ||
    bounds.height < 1
  ) {
    return null;
  }

  const fieldPurpose = fieldPurposeFor(element.type, element.autocomplete);
  if (fieldPurpose === null) {
    return null;
  }

  if (location.protocol !== "https:" || location.origin === "null") {
    return null;
  }
  const fieldToken = crypto.randomUUID();
  state.fields.set(fieldToken, {
    element,
    fieldPurpose,
    frameOrigin: location.origin,
    expiresAt: now + 30_000,
  });
  return {
    documentId: state.documentId,
    fieldToken,
    fieldPurpose,
    frameOrigin: location.origin,
  };
}

function fillApprovedField(fieldToken, documentId, fieldPurpose, frameOrigin, value) {
  const state = globalThis.__vaultProspectorOneShotFillState;
  const entry = state?.fields?.get(fieldToken);
  state?.fields?.delete(fieldToken);
  if (
    !entry ||
    state.documentId !== documentId ||
    entry.expiresAt < Date.now() ||
    entry.fieldPurpose !== fieldPurpose ||
    entry.frameOrigin !== frameOrigin ||
    location.origin !== frameOrigin
  ) {
    return false;
  }

  const element = entry.element;
  const style = getComputedStyle(element);
  const bounds = element.getBoundingClientRect();
  if (
    !(element instanceof HTMLInputElement) ||
    !element.isConnected ||
    element.disabled ||
    element.readOnly ||
    element.type === "hidden" ||
    style.display === "none" ||
    style.visibility === "hidden" ||
    bounds.width < 1 ||
    bounds.height < 1
  ) {
    return false;
  }

  const currentPurpose = fieldPurposeFor(element.type, element.autocomplete);
  if (currentPurpose !== fieldPurpose) {
    return false;
  }

  const setter = Object.getOwnPropertyDescriptor(
    HTMLInputElement.prototype,
    "value",
  )?.set;
  if (typeof setter !== "function") {
    return false;
  }
  setter.call(element, value);
  element.dispatchEvent(new InputEvent("input", {
    bubbles: true,
    composed: true,
    inputType: "insertReplacementText",
  }));
  element.dispatchEvent(new Event("change", { bubbles: true }));
  return true;
}

async function setActionState(tabId, title, badgeText) {
  await api.action.setTitle({ tabId, title });
  await api.action.setBadgeText({ tabId, text: badgeText });
  if (badgeText !== "") {
    setTimeout(() => {
      void api.action.setBadgeText({ tabId, text: "" });
      void api.action.setTitle({ tabId, title: "Fill with Vault Prospector" });
    }, 4_000);
  }
}

async function handleAction(tab) {
  try {
    const topOrigin = canonicalHttpsOrigin(new URL(tab.url).origin);
    const results = await api.scripting.executeScript({
      target: { tabId: tab.id, allFrames: true },
      func: inspectFocusedField,
    });
    const eligible = results.filter((entry) => entry.result !== null);
    if (eligible.length !== 1) {
      throw new Error("Focus one supported sign-in field and try again.");
    }

    const frame = eligible[0];
    frame.result.frameOrigin = canonicalHttpsOrigin(frame.result.frameOrigin);
    const request = createRequest(
      { ...tab, url: topOrigin },
      frame,
      browserFamily(),
    );
    const response = validateNativeResponse(
      await api.runtime.sendNativeMessage(NATIVE_HOST, request),
      request.requestId,
    );
    if (!response.approved) {
      await setActionState(tab.id, "Vault Prospector did not fill this field", "No");
      return;
    }

    if (Date.now() - Date.parse(request.createdAtUtc) > REQUEST_LIFETIME_MS) {
      throw new Error("The fill request expired.");
    }
    let value = decodeValue(response.valueUtf8);
    try {
      const currentTab = await api.tabs.get(tab.id);
      if (
        canonicalHttpsOrigin(new URL(currentTab.url).origin) !==
        request.topOrigin
      ) {
        throw new Error("The top-level page changed before the approved fill.");
      }
      const injection = await api.scripting.executeScript({
        target: { tabId: tab.id, frameIds: [frame.frameId] },
        func: fillApprovedField,
        args: [
          request.fieldToken,
          request.documentId,
          request.fieldPurpose,
          request.frameOrigin,
          value,
        ],
      });
      if (injection.length !== 1 || injection[0].result !== true) {
        throw new Error("The page changed before the approved value could be filled.");
      }
      await setActionState(tab.id, "Vault Prospector filled the approved field", "OK");
    } finally {
      value = "";
      response.valueUtf8 = "";
    }
  } catch {
    if (Number.isSafeInteger(tab.id)) {
      await setActionState(tab.id, "Vault Prospector could not fill this field", "!");
    }
  }
}

if (api?.action?.onClicked) {
  api.action.onClicked.addListener((tab) => {
    void handleAction(tab);
  });
}

globalThis.VaultProspectorBrowserTest = Object.freeze({
  canonicalHttpsOrigin,
  createRequest,
  fieldPurposeFor,
  validateNativeResponse,
});
