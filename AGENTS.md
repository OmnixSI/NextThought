# InterviewHacker project instructions

## Local signing and privacy permissions

- On this Mac, build and install with the existing app-bound **InterviewHacker Local Signing** identity in `~/Library/Application Support/InterviewHacker/Signing/`.
- Never create, rotate, delete, or replace that identity during a local build, install, or source change. Keep Bundle ID `com.interviewhacker.app` and the app's designated requirement stable.
- Local scripts must fail if the bound identity is missing, invalid, or different. Do not fall back to ad-hoc signing or the separate GitHub release identity.
- `scripts/install-latest.sh` must re-sign downloaded apps with the bound identity and stop before replacing an installed app whose designated requirement differs.
- Do not reset TCC permissions. If the signing identity is unavailable or has genuinely changed, stop and explain the one-time permission impact before taking any identity-changing action.
- GitHub Actions may use its separately configured release signing identity; that identity must not replace the local app-bound identity.
- Before touching signing material, follow the persistent identity and recovery rules in `docs/DEVELOPMENT.md`. Never commit or upload a keychain, private key, or keychain password.
- If the app-support signing directory exists but its identity is incomplete or unusable, treat it as recovery work: do not run `scripts/setup-local-signing.sh` or invoke the app's bind flow. Restore and verify the same existing identity first.

For this Mac's test-build and TCC workflow, also read `docs/LOCAL-MACOS-SIGNING.md` when that local-only file is present. It is intentionally excluded from Git and records machine-specific details and known separation limits.
