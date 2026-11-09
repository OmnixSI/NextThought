# NextThought project instructions

## Local signing and privacy permissions

- On this Mac, build and install with the existing app-bound local signing identity and its established machine-specific signing directory. Keep that certificate and private key in place.
- Never create, rotate, delete, or replace that identity during a local build, install, or source change. Keep Bundle ID `com.nextthought.app` and the app's designated requirement stable.
- Local scripts must fail if the bound identity is missing, invalid, or different. Do not fall back to ad-hoc signing or the separate GitHub release identity.
- `scripts/install-latest.sh` must re-sign downloaded apps with the bound identity and stop before replacing an installed app whose designated requirement differs.
- Do not reset TCC permissions. If the signing identity is unavailable or has genuinely changed, stop and explain the one-time permission impact before taking any identity-changing action.
- GitHub Actions may use its separately configured release signing identity; that identity must not replace the local app-bound identity.
- Before touching signing material, follow the persistent identity and recovery rules in `docs/DEVELOPMENT.md`. Never commit or upload a keychain, private key, or keychain password.
- If the app-support signing directory exists but its identity is incomplete or unusable, treat it as recovery work: do not run `scripts/setup-local-signing.sh` or invoke the app's bind flow. Restore and verify the same existing identity first.
- Run local signing preflight and builds through the approved unsandboxed host execution path when the Codex shell sandbox blocks keychain access. A sandboxed `security unlock-keychain` error such as exit code 206 is not proof the identity is invalid; repeat the documented check outside the sandbox and confirm the matching valid identity. SwiftPM's `--disable-sandbox` option does not remove the outer Codex shell sandbox. If the approved unsandboxed route is unavailable or the check still fails, stop. Never print the keychain password or switch identities.

For this Mac's formal/test workspace, signing, and TCC workflow, also read `.local/LOCAL-PROJECT-MANAGEMENT.md` when that local-only file is present. It is intentionally excluded from Git and records machine-specific paths and current separation limits. Git worktrees do not inherit ignored files; if it is absent, consult the local copy in the primary source checkout before building or signing.
