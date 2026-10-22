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

## Stage C implementation and measured validation

- SQLite/WAL preparation jobs are durable and bound to their original workspace.
  The UI accepts consecutive uploads; two light jobs and one heavy job may run.
  Original files are stored once by SHA-256. Cancellation/restart preserves page
  checkpoints and batch vector checkpoints. Meetings pause preparation and retain
  the prepared retrieval index; active display changes do not retarget uploads.
- Limits are now 512 MiB and 2000 PDF pages; former character caps were removed.
  Legacy `.ppt` uses an isolated LibreOffice profile; PPTX groups, merged tables,
  notes and chart values are extracted. Worker progress is delivered to the UI.
- Codex PDFs are page-scoped, Office is assigned bounded groups, long UTF-8 text
  and multipage TIFF are scoped. Successful units are reused after interruption.
- Local OCR selector supports pinned Q8 and Q4 models, with actual selected model
  passed through preparation. Q4 revision `c8987b2` was downloaded and all pinned
  file sizes and hashes verified. The six authored real-route samples completed
  in 25.3 seconds with 27/27 numeric anchors; merged cells, independent columns
  and the authored formula samples were reviewed. This is not full-character
  accuracy or a claim about arbitrary documents.
- 155 Swift tests and seven Python tests passed. A packaged `.ci` application
  exercised three immediate batches totaling 20 text files (including >200k
  characters), a corrupt PDF, workspace switching and SQLite persistence.
- Authored PPTX extraction retained grouped text, merged header, speaker note and
  chart values. A real LibreOffice binary `.ppt` round trip completed in 4.6 s.
- 401, 1000 and 2000 native PDF pages were fully retained; 2001 pages rejected.
  Valid 25/100/500 MiB PDFs passed the actual DocumentImporter with text, numbers
  and page locator intact. These padding-stream PDFs test capacity, not OCR quality.
- User-provided SCAN-BEST paper: 15,395,279 bytes (14.68 MiB), 16 physical pages.
  Real preparation completed in 30.5 s including intentional cancel/retry, with
  16/16 pages, 73,806 output characters and unchanged original SHA-256. Local
  evidence contains the progress trace and warnings; paper contents are not
  committed or uploaded to GitHub. Page coverage is not an accuracy guarantee.
- Real Codex route completed 6/6 authored samples (93.3 s). No user paper was
  submitted to Codex in this test.

Remaining validation: independent retrieval calibration and physical-device/
real-meeting acceptance. Large Excel/Office atomic blocks still need their own
quality corpus; exceeding a processing resource must fail explicitly, not clip.
