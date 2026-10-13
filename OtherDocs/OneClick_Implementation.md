# One Click: Implementation Guide

| Field | Value |
|---|---|
| Audience | An AI coding agent acting as senior developer, test engineer, HCI designer, and QA lead; and the human product owner |
| Companion | OneClick_SRS.md (requirements are referenced by ID, for example FR-CORE-04) |
| Version | 1.0 |
| Date | 6 October 2026 |

---

## 0. How the agent must work

### 0.1 Roles the agent plays
| Role | Responsibility |
|---|---|
| Developer | Implement requirements with clean, layered, tested code |
| Test engineer | Write tests first for the engine, then for each feature; build fault-injection and property tests |
| HCI designer | Apply section 5 and SRS section 5 to every screen; review each screen against the heuristics |
| QA lead | Maintain the traceability sheet, run release gates, triage defects, write the test report |

### 0.2 Ground rules
1. **Read the SRS first.** Implement only what a requirement ID asks for. If a requirement is ambiguous, state the assumption in the pull request description and in `docs/decisions/`, then proceed with the safest interpretation.
2. **Safety outranks features.** The six product principles (SRS 1.5) override any convenience. No code may change files except through the Operation Engine (FR-CORE-01).
3. **Work in vertical slices, small steps.** One task = one branch = one pull request with tests. Never commit a broken build.
4. **Test first for the engine.** Write the failing test, then the code. UI code gets tests for view-models; screens get automation scenarios.
5. **Never touch real user data in tests.** Use temporary directories or an in-memory file system. Tests must never write outside their sandbox.
6. **No administrator rights, no network calls, no telemetry,** other than the optional update check.
7. **Document as you go.** Update `README.md`, `CHANGELOG.md`, architecture decision records (ADRs), and the traceability sheet in the same pull request.
8. **Stop and ask the product owner** only for the open questions in SRS Appendix E or when a change would alter a Must requirement.

### 0.3 Definition of Done (every task)
- Requirement IDs listed in the pull request; each has at least one passing test (named `TC-<module>-<n>`).
- Build has zero warnings (warnings as errors); static analysis clean; code formatted.
- Unit tests pass; coverage thresholds met (engine 90%, overall 80%).
- For UI work: keyboard-only walkthrough done, screen-reader names set, contrast checked in light and dark themes, empty/loading/error states implemented.
- No hard-coded user-visible strings (resource files only).
- Logging added for failures (no personal data).
- Documentation and changelog updated.

### 0.4 Master prompt (paste first into the agent)
```
You are the lead engineer, test engineer, HCI designer and QA lead for "One Click",
a Windows desktop utility that helps non-technical users rename, organise and clean
files safely and automate repetitive tasks. Your two source documents are
OneClick_SRS.md (what to build) and OneClick_Implementation.md (how to build it).
Follow them exactly. Priorities in order: (1) never lose user data, (2) correctness,
(3) usability and accessibility, (4) performance on 4 GB RAM / HDD laptops,
(5) features. Work in small vertical slices, tests first for the engine, one
requirement set per pull request, and report after each task: what you built,
requirement IDs covered, tests added and their results, assumptions made, and risks.
Do not add features that are not in the SRS. Start with Phase 0 in section 8.
```

### 0.5 Task prompt template
```
Task: <Phase.Task id and title>
Requirements: <FR / UX / NFR ids>
Read: SRS sections <...>, Implementation sections <...>
Deliver: code, tests, docs updates
Acceptance: <copy the gate from section 8>
Report format: summary, files changed, tests run with results, assumptions, open risks
```

---

## 1. Technology decisions

| Decision | Choice | Reason |
|---|---|---|
| Platform | Windows 10 22H2 and 11, x64 | Target audience; deep file-system and Shell access |
| Language / runtime | C# on the current .NET long-term-support release (.NET 10 at the time of writing; confirm and pin in `global.json`) | Mature file APIs, strong tooling, fast startup, familiar to many student and professional developers |
| UI framework | WPF with MVVM | Stable, accessible (UI Automation support), well documented, no packaging constraints. WinUI 3 is a valid later migration because the layers below are UI-free |
| MVVM helpers | CommunityToolkit.Mvvm | Source-generated observable properties and commands |
| Dependency injection / hosting | Microsoft.Extensions.Hosting and DependencyInjection | Standard, testable composition |
| Storage | SQLite via Microsoft.Data.Sqlite (optionally Dapper) | Local, reliable, transactional, no server |
| Logging | Serilog with rolling file sink (local only) | Structured logs, easy path scrubbing |
| File-system abstraction | System.IO.Abstractions (and its TestingHelpers) | Enables fast, safe tests |
| Test frameworks | xUnit, Shouldly (assertions), NSubstitute (mocks), FsCheck (property-based), Verify (snapshots), FlaUI.UIA3 (UI automation), BenchmarkDotNet (performance), coverlet + ReportGenerator (coverage) | Mature and free for commercial use |
| Installer and updates | Velopack (per-user installer with delta updates), or MSIX if a Store release is chosen | No admin rights, simple updates |
| Signing | Code-signing certificate (for example Azure Trusted Signing or an OV/EV certificate) | Reduces SmartScreen and antivirus friction |
| CI | GitHub Actions on `windows-latest` | Free tier, Windows runners |

