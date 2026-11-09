# Live session UI acceptance matrix

Run the deterministic native matrix on macOS with:

```sh
scripts/capture-live-session-acceptance.sh
```

Optionally pass an output directory. The default is `.local/validation/live-session-ui/`. Each run builds the Swift executable and renders the real `LiveSessionView` with `NSHostingView`; it does not create an app bundle, change a Bundle ID, sign, install, request permissions, write an archive, record audio, run ASR, call a model, or access the network.

The output contains `manifest.json`, `report.md`, PNGs named `synthetic-<scenario>-<appearance>-<width>.png`, and a separate `synthetic-component-samples-light-1200.png` sheet. The manifest derives the scenario count from the captured case list and maps each matrix capture to its dimensions and explicit synthetic-state flags. All scenarios cover light and dark appearances at 1200 pt, the current 960 pt minimum, and a 900 pt below-minimum stress render. Every live-page capture uses a long Chinese/English title. The `large-text` case sets `DynamicTypeSize.accessibility3`, but this does not enlarge macOS text and is explicitly not large-text acceptance evidence.

The acceptance script also refreshes the compact tracked evidence set at [`docs/validation/v2.4.1/live-session-ui/`](validation/v2.4.1/live-session-ui/): `manifest.json`, `report.md`, representative PNGs for all 19 scenarios, the component sample sheet, and a separate dark 1200 pt evidence-popover capture.

Scenarios cover normal, streaming, busy, busy+focus, success, save warning, save recovery, unavailable, stale, stale+manual-edit, empty, reading history, streaming+history, selection, selection+focus, cancel+late-callback, focus, evidence overlay, and accessibility3 override. The interaction checks exercise the scroll-follow state and scroll-input classifier, directly verify that a late answer completion cannot replace a cancelled partial result, and compare native geometry before/after a long partial transcript grows while reading history is pinned.

## Evidence limits

- All transcript, answer, and persistence states are fabricated for layout coverage. The streaming/history geometry check is deterministic but synthetic; it does not replace a real audio/ASR stream or real-meeting recording.
- Save warning and recovery are seeded in-memory persistence snapshots. They verify the status UI, not disk failure/retry behavior.
- Stale notices and manual corrections are synthetic because this matrix does not exercise a real archive revision transition.
- Selection coverage verifies the selection-pinned scroll model and incoming-item count; it does not fabricate a native selected-text highlight.
- The 900 pt image is a stress render below the app's 960 pt minimum. It is not a supported window size.
- On macOS, the Dynamic Type environment override does not resize text. The accessibility3 image does not verify enlarged type, and there is no app-level text-size control; large-text acceptance remains open.
- Hover, pressed, and overlap focus-ring appearances are injected preview states, not native pointer or keyboard event recordings. The matrix does not capture a native operation video.
- This focused matrix does not replace full permission, VoiceOver, or Chinese/Japanese IME testing in blueprint §12.4.
