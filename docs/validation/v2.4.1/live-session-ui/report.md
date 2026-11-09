# T07 Live Session Acceptance Evidence

Generated at: 2026-11-09T04:01:13Z  
Source revision: `bccece1+worktree`  
Tracked evidence includes 24 representative scenario PNGs, one evidence popover PNG, and one component sample sheet PNG. The reproducible run produced 114 native matrix captures for both appearances, three widths, and all 19 scenarios. `manifest.json` maps every tracked matrix image and records the full dimensions.

All transcript, answer, and persistence states are synthetic. Captures use the actual SwiftUI live page rendered through AppKit with locale `zh-Hans`; they use no real audio, ASR, model call, network request, or archive write. The full local matrix is at the requested output directory; run `scripts/capture-live-session-acceptance.sh` to regenerate it and refresh these representative files.

## Interaction checks

| Check | Result | Evidence |
|---|---|---|
| `reading-history` | passed | ScrollFollowState holds position, counts six new items, and resumes only on explicit return. |
| `selection-pinning` | passed | Selecting text pins the viewport through new content; explicit return releases it. |
| `scroll-navigation-input` | passed | Arrow/Page navigation keys and pointer-inside hit testing are classified; unrelated keys and outside points are ignored. |
| `keyboard-focus` | rendered | The live pause control receives keyboard focus in the native SwiftUI capture. |
| `evidence-overlay` | passed | Captured the open evidence details popover at synthetic-overlay-dark-1200-popover.png alongside the live page. |
| `busy-focus` | rendered | The focus-busy capture shows the Stop control's focus ring while generation is active; focus is a synthetic preview state. |
| `selection-focus` | rendered | The selected-focus capture shows the return-to-live action's focus ring; focus is a synthetic preview state. |
| `cancel-late-callback` | passed | A synthetic finish after cancellation was ignored; the cancelled phase and partial text remained unchanged. |
| `streaming-history-layout` | passed | Expanded synthetic partial text by 203 pt; composer drift 0.0 pt and primary-action drift 0.0 pt (≤0.5 pt). |
| `component-sample-sheet` | passed | Captured the native component sample sheet at synthetic-component-samples-light-1200.png. |
| `accessibility-text-size` | limited | The renderer records an accessibility3 environment override, but native macOS text remains the same size; app-level font enlargement is not implemented. |

## Known acceptance gaps and limits

- The 900 pt capture is a below-minimum stress render; the current app window minimum is 960 pt.
- Save warning and recovery are seeded persistence snapshots. They do not simulate a disk fault or execute a retry against storage.
- Stale notices and manual corrections are synthetic acceptance fixtures; this matrix does not exercise a real archive revision change.
- Selection coverage verifies selection-pinned scroll state and unread-count behavior; it does not synthesize a native text range highlight.
- The streaming+history geometry check uses a synthetic partial transcript, not an audio/ASR stream or real meeting.
- Hover, pressed, and overlap focus rings are deterministic previews, not native pointer or keyboard recordings.
- The focused suite does not cover live device permission prompts, VoiceOver navigation, Chinese/Japanese IME composition, or Reduce Motion behavior.
- The accessibility3 environment override is a no-op for text sizing on macOS; font enlargement remains unverified and no app-level text-size control is provided.
- No native operation video is included in this focused T07 evidence set.

## Component sample sheet

![Native component state samples](synthetic-component-samples-light-1200.png)

## Representative screenshots

### normal · light · 1200 pt

![normal · light · 1200 pt](synthetic-normal-light-1200.png)
### normal · dark · 960 pt

![normal · dark · 960 pt](synthetic-normal-dark-960.png)
### normal · light · 900 pt

![normal · light · 900 pt](synthetic-normal-light-900.png)
### streaming · light · 900 pt

![streaming · light · 900 pt](synthetic-streaming-light-900.png)
### streaming · dark · 1200 pt

![streaming · dark · 1200 pt](synthetic-streaming-dark-1200.png)
### busy · dark · 960 pt

![busy · dark · 960 pt](synthetic-busy-dark-960.png)
### success · light · 1200 pt

![success · light · 1200 pt](synthetic-success-light-1200.png)
### save-warning · light · 960 pt

![save-warning · light · 960 pt](synthetic-save-warning-light-960.png)
### save-warning · dark · 1200 pt

![save-warning · dark · 1200 pt](synthetic-save-warning-dark-1200.png)
### save-recovery · dark · 960 pt

![save-recovery · dark · 960 pt](synthetic-save-recovery-dark-960.png)
### unavailable · dark · 1200 pt

![unavailable · dark · 1200 pt](synthetic-unavailable-dark-1200.png)
### stale · light · 960 pt

![stale · light · 960 pt](synthetic-stale-light-960.png)
### empty · dark · 900 pt

![empty · dark · 900 pt](synthetic-empty-dark-900.png)
### reading-history · light · 1200 pt

![reading-history · light · 1200 pt](synthetic-reading-history-light-1200.png)
### selection · dark · 960 pt

![selection · dark · 960 pt](synthetic-selection-dark-960.png)
### focus · light · 960 pt

![focus · light · 960 pt](synthetic-focus-light-960.png)
### overlay · dark · 1200 pt

![overlay · dark · 1200 pt](synthetic-overlay-dark-1200.png)
#### Evidence details popover

![Evidence details popover](synthetic-overlay-dark-1200-popover.png)
### large-text · light · 960 pt

![large-text · light · 960 pt](synthetic-large-text-light-960.png)
### large-text · dark · 1200 pt

![large-text · dark · 1200 pt](synthetic-large-text-dark-1200.png)
### focus-busy · light · 960 pt

![focus-busy · light · 960 pt](synthetic-focus-busy-light-960.png)
### stale-manual-edit · dark · 1200 pt

![stale-manual-edit · dark · 1200 pt](synthetic-stale-manual-edit-dark-1200.png)
### streaming-reading-history · light · 1200 pt

![streaming-reading-history · light · 1200 pt](synthetic-streaming-reading-history-light-1200.png)
### selected-focus · dark · 960 pt

![selected-focus · dark · 960 pt](synthetic-selected-focus-dark-960.png)
### cancel-late-callback · dark · 1200 pt

![cancel-late-callback · dark · 1200 pt](synthetic-cancel-late-callback-dark-1200.png)
