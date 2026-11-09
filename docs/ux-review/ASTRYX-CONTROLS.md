# Astryx controls: reference and macOS adaptation

This review studies behavior and component ownership, then adapts them to NextThought's paper-and-ink interface. It does not install Astryx, execute its scripts, copy its implementation, or replace native macOS semantics.

Reference repository: [meta/astryx](https://github.com/meta/astryx/tree/5baebb01d37d0af485b16a7a3c8893e9dfcf247b), fixed commit **`5baebb01d37d0af485b16a7a3c8893e9dfcf247b`** (`5baebb0`). The local read-only reference is `app/.local/reference/astryx`. The links below point to that exact revision, not a moving branch.

## Evidence and authority

The three family records and core Button record examined here declare `authority: current`. That means they define the intended contract; it does not prove that every listed behavior is implemented. These documents explicitly record implementation gaps and pending decisions. `docs/specs/components/Button/Button.swift.spec.md` declares **`authority: draft`**, with candidate invariants awaiting verification. It must not be presented as a complete, shipping SwiftUI library contract.

The review compares family rules, component specs, component `.doc.mjs` records and actual `packages/core/src` behavior. Source-verified facts, intended contracts and proposed NextThought adaptations are kept distinct below. This document does not claim runtime accessibility or visual acceptance based on reading code.

| Reference | Responsibility |
|---|---|
| [Button family](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/docs/families/buttons.md) | Naming, activation, visual states, async actions, shared size, elevation and group ownership |
| [Input-field family](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/docs/families/input-fields.md) | Field shell, widths, trailing controls, status, async value changes and source loading |
| [Action-group family](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/docs/families/action-groups.md) | Shared sibling-action tracking, touch/mouse distinction, menu exceptions |
| [Core Button spec](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/docs/specs/components/Button/Button.spec.md) / [source](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/Button/Button.tsx) | Concrete variants, sizing, focus, loading and activation guards |
| [Swift Button draft](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/docs/specs/components/Button/Button.swift.spec.md) | Proposed Apple adaptation; iOS/iPadOS glass rules are not macOS rules |
| [Core tokens](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/theme/tokens.stylex.ts) | Default spacing, type, height, radius, motion and focus values |

## Product decisions that govern adaptation

The user's screenshot feedback is authoritative: **do not turn default-template values into full-width gray dropdowns, and do not use full-width gray segmented headers or update-channel selectors.** Astryx's generic form variant is not a mandate to repeat heavy field chrome throughout Settings.

NextThought therefore keeps these distinctions:

- Template defaults stay in 42-point rows with fine dividers, readable type labels and compact, quiet value menus. A value control hugs its content within the available row; it does not fill all remaining space with gray.
- Settings page navigation uses a restrained tab treatment. Navigation, choosing one setting value and invoking an action are different responsibilities and must not share one oversized segmented appearance.
- Bordered fields remain useful for editing text. Persistent labels and explicit status make those fields understandable; the border alone does not provide clarity.
- macOS uses native Button, Menu/Picker, text input, Toggle and Slider behavior wherever possible. Shared styling standardizes their appearance without replacing their keyboard and assistive-technology meaning.
- Neutral paper-and-ink colors remain the product language. Astryx's accent/error tokens inform hierarchy and state, not a wholesale palette replacement.

## Complete family map

### Buttons: five members

| Member | Owns | SwiftUI adaptation |
|---|---|---|
| Button | Momentary action, shared variants, focus/press, size and pending presentation | Native Button plus a shared visual surface; action owner guards repeated pending activation |
| IconButton | Explicit icon-only projection of Button | Same surface/state contract, square geometry, required accessible action name |
| ToggleButton | Retained pressed state and reversible async state change | Native Toggle or explicitly selected action; selected state is semantic, not inferred from a tint argument |
| ButtonGroup | Connected surface, one elevation, inherited size, roving keyboard focus | Only for an actual connected action group; a random HStack does not become this component |
| ToggleButtonGroup | Single/multiple selection of separate toggle surfaces | Group owns selection and accessible name; spacing and connected geometry remain separate choices |

Spinner, Tooltip, Icon, SizeContext and LinkProvider collaborate with these members. Switch, CheckboxInput and RadioList represent values; SegmentedControl and TabList have their own selection/navigation contracts. They are not alternative button skins.

### Input fields: thirteen members

| Members | Shared obligations | Deliberate variation / shipped capability |
|---|---|---|
| TextInput | Stable width, label, description, status, clear and non-overlapping trailing space | Native single-line editing; IME-safe submission; supports value busy/changeAction |
| TextArea | Same field semantics and status | Multiline growth; not InputGroup-compatible |
| NumberInput | Numeric editing with field semantics | No shipped family `isLoading` or `changeAction`; do not invent parity from appearance |
| DateInput, TimeInput | Field shell and bounded value/clear/status controls | Both support InputGroup; calendar/time interaction remains component-owned |
| DateRangeInput, DateTimeInput | Consistent field states and supporting text | Not InputGroup-compatible in the reviewed capability table |
| FileInput | Value, label, description, clear/status and meaningful work state | Compact/dropzone presentations; no public shared-size prop |
| Selector, MultiSelector | Clear selected value, bounded trailing affordances, source/value distinction | Both support InputGroup; standalone Selector may follow displayed content width |
| ComplexSelector | Equivalent labeled field shell and busy/status feedback | Caller owns rich selection content; no shipped InputGroup or disabled-reason API |
| Typeahead | Input width and trailing lane remain stable | BaseTypeahead owns source work; no family `isLoading` or `changeAction` |
| Tokenizer | Named field, token/value editing and bounded controls | Standalone tokens may wrap; grouped form needs its explicitly supported overflow adaptation |

Field and FieldStatus own labels, descriptions and status placement; FormLayout owns external arrangement; InputGroup owns its connected row and must admit each child explicitly. A component sharing a border helper is not evidence that it supports grouping.

CheckboxInput/CheckboxList, RadioList, Switch and Slider are separate labeled-value controls. Field semantic targeting also applies to CheckboxList, RadioList and Slider. They do not inherit text-field content/trailing-lane geometry.

### Action groups: twenty-one member surfaces

The membership covers DropdownMenu and submenus, ContextMenu, Selector/MultiSelector options, DateInput/DateTimeInput/TimeInput wheels, DateRangeInput presets, AlertDialog, ButtonGroup, ToggleButtonGroup, Toolbar, Banner, Toast, TopNavMenu, SideNavHeading, TopNavHeading, NavHeadingMenu, BreadcrumbItem's menu and TabMenu.

Touch allows sliding between sibling actions and activates only the final enabled target on release; leaving the group or cancelling activates nothing. The family decision explicitly limits this sibling sliding model to touch. Desktop buttons keep their mouse model. Menus and option lists keep native menu tracking. SegmentedControl is a named exception; TabList scrolling, RadioList/CheckboxList, InputGroup caret manipulation and input-focused suggestions have their own contracts.

## Values and invariants worth reusing

These are default reference values, not an obligation to replace every NextThought token. Theme expansion may alter them.

| Area | Reference value / rule | Application decision |
|---|---|---|
| Control size | `sm/md/lg` = **28/32/36px**, default `md` | Establish a shared size axis; do not let every call site invent a height. Preserve larger native/user text without clipping. |
| Type | Body and label **14px**, label weight **500** | Keep one coherent product type scale; no arbitrary 12.5/13/14 differences between equivalent controls. |
| Spacing | Base unit4px; common gaps8px and12px | Align labels, values and action groups using shared spacing. |
| Button surface | Horizontal padding12px, gap8px, element radius8px | Equivalent action surfaces share geometry. These are not reasons to fill an entire settings row. |
| Variants | primary accent, secondary neutral, ghost transparent, destructive error; default secondary | Separate prominence from size, selected state and whether an action is destructive. |
| Icon size | sm16px for sm/md buttons; md20px for lg | Icon-only actions reuse Button's box and state. An icon is not its accessible name. |
| Focus | 2px outline, 3px offset; adequate adjacent contrast | Wire to actual keyboard focus. Synthetic screenshot/test environment state is not real focus evidence. |
| Busy | Stable geometry/name; ordinary pending actions deduplicate immediately | Keep normal opacity and focus while busy. Suppress repeat invocation independently of the spinner's visual delay. |
| Motion | Default fast175ms; action spinner reveal after medium-min310ms | Short actions need not flash a spinner. Explicit loading is immediate. Reduced motion removes scale/fade behavior. |
| Checkbox/radio boxes | sm20px, md24px | Preserve semantic mixed/single-selected states. Match row rhythm rather than enlarging the drawn indicator to the entire hit target. |
| Switch track | sm32×20px, md40×24px | Immediate setting changes; keep label/description readable. Async state feedback belongs inside the thumb. |
| Slider | Track4px, thumb20px; default range0–100, step1 | Display meaningful units; use the product's actual bounds. Preview during movement, persist at the appropriate completion boundary. |
| Pointer target | Coarse-pointer targets may expand toward44px without changing the visible geometry | Do not make every macOS button44px tall. Respect native pointer behavior and neighboring targets. |

Detailed source: [IconButton](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/IconButton/IconButton.tsx), [ToggleButton](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/ToggleButton/ToggleButton.tsx), [ButtonGroup](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/ButtonGroup/ButtonGroup.tsx), [TextInput](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/TextInput/TextInput.tsx), [CheckboxInput](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/CheckboxInput/CheckboxInput.tsx), [RadioListItem](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/RadioList/RadioListItem.tsx), [Switch](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/Switch/Switch.tsx), [Slider](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/Slider/Slider.tsx).

## Behavioral rules for the shared SwiftUI layer

### Actions and pending feedback

Keep resting, hover, keyboard focus, pressed, selected, disabled and busy as separate states. A shared ButtonStyle should own presentation; the action owner must also prevent duplicate execution, including two activations before a view redraw. Persistent toggle actions remain reversible according to their own ownership model.

For pending actions, reserve the label's original geometry and overlay a spinner instead of inserting an extra sibling into the label. Keep the accessible purpose stable and announce the busy transition once. Disabled controls must not run callbacks; explain why an unavailable action is unavailable through persistent text or a keyboard/VoiceOver-reachable description. A tooltip on an unfocusable disabled control is insufficient by itself.

Elevation belongs to the visible surface. A connected ButtonGroup gets one shadow; its children do not each add another. Hover/press feedback should not invent an unrelated resting elevation.

### Editing and validation

Use persistent labels for fields whose purpose would disappear when their placeholder is replaced. Keep example text as a placeholder or description. The base identity, focus and accessibility semantics belong to the actual editable/selectable control, not only a decorative enclosing row.

Place clear, spinner, status and disclosure in independent layout space. A rendered affordance cannot cover the value or caret. Remove unused lanes when the affordance is absent. A clear action uses a generic short tooltip but a contextual accessible name, and restores input focus. Pressing Return to confirm Chinese/Japanese/Korean IME composition must not prematurely save or submit.

Failed validation keeps the whole draft. State the field-level reason, move focus to the first missing field and make it visible in the scroller. Do not silently filter partly completed sections to make a save succeed. A save that was rejected or not accepted by the current storage interface keeps the editor open.

Status placement is capability-based: detached text is the reliable baseline. Attached messages require a suitable opaque, bordered, fixed-height surface and explicit support. Compact value menus should not grow into full-width gray fields just to carry validation text.

### Selection and value controls

Selector's [component docs](https://github.com/meta/astryx/blob/5baebb01d37d0af485b16a7a3c8893e9dfcf247b/packages/core/src/Selector/Selector.doc.mjs) distinguish input, secondary and ghost triggers. NextThought chooses compact quiet selectors for Settings because the setting label and selected value already supply the row's structure, and the user explicitly rejected heavier treatment. Keep a visible disclosure, clear hover/focus and the complete selected value through accessibility.

Separate value-saving feedback from source fetching. Existing options remain usable while a value is resolving; initial source-pending is explicit; a completed empty source gets an honest empty state. Do not use an indefinite spinner for a list with no choices.

Use native Menu/Picker opening, keyboard navigation, selection and focus return. When implementing a searchable popup, do not auto-select the first match on Enter unless it is actually highlighted; query changes clear stale highlight. Read-only controls retain a readable value and omit mutation affordances.

Use Switch for an immediately applied preference; Checkbox for a value collected before form submission; Radio/Picker for an exclusive choice. Slider arrow keys adjust by step, Page keys by ten steps, Home/End reach the real bounds, and the announced/readout value includes units. Repeated live preview updates should not force unnecessary durable writes.

## Concrete application work

| File / area | Desired behavior | Scope / verification |
|---|---|---|
| `Sources/NextThought/LiquidGlass.swift` shared buttons | Consolidate size/variant/state geometry, actual hover/focus and stable busy feedback; preserve compatibility wrappers where useful | Main workstream owns the global styles. Check every affected button at real keyboard focus, not only synthetic acceptance states. |
| `Sources/NextThought/LiquidGlass.swift` MenuPicker | One consistent compact value-menu treatment; no full-width gray template dropdowns | Keep labels and values distinct; use native menu tracking. Check long Chinese/English values and narrow windows. |
| `Sources/NextThought/SettingsView.swift` navigation and channels | Task navigation and setting-value selection have distinct semantics and restrained surfaces | Main workstream owns header tabs, channel selectors and shared layout. |
| Settings search | Clear restores typing focus; results and no-result state remain understandable | Existing Cmd-F/escape/native sidebar behavior must remain available. |
| Settings async actions | Pending does not gray out as unavailable or alter label width | Test engine, model download, cancellation, retry and permission states individually. |
| `Sources/NextThought/NotesSettingsView.swift` default templates | Preserve 42-point rows, fine separators and compact quiet value menus | Explicit screenshot constraint; this pass leaves that region unchanged. |
| Notes template editor | Persistent field labels; validate names and at least one section; no instruction loss; focus missing field; accepted save closes editor | Implemented in this pass. Empty or whitespace-only section titles are rejected rather than removed. |
| Custom template deletion | Native confirmation with effect explained, destructive role, safe cancel default | Implemented in this pass. Deletion resets affected defaults to built-ins and does not delete generated notes. |
| Custom template action hierarchy | Editing is a secondary action, deleting is quieter and explicitly destructive | Reuses paper-and-ink shared styles; no new global style is introduced here. |

The existing `updateNotesTemplate` interface returns no result and `saveNotesSettings` writes UserDefaults without exposing a durable-write error. The editor checks that the template still exists and that the existing API accepts the complete updated value before dismissal. This is not a claim that an fsync or an external storage acknowledgment has been verified; a future storage-result API should report that directly.

## Reference gaps and platform limits

- The field family's DEC-2 separates value loading from option-source loading. The reviewed Selector implementation still uses `isLoading` when suppressing empty/no-result states. Its component spec explicitly warns against documenting the unimplemented source behavior as shipped. Adopt the intended separation, not this ambiguity.
- Button/Radio disabled focus behavior and several reduced-motion behaviors have documented gaps. A native macOS adaptation needs its own keyboard/VoiceOver evidence rather than inheriting a conformance claim from the web library.
- TextInput's spec calls for text growth, while the inspected source uses fixed size heights. Prefer minimum geometry that accommodates the user's text, and verify actual layout.
- Swift Button's glass rules are draft and limit bar/floating glass to iOS/iPadOS26. They explicitly leave macOS at the token look. Do not infer a glass requirement for this Mac app.
- The 16px focus-zoom floor exists for iOS web behavior. It is not a macOS input-font requirement. Touch sibling sliding and coarse-pointer hit expansion are similarly platform-specific.

## Acceptance after integration

The implementation owner should verify these in an isolated review app:

1. Default-template rows and Settings navigation remain compact at narrow and wide sizes; no full-width gray selectors reappear. Long values truncate without moving or obscuring their row label.
2. Empty template name and missing section name produce visible inline reasons; Save focuses and scrolls to the first invalid field. Previously typed instruction text remains byte-for-byte present after rejection and correction.
3. A completely empty section must be named or explicitly removed. A template with no sections cannot be saved. Valid save preserves every section and closes only after the current settings API accepts it.
4. Template deletion always opens native confirmation; Return defaults to Cancel, Escape cancels, and cancellation leaves templates and defaults untouched. Explicit Delete removes only the chosen custom template and resets its default mappings.
5. Keyboard traversal, Return/Space, native menu selection, search clear/focus return and VoiceOver naming work in Chinese and English. Icon-only actions announce their purpose.
6. Fast and slow actions do not shift their labels or execute twice. Retry/cancel are understandable. Reduced-motion, light/dark and increased-contrast states remain readable.

This pass performed source review and a syntax parse of NotesSettingsView. Full integration builds, native confirmation behavior and graphical acceptance belong to the main workstream's isolated verification; no successful runtime result is implied here.