Check current package versions when starting; pin them in `Directory.Packages.props` (central package management). Do not upgrade major versions without a changelog note.

**Why not continue in Python/tkinter:** weaker screen-reader support, no native look and feel, harder signed installers and background-tray behaviour, and slower to reach professional polish. The prototype remains the behavioural reference (section 9).

---

## 2. Architecture

### 2.1 Layers and dependency rule
```
OneClick.App            WPF shell, views, view-models, design system, tray
   |  depends on
OneClick.Application    use cases (RenameBatch, OrganiseFolder, ScanStorage, ...), interfaces, DTOs
   |  depends on
OneClick.Domain         pure logic: Operation, Batch, PathGuard, NameSanitizer, planners, rule model
   ^  implemented by
OneClick.Infrastructure file system, SQLite journal, registry, watchers, WMI, Shell, settings
```
Rules: Domain references nothing. Application references Domain. Infrastructure references Application and Domain. App references all through dependency injection only. A architecture test (NetArchTest or reflection) shall fail the build if these rules are broken.

### 2.2 Repository layout
```
/src
  OneClick.Domain/
  OneClick.Application/
  OneClick.Infrastructure/
  OneClick.App/
/tests
  OneClick.Domain.Tests/
  OneClick.Application.Tests/
  OneClick.Infrastructure.Tests/     (integration, temp folders)
  OneClick.UiTests/                  (FlaUI scenarios)
  OneClick.Benchmarks/
/docs
  decisions/   (ADRs)  test-plan/  traceability.csv  usability/
/build           (scripts, installer config)
Directory.Build.props  Directory.Packages.props  .editorconfig  global.json
```

### 2.3 Cross-cutting design
- **Async everywhere I/O happens:** `async/await`, `CancellationToken` on every long method, `IProgress<T>` for progress, `System.Threading.Channels` to stream scan results to the UI.
- **Result type:** operations return `Result<T>` with a typed error (`ProtectedPath`, `Locked`, `PermissionDenied`, `NotFound`, `InvalidName`, `Conflict`, `Cancelled`, `Unexpected`); exceptions are caught at the infrastructure boundary and never shown raw to the user.
- **Time and IDs:** inject `TimeProvider` and an ID generator to keep tests deterministic.
- **Configuration:** typed options bound from the settings table; defaults in code.
- **Single instance:** a named mutex prevents two app instances; the second one activates the first.

---

## 3. The Operation Engine (build this first)

### 3.1 Concepts
| Type | Purpose |
|---|---|
| `FileOperation` | One planned action: `Move`, `Rename`, or `Hold` (move into holding area), with source and destination |
| `OperationBatch` | A labelled set of operations from one user action or rule run |
| `IPlanner<TOptions>` | Pure function that turns options and a file listing into a planned batch (no I/O side effects) |
| `PathGuard` | Decides whether a path may be read for modification or written to |
| `ConflictResolver` | Produces unique destination names (" (1)", " (2)") |
| `OperationExecutor` | Runs a batch: journal first, execute, update statuses |
| `UndoService` | Reverses a batch safely |
| `Journal` | SQLite store of batches and operations |
| `HoldingArea` | Moves files to the holding folder, tracks expiry, purges |

### 3.2 Flow
```
Options --Planner--> planned batch --(UI preview, confirm)--> Executor
  Executor: 1) validate every path with PathGuard   2) write batch+operations to journal (status Planned)
            3) mark Running   4) execute each operation, update its status   5) mark Complete / Failed
Undo:     verify each destination still exists and original path is free -> reverse -> mark Undone / PartiallyUndone
Startup:  any batch still Running => offer Complete or Roll back (FR-CORE-12)
```

### 3.3 Key code skeletons
```csharp
public enum OperationType { Move, Rename, Hold }

public sealed record FileOperation(
    OperationType Type, string Source, string Destination, long Size, DateTime ModifiedUtc);

public sealed record OperationBatch(
    Guid Id, string Tool, string Label, DateTime CreatedUtc, IReadOnlyList<FileOperation> Operations);

public interface IPathGuard
{
    // Resolves the path fully (symlinks, 8.3 names, ..) before deciding.
    GuardResult Check(string path, GuardIntent intent); // intent: ReadForChange, WriteTarget, ScanOnly
}

public interface IOperationExecutor
{
    Task<BatchResult> ExecuteAsync(OperationBatch batch, IProgress<BatchProgress> progress, CancellationToken ct);
}

public interface IUndoService
{
    Task<UndoResult> UndoAsync(Guid batchId, IProgress<BatchProgress> progress, CancellationToken ct);
    Task<Guid?> GetLastUndoableBatchAsync();
}
```

