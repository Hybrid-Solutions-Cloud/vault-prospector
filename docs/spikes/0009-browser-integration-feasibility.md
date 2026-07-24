# Research Spike: Browser integration and password-vault interoperability

- **Status:** Completed for implementation feasibility; live/store validation remains
- **Owner:** Vault Prospector maintainers
- **Created:** 2026-07-23

## Objective

Determine whether Chromium-family and Firefox extensions can support a least-privilege,
user-initiated Vault Prospector fill without reading browser credential databases or exposing a
secret inventory.

## Primary-source findings

| Question | Chromium / Edge | Firefox | Decision |
| --- | --- | --- | --- |
| Can a user gesture grant temporary page access? | Chromium `activeTab` grants access after extension invocation and revokes it on cross-origin navigation. | Firefox supports the compatible WebExtensions model; browser sender data includes tab, URL, origin, and frame ID. | Use a toolbar/context/shortcut gesture and no persistent all-site access. |
| Can an extension reach a native desktop process? | Native messaging launches a registered standard-I/O host. The host manifest has exact `allowed_origins`; wildcards are forbidden. | Native messaging uses the same standard-I/O model with exact `allowed_extensions` and a Firefox-specific manifest registration. | Ship one strict protocol and separate browser host manifests. |
| Can content scripts call the host? | Native messaging is available to extension pages/service workers, not content scripts. | The compatible API is used from an extension context. | Background code derives browser context and owns the host call. |
| What page identity is available? | `activeTab`, tab data, and scripting targets identify the active tab and selected frame; navigation revokes access. | `runtime.MessageSender` exposes browser-populated `tab`, `url`, `origin`, and `frameId`; opaque origins are possible. | Bind tab, frame, document nonce, top origin, and frame origin, and fail on missing/opaque context. |
| Is the native host installed by the extension? | No. The Windows installer registers the host manifest; Edge also exposes administrator native-host and extension policies. | No. The application installer registers the host manifest in Firefox's Windows location. | MSI owns installation, repair, disablement, and removal. |
| How are extensions trusted? | Store/enterprise signing, review, update ownership, exact extension ID, and policy are operational controls. | Release/Beta Firefox requires Mozilla signing, including self-distributed extensions. | Developer-mode packages are test artifacts only, never supported distribution. |
| Can the extension read or synchronize the browser password vault? | The public privacy API controls whether password saving is enabled; it does not expose saved credentials. | The documented WebExtensions surface does not provide a general saved-login import/sync API. | Do not access browser profile databases. Treat import/export/sync as unsupported. |

## Sources

- [Chrome native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging)
- [Chrome `activeTab`](https://developer.chrome.com/docs/extensions/develop/concepts/activeTab)
- [Chrome scripting API](https://developer.chrome.com/docs/extensions/reference/api/scripting)
- [Chrome privacy API](https://developer.chrome.com/docs/extensions/reference/api/privacy)
- [Microsoft Edge native messaging](https://learn.microsoft.com/microsoft-edge/extensions-chromium/developer-guide/native-messaging)
- [Microsoft Edge extension installation policy](https://learn.microsoft.com/deployedge/microsoft-edge-policies/extensioninstallforcelist)
- [Mozilla native messaging](https://developer.mozilla.org/docs/Mozilla/Add-ons/WebExtensions/Native_messaging)
- [Mozilla `runtime.MessageSender`](https://developer.mozilla.org/docs/Mozilla/Add-ons/WebExtensions/API/runtime/MessageSender)
- [Firefox signing and distribution](https://extensionworkshop.com/documentation/publish/signing-and-distribution-overview/)

## Feasibility result

A constrained one-shot fill is feasible through documented APIs. The browser can provide
short-lived page access after a user gesture, and the extension can use a registered native host to
reach the desktop boundary. That does not make the browser request intrinsically trustworthy:
origin/frame/navigation checks, exact stored mappings, visible desktop confirmation, policy, fresh
Windows verification, strict framing, and extension/update governance remain mandatory.

Browser password-vault import, export, and synchronization are not feasible through a supported,
portable public extension API. Private profile/database access is prohibited. Vault Prospector
should coexist with the browser's password manager and perform only its own explicit one-shot fill.

## Implementation recommendation

1. Define and fuzz a bounded protocol before connecting it to the desktop or a value provider.
2. Implement a browser-launched host that cannot read repository keys or values.
3. Add a current-user-only authenticated desktop channel and an explicit confirmation workflow.
4. Use shared extension logic with separate Chromium and Firefox manifests.
5. Add exact origin/item/field mapping UI and keep integration disabled by default.
6. Complete signed-package, installed-browser, compromise-response, accessibility, and independent
   security evidence before distribution.

## Decision impact

[ADR-0014](../adr/0014-user-initiated-origin-bound-browser-fill.md) records the proposed production
boundary. The [browser threat model](../security/browser-integration-threat-model.md) is the
implementation and review gate.

