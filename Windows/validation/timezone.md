# B24: time-zone independence and the UTC verification run

## Why the three tests failed in UTC

`SessionMarkdownExporterTests.HeaderUsesSummaryTitleAndFacts`, `SessionSummaryPromptTests.HeaderIncludesOnlyPresentFacts` and `MeetingOutcomeTests.WhenFollowsTodayYesterdayWeekdayAndDate` built their expectations from a `+08:00` input but the implementations formatted the same instant with `DateTimeOffset.ToLocalTime()`. The process-local zone is Asia/Shanghai on the development machine, so they passed; in a UTC process the header became `01:30` / `01:00` and the "today" comparison shifted, so they failed. The tests were therefore asserting the machine's zone, not the formatter's contract.

## What Windows can and cannot do about the process zone (research)

| Approach | Verdict |
| --- | --- |
| `TZ=UTC` environment variable | Only honoured by .NET on Unix. On Windows `TimeZoneInfo.Local` comes from the OS time-zone setting (`GetTimeZoneInformation`), so this changes nothing. |
| `tzutil /s "UTC"` | Changes the machine-wide zone and requires administrator rights; verify must stay runnable without elevation. |
| `TimeZoneInfo.Local = ...` | The property has no setter; the cache (`s_cachedData`) is internal and reflection is unsupported/fragile. |
| Per-process time-zone API | Windows has none (no equivalent of `setenv("TZ")` for a running process). |
| Container/WSL/registry override | Needs elevation or a different environment; not the release-verification workflow. |

So no supported, non-admin way exists to change `TimeZoneInfo.Local` for the test process. The only honest options are (a) make the tests/implementations take the zone explicitly, and (b) give the verification run a seam that makes the *application's* local-time source UTC while the machine stays untouched.

## Chosen design

1. **The three formatters take an explicit zone.**
   - `MeetingOutcome.When(date, now, english, TimeZoneInfo? timeZone = null)`
   - `SessionMarkdownExporter.Export(..., TimeZoneInfo? timeZone = null)`
   - `SessionSummaryPrompt.Header(..., TimeZoneInfo? timeZone = null)`
   `null` keeps the old behaviour (the machine's local zone), so production output is unchanged. The tests pass a `+08` zone explicitly and additionally assert that passing `TimeZoneInfo.Utc` renders the same instant in UTC — proving the formatter follows its argument and not the process zone.
2. **One seam for "the app's local zone": `NextThought.Core.LocalTime.`** `LocalTime.Zone` defaults to `TimeZoneInfo.Local`, `LocalTime.ToLocal/Now()` convert through it, and the three formatters default to `LocalTime.Zone`. Production never overrides it.
3. **The UTC run is test-side only.** `tests/NextThought.Core.Tests/TestTimeZone.cs` has a `[ModuleInitializer]` that reads `NEXTTHOUGHT_TEST_TIMEZONE` and sets `LocalTime.Zone`. The app never reads this variable (no new production environment variable, no behaviour change); only the test assembly does, and only when verify sets it.
4. **`verify.ps1` adds a second Core run** `tests: NextThought.Core.Tests (UTC)` that sets `NEXTTHOUGHT_TEST_TIMEZONE=UTC` for the duration of the `dotnet test` call, restores the previous value, and writes `artifacts/verify/core-utc.trx`. No elevation required.

## Results

- Local-zone Core run: **1668/1668** passed.
- Simulated-UTC Core run (`$env:NEXTTHOUGHT_TEST_TIMEZONE='UTC'`): **1668/1668** passed, and the test assembly printed `B24: Core tests run with NEXTTHOUGHT_TEST_TIMEZONE=UTC`, confirming the seam was active.
- The three regression tests now have explicit-zone and explicit-UTC assertions; they can no longer pass or fail because of the machine's zone.

Command to reproduce the UTC run manually:

```powershell
$env:NEXTTHOUGHT_TEST_TIMEZONE = 'UTC'
.\.tools\dotnet\dotnet.exe test tests/NextThought.Core.Tests -c Release --no-build --no-restore
Remove-Item Env:NEXTTHOUGHT_TEST_TIMEZONE
```

or simply `Windows/verify.ps1` (the UTC step is part of it).

## Remaining notes / unresolved

- Other UI-facing date formatting still calls `DateTimeOffset.ToLocalTime()` directly (e.g. `Models.SessionMeta.StartedAtText`, `NotesDates`/`NotesTemplates` display strings). Those paths are the app's normal local-time display and were already UTC-safe in the test suite (the full Core run passes under UTC); if a future test needs them zone-independent, route them through `LocalTime.Zone`/pass a zone the same way.
- `NEXTTHOUGHT_TEST_TIMEZONE` is a test-only hook. It is read exclusively by the Core test assembly and documented here; nothing in `src/` reads it.
- Verify cannot prove the OS-level "process is in UTC" state itself (Windows has no such state); it proves every seam the app uses for local-time display is driven by the explicit/injected zone.