**PathGuard rules (implement exactly):**
1. Resolve to a full canonical path (`Path.GetFullPath`, resolve links via `FileSystemInfo.ResolveLinkTarget`); reject if resolution fails.
2. Reject drive roots, the user profile root, and anything equal to or under the protected list (SRS Appendix B), using case-insensitive ordinal comparison and directory-boundary matching (so `C:\Windows2` is not treated as `C:\Windows`).
3. Reject files with System or ReparsePoint attributes; treat Hidden as excluded by default.
4. Reject destinations that resolve outside the allowed root unless the user explicitly chose it.
5. Allow the app's own data folder only for the app's own writes.
Write at least 40 unit tests for PathGuard including traversal (`..\..`), mixed slashes, short 8.3 names, trailing dots, UNC paths, junction escapes, and case differences.

**NameSanitizer:** remove `<>:"/\|?*` and control characters; trim trailing spaces and dots; reject empty results; reject reserved device names (with or without extension, any case); cap names at 255 characters and full paths at 32,000 using the `\\?\` prefix internally while showing normal paths to the user.

**Collision handling:** `ConflictResolver.Unique(destination, existingNames)` checks both the file system and the names already planned in the same batch (otherwise two planned files may target one name). Case-only renames (`a.txt` to `A.txt`) go through a temporary name in two steps.

**Two-phase rename for swaps and cycles (FR-REN-09):** if any destination equals another operation's source in the same batch, first rename the affected sources to unique temporary names (`.oneclick-tmp-<guid>`), journaling both phases, then rename to final names. Undo reverses both phases.

**Executor details:**
- Same-volume move: `File.Move` (atomic rename). Cross-volume: copy to a temporary name at the destination, verify length and last-write time (and SHA-256 when "verify copies" is on), rename temp to final, then delete source. If any step fails, delete the temporary copy and leave the source untouched.
- Catch `IOException` (locked), `UnauthorizedAccessException`, `PathTooLongException`, `FileNotFoundException`; map to typed errors; continue with the next operation.
- Check cancellation between operations; on cancel, mark the batch `Complete` for what ran, with a note, so undo covers it.
- Update each operation row immediately after it runs (commit per operation or in small groups) so a crash leaves an accurate record.

**Undo details:** for each executed operation in reverse order: if destination exists and original path is free, move back; if the original path is occupied, restore to a unique name beside it and report; if the destination is gone, report as "not restorable". Never overwrite. Return a per-item result list for the UI.

**HoldingArea:** held files are moved to `<holding>\<yyyyMMdd-HHmmss>\<name>` and recorded in `HoldingItem`. A purge job (on start-up and daily while running) permanently removes items past retention only after the batch is no longer the "last undoable" action, and logs each removal. "Empty holding area" shows the total size and requires explicit confirmation.

### 3.4 SQLite schema (migration 001)
```sql
PRAGMA journal_mode = WAL;  PRAGMA foreign_keys = ON;
CREATE TABLE schema_info (version INTEGER NOT NULL);
CREATE TABLE batch (
  id TEXT PRIMARY KEY, tool TEXT NOT NULL, label TEXT NOT NULL,
  created_utc TEXT NOT NULL, status TEXT NOT NULL, item_count INTEGER NOT NULL, note TEXT);
CREATE TABLE operation (
  id INTEGER PRIMARY KEY AUTOINCREMENT, batch_id TEXT NOT NULL REFERENCES batch(id) ON DELETE CASCADE,
  seq INTEGER NOT NULL, type TEXT NOT NULL, source TEXT NOT NULL, destination TEXT NOT NULL,
  size INTEGER, modified_utc TEXT, status TEXT NOT NULL, error TEXT);
CREATE INDEX ix_operation_batch ON operation(batch_id, seq);
CREATE TABLE holding_item (
  id INTEGER PRIMARY KEY AUTOINCREMENT, batch_id TEXT, original_path TEXT NOT NULL,
  held_path TEXT NOT NULL, held_utc TEXT NOT NULL, expires_utc TEXT NOT NULL);
CREATE TABLE rule (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, enabled INTEGER NOT NULL, trigger_json TEXT NOT NULL,
  conditions_json TEXT NOT NULL, actions_json TEXT NOT NULL, last_run_utc TEXT, failure_count INTEGER NOT NULL DEFAULT 0);
CREATE TABLE rule_run (
  id INTEGER PRIMARY KEY AUTOINCREMENT, rule_id TEXT NOT NULL REFERENCES rule(id) ON DELETE CASCADE,
  started_utc TEXT NOT NULL, files_affected INTEGER NOT NULL, errors TEXT, dry_run INTEGER NOT NULL);
