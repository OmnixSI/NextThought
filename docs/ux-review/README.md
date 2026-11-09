# Academic UX implementation and verification

The review targets macOS research meetings, preserving existing records and user preferences. Production `/Applications/InterviewHacker.app` is not replaced by this workflow.

## Workstreams

| Area | Information architecture | Flow / efficiency | Feedback | Visual hierarchy / consistency |
|---|---|---|---|---|
| Onboarding / Home | Academic example and recent research | Three-step start, AI optional | Recording readiness distinct from AI | One primary start action |
| Preparation | Single-session context, project defaults separate | Sticky start and explicit carry | Typed dependency/waiting states | Academic type order |
| Live / floating / subtitles | Transcript, personal notes, optional answer | Highlights, pause-follow, keyboard access | Interrupted answer retained, real saving state | Readable text and named controls |
| Notes / formats / evidence | User edits separate from AI original | Edit and inspect supporting transcript | Save acknowledgment, obsolete format indicator | Shared effective content in copy/export |
| Records / projects / materials | Projects and courses aggregate their records | Full text search, recent and pinned projects | Search progress and useful document warnings | Single empty state and consistent terminology |
| Settings / recovery | Existing twelve destinations | Item-level search and restore preview | Explicit conflicts and failure counts | Shared row and status conventions |

## Large PDF changes

Readable PDF text is extracted directly even if it contains mathematical symbols, tabs or multiple columns. Previously these heuristics forced an entire page through OCR and performed expensive character and pairwise line comparisons. Scanned pages still require recognition; no universal speed guarantee is implied.

Pages are checkpointed independently. Cancellation preserves completed checkpoints, failed pages do not invalidate completed pages, and failed OCR is presented as a document warning. Missing dependencies retain their actionable waiting state. Raster image files are removed after each OCR request and worker OCR is bounded to 90 seconds per page.

`--self-test large-pdf` generates 400 scientific text pages including mathematical notation, requires all physical pages and text to survive, verifies strict Paddle selection does not unnecessarily invoke OCR, and compares checkpoint replay content. Actual results, mixed-page safeguards and limitations are recorded in `VALIDATION.md`.

## Isolated preview

Build using `scripts/build-ux-preview.sh`. This creates `.local/AcademicUXReview.app` with a separate preview bundle identity. Use `INTERVIEWHACKER_DATA_DIR` pointing inside this repository for fixture/self-test runs. Never run demo mode using the production bundle identity: demo mode changes preferences.

## Acceptance record

Record actual test commands, results and remaining limitations in `VALIDATION.md` after integration. UI coverage must include 960×660, 1120×820 and a large window; Chinese/English; light/dark; keyboard and accessibility inspection. Synthetic records do not establish real user-study outcomes or real scanned-PDF performance.
