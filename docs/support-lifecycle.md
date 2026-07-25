# Support and product lifecycle

**Owner:** Kristopher Turner / Hybrid Solutions Cloud  
**Applies from:** 2026-07-24  
**Review cadence:** Before every release and at least every 90 days

## Current support status

Vault Prospector is a Preview product for non-production evaluation. The only currently supported
evaluation build is `0.2.0-preview.1`. “Supported” at this stage means that the maintainer accepts
privacy-safe feedback and private vulnerability reports and may provide a replacement Preview or
containment guidance. It is not a production-support commitment or contractual service-level
agreement.

There is no supported stable or GA release. Source branches, pull-request artifacts, CI builds,
prototypes, withdrawn releases, and locally built packages are not supported distributions.

## Version states

| State | Meaning | Maintenance behavior |
| --- | --- | --- |
| Current Preview | Latest Preview explicitly named in the release-scope document | Feedback and vulnerability intake; security or release-blocking defects may receive a replacement immutable Preview |
| Superseded | A newer supported Preview or stable release is available | No routine fixes; upgrade to the named replacement |
| Withdrawn | The release has a trust, security, data-loss, upgrade, or core-function defect | Do not install; follow published containment/recovery guidance |
| Current stable | A future release explicitly approved through every GA gate | Production support begins only when its release record publishes scope, supported platforms, channels, and end-of-support terms |
| End of support | The announced support period has ended | No fixes or routine investigation; users must upgrade or remove the product |

Only a release record and the canonical
[release-scope document](product/release-scope.md) can move a version between these states. Assets
remain immutable: withdrawal or replacement never changes files under an existing version.

## Preview supersedence and withdrawal

Publishing a newer Preview supersedes older Preview builds unless the new release record explicitly
states otherwise. The older build receives no routine fixes. A security-sensitive or broken build
may be withdrawn immediately without advance notice. Withdrawal requires:

1. a prominent notice on the release;
2. affected-version and immutable-artifact identification;
3. safe containment, credential-rotation, upgrade, or local-state recovery guidance;
4. package-manager withdrawal or moderation requests when applicable; and
5. a release-evidence record linked from the readiness matrix.

The current history is:

| Version | State | Required action |
| --- | --- | --- |
| `0.2.0-preview.1` | Current Preview | Non-production evaluation only |
| `0.1.1-preview.1` | Superseded Preview | Upgrade to the current Preview |
| `0.1.0-preview.2` | Withdrawn | Do not install or resubmit |
| `0.1.0-ci.68` | Superseded CI artifact | Replace with the current Preview |

## Stable and GA end-of-support policy

Before the first stable or GA release, its go/no-go record must publish:

- supported Windows editions/builds and processor architecture;
- supported direct, WinGet, and Chocolatey installation/update paths;
- the supported release line and exact upgrade path;
- an end-of-support date and successor policy;
- support/security owners and a backup operator;
- signing-certificate custody, expiration, rotation, revocation, and compromise handling; and
- any dependency or platform end-of-support date that occurs earlier.

A planned GA end of support requires at least 90 calendar days’ public notice and a tested upgrade
path to a supported successor. An immediate withdrawal is permitted when continued distribution
would expose protected data, bypass a security boundary, break upgrade/recovery, or invalidate
artifact trust. The security exception takes precedence over the notice period.

Vault Prospector never claims support beyond an underlying runtime or operating-system lifecycle.
The operational-readiness monitor warns 120 days before a recorded runtime end-of-support date and
fails after that date. A release candidate cannot pass GA while its required runtime or supported
Windows baseline will expire inside the announced product support period.

## Channels and response targets

| Request | Channel | Target |
| --- | --- | --- |
| Non-sensitive Preview feedback | [Public feedback instructions](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/blob/main/FEEDBACK.md) | Queue reviewed each business day; initial classification within three business days |
| Suspected vulnerability | Private email in [SECURITY.md](../SECURITY.md) | Acknowledge within three business days; initial severity assessment or evidence request within seven business days |
| Package validation | Relevant WinGet or Chocolatey moderation channel | Best effort; provider timelines are outside HCS control |

These are Preview operational targets, not contractual SLAs. Never submit credentials, tokens,
secret values, private keys, sensitive screenshots, or unredacted diagnostics through a public
channel.

## Dependency and platform maintenance

Dependabot checks desktop and mobile NuGet manifests and the browser and design-prototype npm
manifests each week. Dependency pull requests are not auto-merged. They
must preserve lock files, pass the applicable locked build/security/package workflows, and receive
normal review.

The scheduled Azure DevOps operational-readiness pipeline checks the machine-readable ownership/lifecycle
contract, direct and transitive desktop NuGet vulnerabilities, runtime lifecycle dates, and public
release/support endpoints. Its JSON report is retained as pipeline evidence. A failed scheduled
run is a support-owner action item and blocks a release until it is dispositioned.

Patch updates should be evaluated promptly. A known critical/high vulnerability, runtime end of
support, compromised build action, or broken public endpoint triggers immediate triage under the
[release operations and incident runbook](release-operations-runbook.md).

## Review record

Every lifecycle review records the date, current supported version, dependency/runtime status,
public-channel checks, credential/signing inventory, owner, reviewer, exceptions, and corrective
dates in release evidence. G-08 remains in progress until a backup operator is named, the scheduled
monitor has successful hosted history, the complete runbook exercise passes, and Microsoft Store
trust evidence exists.