CREATE TABLE preset (id TEXT PRIMARY KEY, kind TEXT NOT NULL, name TEXT NOT NULL, settings_json TEXT NOT NULL);
CREATE TABLE setting (key TEXT PRIMARY KEY, value TEXT NOT NULL);
```
Migrations are forward-only SQL files embedded as resources, applied in a transaction, with a file copy of the database taken first (`backup-<version>.db`, keep the last 3).

---

## 4. Feature implementation notes

### 4.1 Bulk Rename (FR-REN)
- `RenameOptions` holds an ordered list of `IRenameRule` (FindReplace, Prefix, Suffix, CaseChange, Numbering, RemoveChars, InsertDate). Each rule is a pure function `string Apply(string stem, RenameContext ctx)`.
- Apply rules to the file name without extension; preserve the extension unless the user opts to change it.
- Numbering is assigned after sorting by the chosen key (name, modified, created, size); use natural sort for names (so "file2" precedes "file10").
- The planner returns, per file, `OldName`, `NewName`, and a status: `Changed`, `Unchanged`, `Conflict`, `Invalid`, `Skipped (locked or protected)`. The preview binds directly to this list and virtualises rows for 10,000+ items.
- Debounce option changes by 150 ms and compute previews on a background thread, cancelling stale computations.
- Regex: compile with a timeout (`Regex` match timeout of 1 second) to prevent catastrophic backtracking; show a plain-language message for invalid patterns.

### 4.2 Folder Organiser (FR-ORG)
- A `Classifier` maps each file to a destination sub-folder (by type table, by `yyyy-MM`, or by size band). The type table is data, loaded from settings with the defaults in SRS Appendix C.
- Skip files already inside a matching sub-folder (FR-ORG-06), partial downloads, files newer than 2 minutes, shortcuts (`.lnk`), and anything PathGuard rejects.
- Preview is a tree (folder, count) with an expandable file list; the folder tree is built from the planned batch, not from the disk.
- Presets "Tidy Downloads" and "Tidy Desktop" resolve known-folder locations through `KnownFolders` APIs, not hard-coded paths, so redirected folders and different languages work.

### 4.3 Storage Analyser (FR-STO)
- Scan with `Directory.EnumerateFileSystemEntries` using `EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = ReparsePoint | System, RecurseSubdirectories = false }` and an explicit work queue, so protected folders and links are skipped before descent.
- Run on a background thread at below-normal priority; stream batches of results through a `Channel<ScanEntry>`; keep only the top N largest files in a bounded min-heap (default N = 500) and per-folder size totals in a dictionary keyed by folder path; aggregate category totals as the scan runs. This keeps memory bounded for large drives.
- On HDDs, avoid parallel enumeration of the same physical disk (it slows seeks); use a single enumerator with batched UI updates every 200 ms.
- The status message for disk usage uses `DriveInfo`; thresholds: under 75% healthy, 75 to 89% getting tight, 90% or more almost full.
- Actions go through the Operation Engine (`Hold`) or through the Shell Recycle Bin API (`Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with `RecycleOption.SendToRecycleBin`); Recycle Bin moves are not undoable from the app, and the UI must say so.

### 4.4 Duplicate Finder (FR-DUP)
- Pipeline: group by file size (ignore zero-byte files); within a group, hash the first and last 64 KB; for survivors, compute a full SHA-256 using a streaming read with a 1 MB buffer. Run groups with limited parallelism (2 on HDD, up to 4 on SSD, detected via `DeviceIoControl` seek-penalty query, defaulting to 2 if unknown).
- Never mark all members of a group for removal; the view-model enforces this and the executor re-validates it.

### 4.5 Startup Manager (FR-STA)
- Read `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` and the user Startup folder; read `HKLM\...\Run` read-only.
- Enabled state lives in `...\Explorer\StartupApproved\Run` (and `\StartupFolder`): a binary value whose first byte is `02` (or `06`) for enabled and `03` for disabled, matching Task Manager. Disable by writing the disabled value; enable by writing the enabled value; never delete the original entry.
- Publisher and description come from the executable's version information; resolve the executable path from the command line safely (handle quotes and arguments).
- Advisory labels come from a signed, bundled JSON list; unknown entries show "Unknown" and no recommendation.
- Wrap registry writes in a helper that records previous values in the journal as a special `Registry` batch so it appears in History and can be undone.

### 4.6 Automation Rules (FR-AUT)
- `RuleEngine` evaluates conditions as pure predicates over a `FileFacts` record (name, extension, size, ages, folder), so rules are fully unit-testable.
- Folder watching: `FileSystemWatcher` (Created, Renamed) with an internal buffer increased to 64 KB, events pushed to a debounced queue. A file is processed only when it has existed for at least the minimum age, is not locked (attempt open for read with sharing), is not a partial download, and its size has been stable for 2 consecutive checks 2 seconds apart.
- Handle watcher overflow by running a reconciliation scan of the folder.
- Run as a background service inside the app process with a tray icon (`NotifyIcon` via a lightweight wrapper); closing the window minimises to the tray only if a rule is active and the user agreed; the tray menu offers Open, Pause all, and Exit.
- Safeguards per FR-AUT-05 are enforced inside the rule runner, not the UI.
- Start with Windows: use the per-user Run key (documented, reversible), only after explicit user opt-in.

