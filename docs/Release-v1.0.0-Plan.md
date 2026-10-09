# Windows VPN Console v1.0.0 Implementation Plan

This plan translates the v1.0.0 scope into reviewable engineering slices. It is intentionally evidence-driven: a protocol, engine, or authentication path is not marked supported unless the implementation can either control it directly or clearly labels the remaining manual/vendor step.

Current scope: the user deferred independent engine work in favor of the 0.2.1 interface preview. This document is a future roadmap, not a claim that every item is implemented or an instruction to change a working VPN setup.

## Current Baseline

- Existing release: v0.2.0.
- Stack: .NET 10, WPF, MVVM-style view model, provider-based VPN backends.
- Current providers: Windows native VPN, WireGuard for Windows, OpenVPN Community, NCP interactive handoff, Shrew interactive handoff, Mock.
- Current safety posture: profile JSON excludes secrets, optional Windows Credential Manager storage, redacted diagnostics and evidence export.
- Current validation boundary: automated software tests pass in prior Windows CI, but live VPN handshakes and lab server authentication remain external validation tasks.

## Phase 1: Audit and Foundation

- Preserve the existing provider architecture and profile storage.
- Add explicit engine integration metadata:
  - Integrated
  - Windows Native
  - External Managed
  - External Interactive
  - Unavailable
- Add capability flags for connect, disconnect, import, EAP, XAUTH, PSK, certificate authentication, live status, traffic counters, split tunneling, IPv6, administrator rights, external installation, and licensing requirements.
- Keep external launchers honest: opening NCP or Shrew remains an external interactive handoff unless a documented, tested profile-scoped control API is added.

## Phase 2: Diagnostics and Evidence

- Separate provider-reported state from observed Windows network evidence.
- Classify adapter, assigned-address, route, gateway, and target-reachability observations without converting them into a fake CONNECTED state.
- Improve the NCP scenario where Windows can show a virtual adapter/address/route while provider telemetry remains unknown.
- Keep encryption claims tied to packet captures or provider/security-association evidence, not to UDP traffic or route presence alone.

## Phase 3: Profile and UI

- Add a guided profile wizard while preserving the existing editor and saved profiles.
- Keep protocol-specific fields narrow:
  - IKEv2: EAP, PSK/certificate server-auth metadata, proposals, identities.
  - IKEv1/XAUTH: XAUTH-specific workflow and legacy warning.
  - L2TP/IPsec: Windows native PSK plus PPP authentication.
  - OpenVPN: imported profile and supported auth modes.
  - WireGuard: keys, AllowedIPs, endpoint, DNS, keepalive.
- Keep BASIC and ADVANCED modes.

## Phase 4: Protocol Backends

- Preserve the working NCP fallback for IKEv2 PSK plus EAP-MSCHAPv2.
- Investigate integrated IKEv2 only with verified Windows capability for PSK server authentication plus EAP-MSCHAPv2 user authentication.
- Treat Shrew as legacy IKEv1/XAUTH only; do not claim Windows 11 success without live testing.
- Preserve native Windows L2TP/IPsec support and current OpenVPN/WireGuard adapters.

## Phase 5: Release Hardening

- Add migration tests for v0.2.0 profiles.
- Add capability matrix tests for every provider.
- Add diagnostics tests for external-client observed evidence.
- Update release docs, known limitations, and compatibility matrices.
- Build and package only after tests pass; mark live protocol support as not tested unless validated against a real endpoint.

## Completion Definition

v1.0.0 is ready only when the application builds, tests pass, existing profiles remain compatible, secrets remain protected, and unsupported or unverified protocol behavior is visibly marked as fallback or not tested.
