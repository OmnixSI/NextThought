# Stability implementation — 2026-10-23

Baseline: main `e8ec23a`; local application 1.8.0 / source `c5543aa`.
The implementation includes PR #16 (`aa980aa`) without merging or publishing it.
Checkout stays in the interviewhack project. No production application/data replacement.

## Delivery sequence

- [ ] B: coherent answer state; recoverable SSE/polling; event backups off the UI thread.
- [ ] C: durable pre-meeting document jobs; real progress; resumable parsing/indexing; formats and capacity.
- [ ] D: selected screen target, fresh question context, local OCR enhancement, evidence/search routing.
- [ ] E: encrypted relay and browser fallback, container deployment.
- [ ] F: native iOS/Android companion and BLE transport.

## Validation rules

Automated and isolated application checks are separate from physical-device and real-meeting acceptance.
No real-device latency, OCR accuracy, or 60-minute meeting pass is claimed without measurement.
Full Xcode/iOS SDK is not currently selected/installed; macOS Swift CLT is available.

## Acceptance pending real devices/data

- iPhone Safari and Android Chrome: 60 minutes, background/foreground, network loss and recovery.
- Native phone BLE: packet loss, Wi-Fi loss, foreground/background and switching.
- Independent document/retrieval/search evaluation corpus and real conference replay.

## Stage B verification

- 152 Swift tests passed (includes command deduplication, ordered snapshot framing,
  content-addressed backup deduplication, recovery and corruption rejection).
- Offline browser regression passed: initial snapshot, SSE heartbeat, stale frames,
  new-question empty state, stalled-stream polling, SSE errors and command IDs.
- Existing offline Codex transport regression and four Python normalization tests passed.
- Isolated `.ci.stability` app data export/import round trip passed; pre-import
  recovery points are generated asynchronously. Production data was not used.
- Physical phone and 60-minute timing acceptance remains pending.