### 4.7 Guides, Security Checkup, Backup (R2 and R3)
- Guides are content files (Markdown or JSON, localisable) rendered by a simple viewer; each has optional `LaunchAction` entries like `ms-settings:display` opened with `Process.Start` using shell execute, wrapped in try/catch with a fallback instruction.
- Security checkup uses read-only queries: Security Center (`root\SecurityCenter2`, `AntiVirusProduct`), firewall state through the firewall policy COM interface, update information through documented read-only APIs; every probe has a timeout of 3 seconds and a `NotDetectable` outcome that shows a guide. No probe may require elevation.
- Backup (R3): incremental copy using size and last-write comparison, copy to temporary name then rename, never delete at destination, verification pass on a sample or full hash by option, Volume Shadow Copy is out of scope.

---

## 5. UI and HCI implementation

### 5.1 Structure
- `MainWindow` hosts a navigation rail (`ListBox` styled as nav items), a content `ContentControl` bound to the current page view-model (view-model-first navigation with `DataTemplate`s), a status bar, and a notification area.
- One `ShellViewModel` owns navigation, the global Undo command, and the status text. Page view-models receive services through constructor injection and expose `ICommand`s and observable state.
- A reusable `ToolWizardLayout` control implements the five-step flow (UX-01): sections for Choose, Configure, Preview, Apply, Result. Every tool uses it, so behaviour and layout stay consistent.

### 5.2 Design system (build before any feature screen)
Create `Themes/Tokens.xaml`, `Themes/Light.xaml`, `Themes/Dark.xaml`, and `Styles/*.xaml`. All colours, spacing, and fonts come from resources; no literal values in views.

| Token | Light | Dark | Use |
|---|---|---|---|
| `Surface.Window` | #F4F6F9 | #14181F | Window background |
| `Surface.Card` | #FFFFFF | #1C222C | Cards and panels |
| `Surface.Sidebar` | #14213D | #0F1624 | Navigation rail |
| `Text.Primary` | #1B2433 | #E8ECF3 | Body text (contrast above 4.5:1) |
| `Text.Secondary` | #5B6677 | #A9B3C4 | Secondary text (verify 4.5:1) |
| `Accent` | #2F6FED | #5B8DF5 | Primary action, focus |
| `Danger` | #B42318 | #F04438 | Destructive emphasis |
| `Success` | #1E7A4C | #47C28A | Completion |
| `Border` | #DDE2EA | #2B3442 | Dividers |

Typography: Segoe UI Variable (fallback Segoe UI). Scale: Title 24/32 semibold, Heading 16/24 semibold, Body 14/20, Caption 12/16. Spacing on an 8 px grid (4, 8, 16, 24, 32). Corner radius 6. Icons: Segoe Fluent Icons glyphs (monoline); no emoji anywhere in UI, logs shown to users, or documentation.

Components to build and test in a gallery window (`/src/OneClick.App/Gallery`): primary/secondary/destructive buttons, text box, combo box, check box, radio, toggle, data grid with virtualisation, tree view, progress bar, banner (info, warning, error), toast with Undo, dialog, empty state, skeleton loader, key-value row, chart (bar), tooltip.

### 5.3 HCI rules to enforce in code review
1. Primary action is one per screen, named with verb and count, disabled until the preview is valid; show why it is disabled in text next to it.
2. Destructive buttons use Danger styling and are never the default button; Enter does not trigger them.
3. Every list has an empty state with one sentence and one next action.
4. Every long operation shows progress, count, and a Cancel button; the window remains draggable and responsive.
5. After apply, show a result banner: what changed, how many items, any skipped items (expandable), and an Undo button.
6. Error text follows what happened, why, what to do (SRS Appendix D); no stack traces or codes in the main text; a "Details" link reveals technical text and a copy button.
7. Focus order follows visual order; focus is moved to the first meaningful element on page change and to the result banner after apply; focus rectangles are always visible.
8. Set `AutomationProperties.Name`, `HelpText`, and `LiveSetting` for status and result messages; data grids expose row and column headers; icons without text have names.
9. Respect `SystemParameters.ClientAreaAnimation` and high-contrast mode; test at 100%, 150%, 200% scaling and with Windows text size increased.
10. Never rely on colour alone: pair colour with icon or text for states.

### 5.4 Keyboard map
`Ctrl+Z` undo last action (with confirmation text naming it), `Ctrl+F` search the current page, `Ctrl+Enter` apply, `Esc` cancel or close dialog, `F1` help for the page, `Ctrl+1..9` jump to navigation items, `F5` refresh preview.

### 5.5 Localisation
Use `.resx` resources with a generated strongly typed class; format counts with plural-aware resource keys (for example `FilesRenamed_One`, `FilesRenamed_Many`); run a pseudo-locale test that expands strings by 40% to check layout.

---

## 6. Testing and QA

### 6.1 Strategy
Many fast unit tests, fewer integration tests on real temporary folders, a small set of UI scenarios, plus targeted non-functional suites. The engine is the highest-risk component and receives the deepest testing.

### 6.2 Engine invariants (property-based with FsCheck; run at least 1,000 cases in CI, 10,000 nightly)
1. **Round trip:** for any generated file tree and any valid batch, `Undo(Apply(batch))` restores the identical set of paths and byte contents.
2. **No loss:** the multiset of file contents before and after any apply equals the multiset after (moves and renames never change content; Hold keeps content in the holding area).
3. **No overwrite:** no operation ever replaces an existing file.
4. **Protected safety:** no generated path in a protected location is ever modified.
5. **Idempotent preview:** planning twice with the same inputs yields the same plan.
6. **Crash consistency:** killing the executor at any operation boundary leaves a journal that recovery can complete or roll back to a consistent state.

### 6.3 Fault injection (infrastructure tests with a failing file-system wrapper)
Locked file; permission denied; path too long; disk full during cross-volume copy; destination deleted mid-batch; source modified mid-batch; process killed between journal write and move; read-only attribute; antivirus-style transient lock (retry once then skip); network or removable drive disappearing; clock changes.

### 6.4 Test catalogue (minimum set; extend per feature)
| ID | Area | Case | Expected | Req |
|---|---|---|---|---|
| TC-CORE-001 | Engine | Apply then undo 500 mixed files | Identical tree and contents | FR-CORE-05 |
| TC-CORE-002 | Engine | Two files planned to the same name | Second gets " (1)" | FR-CORE-07 |
| TC-CORE-003 | Engine | Target in Windows directory | Rejected, nothing changed | FR-CORE-06 |
| TC-CORE-004 | Engine | Junction pointing into protected folder | Not followed, reported | FR-CORE-08 |
| TC-CORE-005 | Engine | One locked file in batch of 20 | 19 done, 1 reported, undo restores 19 | FR-CORE-09 |
| TC-CORE-006 | Engine | Kill process mid-batch, restart | Recovery prompt; both options reach consistent state | FR-CORE-12 |
| TC-CORE-007 | Engine | Cross-volume move with disk full | Source intact, temp copy removed | FR-CORE-13 |
| TC-CORE-008 | Engine | Name "CON.txt", "a.", "a ", 300-char name | Rejected or sanitised with message | FR-CORE-14 |
| TC-CORE-009 | Engine | Partial download `x.crdownload` in auto mode | Excluded | FR-CORE-15 |
| TC-REN-001 | Rename | Prefix + numbering, 120 files | Names correct, natural order | FR-REN-02 |
| TC-REN-002 | Rename | Swap a.txt and b.txt names | Both swapped, no loss | FR-REN-09 |
| TC-REN-003 | Rename | Case-only change a.txt to A.txt | Succeeds | FR-CORE-14 |
| TC-REN-004 | Rename | Invalid regex | Plain message, no crash | FR-REN-06 |
| TC-REN-005 | Rename | Preview of 1,000 files | Update under 300 ms | NFR-PERF-02 |
| TC-ORG-001 | Organise | Tidy Downloads with mixed types | Correct folders and counts | FR-ORG-01 |
| TC-ORG-002 | Organise | Re-run on organised folder | Zero operations | FR-ORG-06 |
| TC-ORG-003 | Organise | Shortcut and new file present | Excluded and flagged | FR-ORG-04 |
| TC-STO-001 | Storage | Scan 500,000-file synthetic tree | Memory under 400 MB, first rows under 2 s | NFR-PERF-03 |
| TC-STO-002 | Storage | Access-denied subfolder | Scan completes, folder noted | FR-STO-08 |
| TC-STO-003 | Storage | Cancel at 50% | Stops within 1 s, results kept | FR-CORE-10 |
| TC-DUP-001 | Duplicates | Same content different names; same name different content | Grouped by content only | FR-DUP-01 |
| TC-DUP-002 | Duplicates | Attempt to remove all copies | Blocked | FR-DUP-03 |
| TC-STA-001 | Startup | Disable then enable an entry | Matches Task Manager state both times | FR-STA-03 |
| TC-AUT-001 | Automation | Rule on new PDF | Moved after minimum age | FR-AUT-01 |
| TC-AUT-002 | Automation | Rule fails 5 times | Rule disabled, user notified | FR-AUT-05 |
| TC-AUT-003 | Automation | 1,000 files arrive at once | Per-run limit enforced | FR-AUT-05 |
| TC-HIS-001 | History | Undo older batch with later conflict | Blocked with explanation | FR-HIS-03 |
| TC-SEC-001 | Security | Traversal strings in every path input | Rejected | NFR-SEC-02 |
| TC-PRIV-001 | Privacy | Run all features with network monitor | No traffic except optional update check | NFR-PRIV-01 |
| TC-ACC-001 | Accessibility | Complete UC-01 using keyboard and Narrator only | Task completed, all controls announced | NFR-ACC-01 |
| TC-INST-001 | Install | Install, update, uninstall on clean Windows 10 and 11 | No admin prompt; user files untouched | NFR-INST-01 |

### 6.5 UI automation scenarios (FlaUI)
Implement the six use cases (SRS section 8) as end-to-end scenarios against a sandbox folder created per test: UC-01 to UC-06. Each scenario asserts visible text (counts), file-system results, and that Undo restores the start state. Run on every pull request for UC-01 to UC-04, nightly for the rest.

### 6.6 Performance and soak
BenchmarkDotNet suites for planners (10,000 files), PathGuard (1 million checks), scan throughput, and hashing; budgets from NFR-PERF fail the nightly build when exceeded by more than 15%. Soak test: automation engine running with synthetic file arrivals for 8 hours; memory growth under 10%, no handle leaks.

### 6.7 Security and privacy testing
Dependency vulnerability scan (`dotnet list package --vulnerable`) on every build; static analysis with Roslyn analyzers and security rules; fuzz path inputs (random Unicode, long strings, device names); verify no secrets in the repository (secret scanner); verify the installer and binaries are signed; run the app with outbound network blocked and confirm full function; inspect exported logs for personal data.

### 6.8 Accessibility testing
Automated checks with Accessibility Insights for Windows on every screen; manual keyboard-only pass; screen-reader pass with Narrator and NVDA for UC-01 to UC-04; contrast verification of every token pair in both themes; high-contrast theme pass; 200% scaling pass. Record results in `docs/test-plan/accessibility.md`.

### 6.9 Usability testing protocol
- **Participants:** 5 to 8 per round, representative of Mwila and Chola personas; at least half with low computer confidence.
- **Method:** moderated, think-aloud, 30 to 40 minutes, on the participants' type of hardware, using a safe sandbox folder.
- **Tasks:** rename 50 photos; tidy a messy Downloads folder; free space on a nearly full drive; undo a mistake; (R2) auto-sort new downloads.
- **Measures:** task success (unaided, with help, failed), time on task, errors, confusion points, and SUS after the session.
- **Output:** a findings report with severity and recommended fixes; fixes are tracked as defects. Repeat each round after fixes until targets in SRS 2.2 are met.
- Also run a heuristic evaluation (Nielsen's 10) by two reviewers before each usability round.

### 6.10 Exploratory test charters (QA)
Each 60 to 90 minutes, notes recorded: "Explore Rename with hostile file names (emoji, right-to-left text, very long names)", "Explore Organise on a synced OneDrive folder", "Explore undo after moving files manually in Explorer", "Explore low-disk and low-memory conditions", "Explore sign-out and shutdown during a batch", "Explore two instances and rapid repeated clicks", "Explore system language set to a non-English language and a high scaling factor".

### 6.11 Defect handling
Severities per SRS section 9. Each defect has steps, expected and actual results, build number, logs, and a linked requirement ID. S1 and S2 defects block release; every fixed S1 or S2 gets a regression test added before closure. Maintain a regression suite tagged `Regression`, run on every pull request.

### 6.12 Release checklist
Requirement traceability complete and green for Must items; coverage thresholds met; performance and soak passed; accessibility and usability results filed; installer and update tested on clean machines (Windows 10, Windows 11, 4 GB RAM HDD device); signature verified; changelog and user guide updated; known issues documented; backup of release artefacts stored; rollback plan for updates confirmed.

---

## 7. Build, CI/CD, and release

- **Branching:** trunk-based with short-lived feature branches; pull requests require passing CI and review (the agent also performs a self-review checklist using section 0.3).
- **Quality gates in `Directory.Build.props`:** `Nullable` enabled, `TreatWarningsAsErrors` true, `AnalysisLevel` latest, `EnforceCodeStyleInBuild` true, `.editorconfig` enforced.
- **CI workflow (`.github/workflows/ci.yml`):** on pull request and main: checkout; set up .NET from `global.json`; restore; build Release; run `dotnet format --verify-no-changes`; run tests with coverage; fail under thresholds; run vulnerable-package check; publish test and coverage reports; run UI scenarios (subset) on the Windows runner; upload artefacts.
- **Nightly workflow:** extended property tests (10,000 cases), full UI scenarios, benchmarks, soak test (shortened), dependency update checks.
- **Release workflow (tagged `v*`):** build self-contained, trimmed-safe x64 package (avoid trimming that breaks reflection; test the published output); package with Velopack; sign; produce release notes from `CHANGELOG.md`; publish to the update channel and the download page.
- **Versioning:** Semantic Versioning; `0.x` until R2; database schema version recorded and tested for upgrade from every previous release.
- **Updates:** optional check against a static JSON on HTTPS; downloads verified by signature and hash; user always chooses when to apply.

---

## 8. Phased build plan

Each phase lists tasks and an acceptance gate. Do not start a phase until the previous gate passes.

### Phase 0: Foundations (about 2 days)
1. Create the repository, solution, projects, central package management, `.editorconfig`, analyzers, `global.json`.
2. Add CI (build, test, format, coverage) and an architecture test enforcing the dependency rule.
3. Add `README`, `CHANGELOG`, ADR template, `docs/traceability.csv` seeded with every SRS requirement ID.
4. Create the dependency-injection host, logging, settings service, and an empty WPF window that launches.

*Gate:* CI green on an empty solution; architecture test passes; app starts.

### Phase 1: Operation Engine (about 1.5 weeks)
1. `PathGuard`, `NameSanitizer`, `ConflictResolver` with full unit tests (TC-CORE-003, 004, 008).
2. SQLite journal, migrations, repositories.
3. `OperationExecutor` including cross-volume copy-verify-delete and two-phase rename.
4. `UndoService`, crash recovery, `HoldingArea` with retention.
5. Property-based and fault-injection suites (section 6.2, 6.3).

*Gate:* TC-CORE-001 to 009 pass; engine coverage at least 90%; 10,000-case property run clean; no S1 defects.

### Phase 2: Shell and design system (about 1 week)
1. Tokens, themes (light, dark), all base styles and controls, component gallery.
2. Main window, navigation rail, status bar, toast with Undo, dialogs, empty/loading/error states, `ToolWizardLayout`.
3. First-run introduction (3 screens) and Settings page (FR-SET-01, 02, 04).
4. Accessibility baseline: automation names, focus order, keyboard map, high-contrast check.

*Gate:* gallery reviewed against section 5.3; Accessibility Insights clean on shell; both themes pass contrast checks.

### Phase 3: Bulk Rename (about 1 week)
1. Rule classes and planner with tests; live preview with conflict highlighting; virtualised list.
2. View-model and screen using `ToolWizardLayout`; apply through the engine; result banner with Undo.
3. UC-01 automation scenario; usability pilot with 3 participants.

*Gate:* TC-REN-001 to 005 pass; UC-01 scenario green; pilot task success at least 80%.

### Phase 4: Folder Organiser (about 1 week)
1. Classifier and planner; folder-tree preview; presets for Downloads and Desktop with known-folder resolution.
2. Screen, apply, result; UC-02 scenario.

*Gate:* TC-ORG-001 to 003 pass; UC-02 scenario green.

### Phase 5: Storage Analyser and Overview (about 1.5 weeks)
1. Streaming scanner with bounded memory; results grid; category chart; filters.
2. Disk-usage summary and plain-language status on the Overview page; actions via the engine; Empty holding area.
3. UC-03 scenario; performance benchmarks.

*Gate:* TC-STO-001 to 003 pass; NFR-PERF-01 to 04 met on the reference low-end device.

### Phase 6: History and Undo (about 3 days)
1. History list, item details, undo with conflict explanation, global undo shortcut, CSV export.
2. UC-04 scenario.

*Gate:* TC-HIS-001 passes; UC-04 green.

### Phase 7: R1 hardening and release (about 1.5 weeks)
1. Full regression, exploratory charters, accessibility and usability round (SRS gate: task success at least 85%).
2. Installer, signing, update check, clean-machine install tests, diagnostic log export, About page.
3. Documentation: user guide, privacy statement, release notes.

*Gate:* all SRS section 9 release gates met; R1 tagged.

### R2 phases (each follows the same loop: planner and tests, view-model, screen, scenario, gate)
Phase 8 Duplicate Finder; Phase 9 Startup Manager; Phase 10 Automation Rules and tray (the largest and riskiest, allow 3 weeks, soak test required); Phase 11 Guides and Quick Actions; Phase 12 Security Checkup; Phase 13 presets, advanced rename (regex, dates), auto-update; Phase 14 R2 hardening and a second usability round (SUS at least 80).

### R3
Backup Helper, treemap, ARM64, localisation.

---

## 9. Migrating from the prototype (`one_click.py`)

| Prototype element | Production equivalent |
|---|---|
| `Journal.apply` / `undo` (list of moves) | `OperationExecutor` / `UndoService` with SQLite journal and write-ahead logging |
| `is_protected` | `PathGuard` with the full rules in section 3.3 |
| `unique()` | `ConflictResolver` (also considers names planned in the same batch) |
| `plan_rename` | `RenamePlanner` with an ordered rule list and natural sorting |
| `plan_organise` and `CATS` | `OrganisePlanner` and editable category table |
| `scan_files` (sorts everything, keeps top 300) | Streaming scanner with a bounded heap and per-folder totals |
| Holding folder under `~/.oneclick` | `HoldingArea` with retention and purge |
| Tkinter pages | WPF views using `ToolWizardLayout` |

Port the behaviours and the preview-confirm-undo flow, not the code. Use the prototype's observable behaviour as acceptance examples for the first tests.

---

## 10. Review prompts (use after each phase)

**As tester:** "Review the code and tests for Phase N against the SRS. List untested requirements, missing edge cases, and any path where a user file could be lost. Write the missing tests."

**As HCI designer:** "Review every screen built in Phase N against SRS section 5 and implementation section 5.3. List violations of heuristics, accessibility, language, and consistency, with fixes. Produce a one-page findings table with severity."

**As QA lead:** "Update the traceability sheet. Run the release checklist items that apply to this phase. Report coverage, open defects by severity, and whether the phase gate is met."

**As security reviewer:** "Check all path inputs, registry writes, and process launches added in Phase N for traversal, escape, injection, and elevation issues. List findings and add tests."

---

## 11. Documentation to maintain
`README.md` (build, run, test), `CHANGELOG.md`, `docs/decisions/` (one ADR per significant choice), `docs/traceability.csv`, `docs/test-plan/` (strategy, accessibility, performance results), `docs/usability/` (protocols, findings, SUS scores), a user guide with screenshots, a privacy statement (no telemetry, no file data leaves the device), and a support runbook (how to export diagnostics, how recovery works).
