# One Click: Software Requirements Specification

| Field | Value |
|---|---|
| Product | One Click, a Windows desktop utility toolkit |
| Document | Software Requirements Specification (SRS) |
| Version | 1.1 (adds the prioritised post-MVP features; R1 scope unchanged) |
| Date | 8 October 2026 |
| Product owner | [your name] |
| Companion document | OneClick_Implementation.md |
| Standards followed | ISO/IEC/IEEE 29148 (requirements), WCAG 2.1 AA (accessibility), Nielsen's 10 usability heuristics, ISO/IEC 25010 (quality model) |

Conventions: "shall" means mandatory. Every requirement has a unique ID. Priority uses MoSCoW (M = Must, S = Should, C = Could, W = Won't for now). Release uses R1 (MVP), R2 (version 1.0), R3 (later).

---

## 1. Introduction

### 1.1 Purpose
This document defines what One Click must do and how well it must do it. It is the single source of truth for developers, testers, UX designers, and QA. Anything not specified here is out of scope until added through change control (section 12).

### 1.2 Product vision
Most computer users repeat the same tasks the long way and tolerate problems they do not know are fixable. One Click lets ordinary people do hard-to-know tasks in a few clicks and automates the chores they repeat (renaming, sorting, cleaning, backing up), without ever risking their files.

### 1.3 Scope
**In scope:** Windows desktop app; file tools (rename, organise, storage analysis, duplicates); smart search; startup manager; performance snapshot and slow-computer wizard; battery report; automation rules and rule templates; backup helper; laptop migration checklist; guides and quick actions; security checkup (read-only); Explorer context-menu shortcuts; history and undo; settings and onboarding; portable edition; command-line interface.

**Out of scope (this specification):** macOS and Linux versions, mobile apps, cloud accounts or sync, registry cleaners, driver updaters, antivirus features, system optimisation that modifies Windows system files, any feature that needs administrator rights, and AI features.

### 1.4 Definitions
| Term | Meaning |
|---|---|
| Batch | A group of file operations applied together and undone together |
| Journal | The persistent record of every batch, used for undo and crash recovery |
| Holding area | An app-managed folder where "deleted" files wait before permanent removal |
| Preview | The on-screen list of exactly what will change, shown before anything changes |
| Protected location | A path One Click never modifies (Appendix B) |
| Rule | An automation definition: trigger, conditions, actions |
| Dry run | Executing a rule or batch without changing anything, only reporting results |

### 1.5 Product principles
Every design and implementation decision shall be checked against these six principles. A conflict between a feature and a principle is resolved in favour of the principle.

1. **Never lose data.** No operation may permanently destroy a user file without an explicit, separate, confirmed step.
2. **Preview before change.** The user sees exactly what will happen first.
3. **Everything is undoable** unless the user empties the holding area.
4. **Plain language.** No jargon without an explanation; no fear-based messaging.
5. **Local and private.** Works offline; file names and contents never leave the computer.
6. **Least privilege.** Runs as a standard user and never asks for administrator rights.

---

## 2. Overall description

### 2.1 Product perspective
One Click is a standalone, installable, offline-first Windows application. It replaces a set of separate tools (bulk rename utilities, storage analysers, startup managers, folder-sorting scripts) with one guided interface. A working prototype (v0.1, Python/tkinter) validates the core flow: choose, configure, preview, apply, undo.

### 2.2 Goals and success measures
| Goal | Measure | Target |
|---|---|---|
| Users complete core tasks unaided | Task success rate in moderated tests | at least 90% |
| Tasks are fast | Median time: rename 50 files / tidy Downloads | under 60 s / under 45 s |
| Interface is easy to learn | System Usability Scale (SUS) | at least 80 |
| App is trusted | Data-loss defects in release | 0 |
| App is stable | Crash-free sessions | at least 99.5% |
| App is light | Idle memory / cold start on a 4 GB laptop | under 120 MB / under 2 s |

### 2.3 User classes and personas
| Persona | Profile | Needs | Design implications |
|---|---|---|---|
| **Mwila, the student** (primary) | Shares a mid-range laptop, 4 GB RAM, storage always nearly full, messy Downloads, little technical vocabulary | Free space fast, find and sort files, avoid breaking anything | Plain words, large safe defaults, strong undo |
| **Chola, the office administrator** | Handles hundreds of documents weekly, renames and sorts by hand, uses a shared PC | Repeatable batch rename, rules that run automatically | Presets, saved rules, history log |
| **Tendai, the confident power user** | Knows the basics, dislikes slow wizards | Speed, keyboard control, regex rename, dry-run | Shortcuts, advanced panels collapsed by default |

Mwila is the design baseline: if Mwila can use a feature unaided, it ships.

### 2.4 Operating environment
- Windows 10 version 22H2 and Windows 11, 64-bit (ARM64 in R3).
- Minimum hardware: dual-core CPU, 4 GB RAM, 300 MB free disk; hard disk drives (HDD) are common and must perform acceptably.
- Screen: 1366x768 minimum, 100% to 200% scaling, mouse, keyboard, and touch-capable laptops.
- No internet required for any feature except the optional update check.

### 2.5 Constraints
- Runs without administrator elevation. Features needing it are shown read-only with an explanation, or omitted.
- Must not modify the Windows registry outside the user's own hive and only through documented reversible mechanisms (Startup Manager).
- All text externalised for localisation; English first, designed for later languages (including right-to-left layout readiness).
- Distribution outside the Microsoft Store initially; installer shall be code-signed.

### 2.6 Assumptions and dependencies
- Users have a standard user account on NTFS volumes; FAT32/exFAT drives are supported with reduced features (no long-path guarantees).
- Cloud-sync folders (OneDrive and similar) may hold placeholder files; the app treats cloud-only placeholders as read-only and skips them.
- Windows Settings URIs (`ms-settings:`) are used for deep links and may change between Windows versions; each use shall fail gracefully.

---

## 3. System features and functional requirements

### 3.1 Core engine behaviour (FR-CORE)
These apply to every tool that changes files.

| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-CORE-01 | All file changes shall pass through a single Operation Engine; no feature may modify files directly. | M | R1 |
| FR-CORE-02 | Before any change, the app shall show a preview listing each affected item and its result, with counts. | M | R1 |
| FR-CORE-03 | The apply button shall name the action and the count (for example "Rename 48 files"); a confirmation shall show the count and a sample of items. | M | R1 |
| FR-CORE-04 | Every batch shall be written to the journal before execution begins and marked complete or failed afterwards (write-ahead). | M | R1 |
| FR-CORE-05 | Any completed batch shall be undoable; if some items cannot be restored, the app shall list them with reasons and restore the rest. | M | R1 |
| FR-CORE-06 | Protected locations (Appendix B) shall never be read for modification, scanned for cleanup, or written to, regardless of user input. | M | R1 |
| FR-CORE-07 | A name collision shall never overwrite a file; the engine shall add a numeric suffix such as "report (1).pdf". | M | R1 |
| FR-CORE-08 | Hidden files, system files, symbolic links, and junctions shall be excluded by default and never followed. | M | R1 |
| FR-CORE-09 | Files that are locked or in use shall be skipped and reported; they shall not stop the batch. | M | R1 |
| FR-CORE-10 | Long operations shall run off the UI thread, show progress with item counts, and be cancellable; cancelling shall leave a consistent, undoable state. | M | R1 |
| FR-CORE-11 | "Delete" shall mean moving to the holding area (default retention 30 days, configurable 1 to 90). Moving to the Recycle Bin shall be an option. Permanent deletion shall occur only through "Empty holding area" with a separate confirmation. | M | R1 |
| FR-CORE-12 | On start-up, the app shall detect incomplete batches and offer to finish or roll them back. | M | R1 |
| FR-CORE-13 | Moves across drives shall use copy, verify (size and timestamp; hash when "verify copies" is enabled), then delete the source. | M | R1 |
| FR-CORE-14 | The engine shall handle invalid characters, reserved names (CON, PRN, AUX, NUL, COM1 to COM9, LPT1 to LPT9), trailing spaces and dots, paths over 260 characters, and case-only renames. | M | R1 |
| FR-CORE-15 | Files modified within the last 2 minutes, and partial downloads (`.crdownload`, `.part`, `.tmp`, `.download`), shall be excluded from automatic operations and flagged in manual ones. | S | R1 |
| FR-CORE-16 | A dry-run mode shall be available for every batch and every automation rule. | S | R2 |

### 3.2 Bulk Rename (FR-REN)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-REN-01 | The user shall choose a folder or a selection of files; a subfolder option shall be off by default. | M | R1 |
| FR-REN-02 | Rules shall include: find and replace (case-sensitive option), add prefix, add suffix, change case (lower, UPPER, Title, Sentence), add sequence number (start, step, padding, position), remove characters (spaces, digits, symbols), replace spaces with a chosen character. | M | R1 |
| FR-REN-03 | Rules shall combine and apply in a visible, reorderable sequence. | S | R2 |
| FR-REN-04 | The preview shall update live (within 300 ms for 1,000 files) and highlight conflicts, invalid names, and unchanged files. | M | R1 |
| FR-REN-05 | Numbering order shall be selectable: by name, date modified, date created, or size. | S | R1 |
| FR-REN-06 | Advanced users shall be able to use regular expressions with a plain-language error when the pattern is invalid. | S | R2 |
| FR-REN-07 | The tool shall insert dates (modified, created) with a chosen format; photo "date taken" shall be supported from image metadata. | C | R2 |
| FR-REN-08 | Rename presets shall be saveable, named, and reusable. | S | R2 |
| FR-REN-09 | Renames that swap or cycle names (a to b, b to a) shall complete correctly using a two-phase strategy. | M | R1 |
| FR-REN-10 | A filter by extension or name pattern shall limit which files are renamed. | S | R1 |

### 3.3 Folder Organiser (FR-ORG)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-ORG-01 | The user shall group files by type, by month and year modified, or by size band. | M | R1 |
| FR-ORG-02 | File-type categories shall be defined by an editable table (Appendix C); users may add or change extensions. | S | R2 |
| FR-ORG-03 | The preview shall show a folder tree of the result with file counts per new folder. | M | R1 |
| FR-ORG-04 | One-click presets "Tidy Downloads" and "Tidy Desktop" shall use safe defaults and never move shortcuts or installed-program files. | M | R1 |
| FR-ORG-05 | The destination shall default to sub-folders inside the chosen folder; the user may choose another root. | S | R1 |
| FR-ORG-06 | Files already in a correctly named sub-folder shall not be moved again. | S | R1 |

### 3.4 Storage Analyser (FR-STO)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-STO-01 | The app shall show used and free space for each local drive with a plain-language status (healthy, getting tight, almost full at 90% or more) and one sentence on why a full drive slows the computer. | M | R1 |
| FR-STO-02 | The user shall scan a folder; results shall stream in progressively, and the scan shall be cancellable. | M | R1 |
| FR-STO-03 | Results shall list the largest files with size, age, type, and location, and be filterable by minimum size, age, and type. | M | R1 |
| FR-STO-04 | A summary by category (video, images, documents, installers, archives, other) shall be shown as a chart and as text. | S | R1 |
| FR-STO-05 | A view of the largest folders (tree or treemap) shall be provided. | S | R2 |
| FR-STO-06 | "Safe to clean" suggestions shall be limited to user-level items: the user temp folder, installers in Downloads, files in Downloads older than a chosen age, and the Recycle Bin size. Each suggestion shall explain what it is. | S | R2 |
| FR-STO-07 | Actions per file: open, show in Explorer, move to holding area, send to Recycle Bin. | M | R1 |
| FR-STO-08 | The scan shall not follow links, shall skip protected locations, and shall tolerate permission-denied errors without failing. | M | R1 |

### 3.5 Duplicate Finder (FR-DUP)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-DUP-01 | Duplicates shall be detected by size, then a partial hash, then a full cryptographic hash; names shall not be trusted. | M | R2 |
| FR-DUP-02 | Groups shall show all copies with paths and dates; one copy per group shall be preselected as "keep" using a stated rule (oldest, newest, or shortest path). | M | R2 |
| FR-DUP-03 | The app shall never allow all copies in a group to be removed. | M | R2 |
| FR-DUP-04 | Removing duplicates shall use the holding area and be undoable. | M | R2 |

### 3.6 Startup Manager (FR-STA)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-STA-01 | The app shall list startup programs from the current user's Run key, the user Startup folder, and (read-only) the machine-wide Run key. | M | R2 |
| FR-STA-02 | Each entry shall show name, publisher, path, source, and enabled state, and a plain-language description where known. | M | R2 |
| FR-STA-03 | The user shall enable or disable entries using Windows' own StartupApproved mechanism, so the change matches Task Manager and is reversible. | M | R2 |
| FR-STA-04 | Entries shall carry advisory labels ("Usually safe to turn off", "Keep on", "Unknown"), clearly marked as advice, from a local curated list. | S | R2 |
| FR-STA-05 | The app shall not delete startup entries and shall not touch machine-wide entries. | M | R2 |

### 3.7 Automation Rules (FR-AUT)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-AUT-01 | A rule shall consist of a trigger (new file in a folder, schedule, or sign-in), conditions (extension, name contains or matches, size, age), and actions (move, rename using FR-REN rules, move to holding area). | M | R2 |
| FR-AUT-02 | The rule builder shall be sentence-based ("When a PDF arrives in Downloads, move it to Documents/PDFs") with ready-made templates. | M | R2 |
| FR-AUT-03 | Every rule shall support dry run and shall show what it would have done on existing files. | M | R2 |
| FR-AUT-04 | Rules shall run in the background with a tray icon, pause-all control, and optional start with Windows (off by default, asked once). | M | R2 |
| FR-AUT-05 | Safeguards: a per-run file limit (default 200), a minimum file age (default 60 seconds), automatic exclusion of partial downloads, and automatic disabling of a rule that fails 5 times in a row. | M | R2 |
| FR-AUT-06 | Automatic runs shall be journaled as batches and be undoable. | M | R2 |
| FR-AUT-07 | A run history per rule shall show time, files affected, and errors. | S | R2 |
| FR-AUT-08 | A template library shall provide at least 10 ready-made rules, each with a plain description and an example of the result. Minimum set: screenshots to a Screenshots folder; receipts and invoices (PDF) into month folders; installers older than 30 days to the holding area; images into Pictures by month; documents by type; archives into an Archives folder; large videos to a "Videos to review" folder; downloads older than 60 days to the holding area; phone photos by month; Office files by type. | S | R2 |
| FR-AUT-09 | Every template shall open in the rule builder for editing (folders, ages, destinations) and shall always start with a dry run before it can be enabled. | M | R2 |
| FR-AUT-10 | Rules and presets shall be exportable and importable as versioned files. Imported rules shall be validated with the same checks as new rules (protected locations, limits), shall arrive disabled, and shall be rejected with a plain explanation if invalid or from an unknown newer version. | S | R2 |
| FR-AUT-11 | Templates shall support parameters (folder, minimum age, destination root) shown as plain fill-in fields. | S | R2 |
| FR-AUT-12 | A scheduled "Tidy Downloads" proposal shall prepare a dry-run result on a chosen schedule and notify the user; nothing shall change until the user approves the batch. | C | R3 |

### 3.8 Backup Helper (FR-BAK)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-BAK-01 | The user shall choose source folders (suggestions: Documents, Pictures, Desktop) and a destination (external drive or a cloud-sync folder). | S | R3 |
| FR-BAK-02 | Backups shall copy new and changed files only, shall never delete files at the destination by default, and shall verify copies. | S | R3 |
| FR-BAK-03 | A schedule (daily, weekly, when the drive is connected) and a visible "last successful backup" status with reminders shall be provided. | S | R3 |
| FR-BAK-04 | A plain-language restore guide shall be included. | S | R3 |

### 3.9 Quick Actions and Guides (FR-QAC)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-QAC-01 | The app shall provide searchable step-by-step guides for common tasks (take a screenshot and find it, record the screen, connect a printer, use a second monitor or projector, type special characters, check battery usage, free up space). | S | R2 |
| FR-QAC-02 | Guides shall include a button that opens the relevant Windows Settings page or tool through a documented URI, with a fallback instruction if it fails. | S | R2 |
| FR-QAC-03 | A special-characters panel shall copy accented letters, symbols, and currency signs to the clipboard in one click. | C | R2 |
| FR-QAC-04 | A "Phone and laptop" guide shall explain, step by step, how to move files between a phone and a laptop by USB cable (file-transfer mode), Bluetooth, cloud storage, and a shared link, with separate Android and iPhone instructions and buttons that open the relevant Windows settings. | S | R2 |
| FR-QAC-05 | Guide content shall carry a "last reviewed" date and the Windows version it was written for, and shall be reviewed after each major Windows release. | C | R2 |

### 3.10 Security Checkup (FR-SEC)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-SEC-01 | A read-only checkup shall report, where detectable without administrator rights: Windows Update status, security software status, firewall status, and disk-encryption status or guidance. | S | R2 |
| FR-SEC-02 | Items that cannot be detected shall show a guide, not a guess. | M | R2 |
| FR-SEC-03 | The checkup shall include educational checklists for 2-factor authentication and password managers; the app shall never see, store, or request passwords. | S | R2 |
| FR-SEC-04 | The wording shall be calm and factual; the app shall never claim the computer is infected or "at risk" without evidence. | M | R2 |

### 3.11 History and Undo (FR-HIS)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-HIS-01 | A History view shall list batches with date, tool, item count, and status, and allow viewing item-level details. | M | R1 |
| FR-HIS-02 | A global "Undo last action" shall always be reachable, and shall state what it will undo. | M | R1 |
| FR-HIS-03 | Older batches shall be undoable only if later batches do not conflict; otherwise the app shall explain which batch must be undone first. | M | R1 |
| FR-HIS-04 | History shall be exportable to CSV and kept 90 days by default (configurable). | S | R2 |

### 3.12 Settings, onboarding, and help (FR-SET)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-SET-01 | A first-run introduction (maximum 3 short screens) shall explain preview, undo, and the holding area, and can be skipped and reopened. | M | R1 |
| FR-SET-02 | Settings: theme (light, dark, system), holding-area location and retention, confirmation options, journal retention, update check on or off, crash reports (opt-in, off by default), keyboard shortcuts list, reset to defaults. | M | R1 |
| FR-SET-03 | Each tool shall have a short "How this works and why it is safe" explanation. | S | R1 |
| FR-SET-04 | The app shall provide an About page with version, licences of third-party components, and a log-export button for support. | M | R1 |

### 3.13 Smart Search (FR-FIND)
Read-only. Finds files the user saved but cannot locate.

| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-FIND-01 | The user shall search by file name (part of a name, wildcards, and optionally a regular expression) within a chosen scope; the default scope is the user's own folders (Documents, Desktop, Downloads, Pictures, Videos, Music). | M | R2 |
| FR-FIND-02 | Filters shall include file category, date modified or created (for example "today", "this week", "last 30 days", or a custom range), size range, and folder. | M | R2 |
| FR-FIND-03 | Ready-made searches shall be offered: "Saved this week", "Largest files", "Recent downloads", "Photos from last month". | S | R2 |
| FR-FIND-04 | Results shall stream in as found, show name, location, size, and date, and support: open, show in Explorer, copy path, and "send to" Rename or Organise with the selected files. | M | R2 |
| FR-FIND-05 | The search shall use the Windows Search index when it is available and complete for the scope, and shall otherwise fall back to its own bounded scan; the user shall not need to know which is used. | S | R2 |
| FR-FIND-06 | A "Recent files" view shall list files created or changed in the last N days (default 7). | S | R2 |
| FR-FIND-07 | Search shall use names and file properties only. Searching inside file contents is not included. | M | R2 |
| FR-FIND-08 | Searches shall exclude protected locations (Appendix B), hidden and system files, and links, unless the user turns on "include hidden files" for user folders. | M | R2 |
| FR-FIND-09 | Frequently used searches shall be saveable and nameable. | C | R3 |

### 3.14 Performance Snapshot and Slow-Computer Wizard (FR-PERF)
Mostly read-only. The only action that changes anything is closing a program the user chooses.

| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-PERF-01 | A snapshot shall show processor, memory, and disk activity with plain-language labels (for example "Memory is 88% used. The computer may slow down."), refreshed every 2 seconds only while the page is visible. | M | R2 |
| FR-PERF-02 | The snapshot shall list the top 5 programs by memory and by processor use, grouping a program's helper processes under one friendly name and icon. | M | R2 |
| FR-PERF-03 | Short explanations shall teach the difference between memory (RAM), storage, and the processor, shown beside the relevant reading. | S | R2 |
| FR-PERF-04 | For programs started by the current user, the user may choose "Close this program"; the app shall request a normal close, warn that unsaved work may be lost, and offer a forced close only after a second confirmation. | S | R2 |
| FR-PERF-05 | The app shall never offer to close system processes, security software, other users' processes, or itself. | M | R2 |
| FR-PERF-06 | The slow-computer wizard shall ask at most 5 simple questions (for example when it is slow, how long since the last restart) and run read-only checks: disk fullness, memory pressure, number of startup programs, time since restart, and pending Windows updates. | M | R2 |
| FR-PERF-07 | The wizard shall end with ranked recommendations, each stating expected benefit, effort, and a button that opens the matching tool (Storage, Startup, updates guide, restart advice). "No problem found" is a valid result and shall be stated plainly. | M | R2 |
| FR-PERF-08 | The wizard and snapshot shall never change settings, files, or processes by themselves. | M | R2 |
| FR-PERF-09 | The user shall be able to copy or save the snapshot and wizard result as plain text for support. | C | R2 |

### 3.15 Battery Report (FR-BAT)
Read-only. Shown only on devices that have a battery.

| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-BAT-01 | The Battery page shall appear only when the device reports a battery; otherwise it shall be hidden. | M | R2 |
| FR-BAT-02 | The app shall use Windows' own battery report to obtain design capacity, current full-charge capacity, cycle count (when the device reports it), and recent usage, and present battery health as a percentage with a plain-language verdict. | M | R2 |
| FR-BAT-03 | Where the device does not report a value, the app shall say "Your device does not report this" and shall not estimate it. | M | R2 |
| FR-BAT-04 | Tips shall cover screen brightness, background apps, Bluetooth, and battery saver, with buttons that open the relevant Windows settings (including battery usage by app) and a fallback instruction if a page cannot be opened. | S | R2 |
| FR-BAT-05 | Health verdict bands shall be advisory and consistent: 80% or more "Good", 60 to 79% "Reduced", under 60% "Worn: consider a replacement battery". | S | R2 |
| FR-BAT-06 | Battery health readings shall be stored locally to show a trend over months. | C | R3 |

### 3.16 Explorer Context Menu (FR-CTX)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-CTX-01 | The user may opt in (Settings) to add "Rename with One Click" for selected files and "Organise with One Click" for folders to the Explorer right-click menu, registered for the current user only and removable in one click. | S | R2 |
| FR-CTX-02 | Choosing an entry shall open One Click at the Preview step with the selection preloaded; nothing shall change until the user confirms as usual. | M | R2 |
| FR-CTX-03 | When Explorer launches the app several times for a multi-file selection, the app shall combine the launches into one selection (up to 1,000 files) and open a single window. | M | R2 |
| FR-CTX-04 | On Windows 11, the entries appear under "Show more options"; the Settings page shall say so. | M | R2 |
| FR-CTX-05 | Uninstalling shall remove all context-menu registrations; the portable edition shall not offer this feature. | M | R2 |
| FR-CTX-06 | A selection in a protected location shall open the app with the standard protected-folder message and no preview. | M | R2 |

### 3.17 Laptop Migration Checklist (FR-MIG)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-MIG-01 | A guided checklist shall list what to move to a new computer: Documents, Pictures, Desktop, Downloads, browser bookmarks (with export instructions), licence keys and software accounts, email and cloud sign-in details, and printer and Wi-Fi notes. The app shall never read or store passwords or Wi-Fi keys. | S | R2 |
| FR-MIG-02 | The user shall pick the folders to copy; the app shall estimate total size, check free space on the chosen destination, and block the copy if space is insufficient. | S | R3 |
| FR-MIG-03 | Copying shall be resumable, verified, shall never delete from the source, and shall reuse the backup copy engine (FR-BAK-02). | S | R3 |
| FR-MIG-04 | An "after the move" checklist shall guide setup on the new computer (sign in to accounts, reinstall programs, check that files arrived). | C | R3 |

### 3.18 Portable Edition (FR-PTB)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-PTB-01 | A portable edition shall run from a folder or removable drive with no installation and no administrator rights. | S | R3 |
| FR-PTB-02 | In portable mode, all data (database, settings, logs, holding area by default) shall live in a data folder beside the program; nothing shall be written to the user profile or registry. | S | R3 |
| FR-PTB-03 | Features that need registration (start with Windows, context menu, background tray rules) shall be shown as unavailable with a one-sentence reason. | S | R3 |
| FR-PTB-04 | If the portable drive is removed during a batch, the app shall stop safely, and the next start shall run crash recovery (FR-CORE-12). | S | R3 |
| FR-PTB-05 | The app shall warn when the data or holding folder is on a read-only or nearly full drive. | C | R3 |

### 3.19 Command-Line Interface (FR-CLI)
| ID | Requirement | Pri | Rel |
|---|---|---|---|
| FR-CLI-01 | A command-line program (`oneclick`) shall expose the engine through commands for rename, organise, scan, history, undo, and rules (and duplicates when available), plus `--help` with examples and `--version`. | S | R3 |
| FR-CLI-02 | The CLI shall be safe by default: commands show a preview only unless `--apply` is given; `--yes` skips the confirmation prompt; protected locations are enforced exactly as in the app. | M | R3 |
| FR-CLI-03 | The CLI and the app shall share one journal, so a batch applied from the CLI can be undone in the app, and the reverse. | M | R3 |
| FR-CLI-04 | Output shall be human-readable by default and machine-readable with `--json`; documented exit codes shall be 0 success, 1 partly completed, 2 invalid usage, 3 blocked by a safety rule, 4 error. | S | R3 |
| FR-CLI-05 | The CLI shall work without prompts when `--yes` is set and shall cancel safely on Ctrl+C, leaving an undoable state. | S | R3 |
| FR-CLI-06 | All arguments shall be validated as paths and values, and shall never be executed or passed to a shell. | M | R3 |

---

## 4. External interface requirements

### 4.1 User interface
Defined in section 5. The app shall be a single window with left navigation, content area, and status bar.

### 4.2 Software interfaces
| Interface | Use | Notes |
|---|---|---|
| Windows file system (NTFS, exFAT) | All file work | Long-path aware, case-insensitive aware |
| Windows Shell | Open files, show in Explorer, Recycle Bin | Documented APIs only |
| Registry (current user) | Startup entries, StartupApproved | Reversible changes only |
| WMI / Windows Security Center (read-only) | Security checkup | Fail gracefully when unavailable |
| `ms-settings:` URIs | Guides deep links | Fallback text required |
| Local SQLite database | Journal, rules, settings | Stored under the user's local app data |
| Windows Search index (read-only) | Smart Search | Optional; fall back to own scan (FR-FIND-05) |
| System performance counters and process list (read-only) | Performance snapshot | Current user's processes only for actions |
| Windows battery report | Battery page | Standard-user generation; parse defensively |
| Explorer shell registration (current user) | Context menu | Registered and removed per user, no admin |
| Command line and exit codes | CLI | Section 3.19 |

### 4.3 Communications interfaces
No network traffic in normal operation. One optional HTTPS request checks for updates (version number only, no identifiers). Crash reports, if the user opts in, shall have all file paths and user names removed.

---

## 5. HCI and user experience requirements

| ID | Requirement |
|---|---|
| UX-01 | **Consistent flow.** Every file tool shall follow the same five steps: Choose, Configure, Preview, Apply, Result (with Undo). Users learn it once. |
| UX-02 | **Navigation.** Left rail order: Overview, Search, Rename, Organise, Storage, Duplicates, Startup, Performance (with Battery as a tab on devices that have one), Automation, Backup, Guides, History, Settings. Unreleased items shall be hidden, not greyed out. |
| UX-03 | **Plain language.** Reading level at or below grade 8; sentences under 20 words; no jargon without an inline explanation; no exclamation marks, humour, or emoji. |
| UX-04 | **Buttons name the action and the result** ("Move 12 files"), never "OK" or "Submit". Destructive actions never use the default (Enter) focus. |
| UX-05 | **Errors** shall state what happened, why, and what the user can do, in that order, with no error codes in the primary text. Templates are in Appendix D. |
| UX-06 | **Feedback.** Every action shall give visible feedback within 100 ms; operations over 1 second show progress; completion shows a result summary with an Undo link. |
| UX-07 | **States.** Every screen shall define empty, loading, error, partial-result, and success states. |
| UX-08 | **Progressive disclosure.** Advanced options (regex, advanced filters) are collapsed by default. |
| UX-09 | **Visual design.** Calm, professional, neutral palette with one accent colour; 8 px spacing grid; Segoe UI Variable type scale; monoline Fluent-style icons; no emoji or decorative illustrations; light and dark themes. |
| UX-10 | **Motion** shall be minimal (150 ms or less) and disabled when the system requests reduced motion. |
| UX-11 | **Responsiveness.** Minimum window 960x600; layout stays usable at 200% scaling; no horizontal scrolling for primary content. |
| UX-12 | **Keyboard.** Every function is reachable by keyboard with logical tab order; shortcuts: Ctrl+Z undo last action, Ctrl+F search, Ctrl+Enter apply (with confirmation), F1 help. |
| UX-13 | **Trust cues.** The app shall state at each apply step that nothing is permanently deleted and that undo is available (when true). |
| UX-14 | **Onboarding** shall teach by doing: first launch offers a sample "Tidy Downloads" preview, with no change until the user applies. |
| UX-15 | **Terminology** (use consistently): "holding area", "preview", "batch" shall be shown to users as "change", "undo", never "transaction", "journal", or "rollback". |

Usability evaluation plan: heuristic evaluation of every screen before release; moderated usability tests with 5 to 8 representative participants per round for each release; metrics are task success, time on task, error count, and SUS.

---

## 6. Non-functional requirements

| ID | Category | Requirement | Verification |
|---|---|---|---|
| NFR-PERF-01 | Performance | Cold start under 2 s on a 4 GB RAM, HDD laptop | Benchmark |
| NFR-PERF-02 | Performance | Preview of 10,000 files in under 2 s; rename preview update under 300 ms for 1,000 files | Benchmark |
| NFR-PERF-03 | Performance | Scan of 500,000 files uses under 400 MB memory and first results appear within 2 s | Benchmark |
| NFR-PERF-04 | Performance | The UI thread is never blocked for more than 100 ms | Profiling, UI test |
| NFR-PERF-05 | Performance | Idle memory under 120 MB; background rule engine under 1% CPU when idle | Soak test |
| NFR-REL-01 | Reliability | Zero data loss under power loss or crash mid-batch (recovery per FR-CORE-12) | Fault-injection tests |
| NFR-REL-02 | Reliability | Undo of any completed batch restores the original state byte for byte | Property-based tests |
| NFR-REL-03 | Reliability | Crash-free sessions at least 99.5% | Telemetry-free: user-submitted logs, beta feedback |
| NFR-SAFE-01 | Safety | No code path modifies a protected location (verified by tests that attempt it) | Unit and security tests |
| NFR-SEC-01 | Security | Runs without elevation; no embedded secrets; dependencies scanned in CI; installer and binaries signed | CI, release checklist |
| NFR-SEC-02 | Security | Path inputs validated against traversal, reserved names, and symbolic-link escapes | Security tests |
| NFR-PRIV-01 | Privacy | No telemetry by default; no file names or contents transmitted; logs stay local and scrub user names on export | Code review, network test |
| NFR-USA-01 | Usability | SUS at least 80 at R2; task success at least 90% for core tasks | Usability tests |
| NFR-ACC-01 | Accessibility | WCAG 2.1 AA: contrast 4.5:1 for text, 3:1 for controls; full keyboard operation; screen-reader support (Narrator, NVDA); high-contrast and 200% text scaling supported; colour is never the only signal; visible focus; minimum target 32x32 px | Accessibility Insights, manual screen-reader pass |
| NFR-MAIN-01 | Maintainability | Layered architecture; engine unit-test line coverage at least 90%, overall at least 80%; static analysis with zero warnings | CI gates |
| NFR-PORT-01 | Portability | Windows 10 22H2 and 11 on x64; installs and runs per-user with no admin rights | Install matrix |
| NFR-LOC-01 | Localisation | No hard-coded UI strings; culture-aware date, number, and size formatting | Code review, pseudo-locale test |
| NFR-INST-01 | Installability | Install in under 60 s; clean uninstall that leaves user files and offers to remove app data | Install tests |
| NFR-SUP-01 | Supportability | One-click "export diagnostic log" with scrubbed paths | Manual test |
| NFR-PERF-06 | Performance | Smart Search shows first results within 1 s when the index is available, and within 3 s using the fallback scan on a 4 GB RAM, HDD laptop | Benchmark |
| NFR-PERF-07 | Performance | Performance snapshot adds under 2% CPU and under 40 MB memory while visible and nothing while hidden | Profiling |
| NFR-SEC-03 | Security | Arguments from the context menu and the CLI are treated as untrusted: canonicalised, checked by PathGuard, never executed | Security tests |
| NFR-PORT-02 | Portability | The portable edition leaves no files in the user profile and no registry changes | Install and file-system diff tests |

---

## 7. Data requirements

| Entity | Key fields | Notes |
|---|---|---|
| Batch | id, tool, label, created_at, status (planned, running, complete, failed, undone, partially_undone), item_count | One row per user action or rule run |
| Operation | id, batch_id, type (move, rename, hold), source_path, destination_path, size, modified_at, status, error | Written before execution |
| Rule | id, name, enabled, trigger, conditions (JSON), actions (JSON), last_run, failure_count | |
| RuleRun | id, rule_id, started_at, files_affected, errors, dry_run | |
| Preset | id, kind (rename, organise), name, settings (JSON) | |
| Setting | key, value | Includes schema version |
| HoldingItem | id, original_path, held_path, held_at, expires_at, batch_id | Drives retention purge |
| RuleTemplate | id, name, description, parameters, rule definition, example result | Bundled with the app, read-only, versioned |
| SavedSearch | id, name, query, filters | R3 (FR-FIND-09) |
| BatteryReading | id, taken_at, design_capacity, full_capacity, cycle_count | R3 (FR-BAT-06), values may be null |

Data shall be stored in a local SQLite database (write-ahead logging on) under the user's local app data folder. Schema changes shall use versioned, forward-only migrations with a backup taken before each migration.

---

## 8. Key use cases

**UC-01 Rename photos.** Actor: Mwila. Pre: folder of 120 photos named IMG_xxxx. Flow: choose folder, set prefix "Trip_" and numbering, preview shows 120 new names, apply with confirmation, result shows "120 files renamed" with Undo. Alternate: a name conflict is highlighted in preview and resolved automatically with a suffix; locked files are skipped and listed. Post: journal contains one batch; undo restores all names.

**UC-02 Tidy Downloads.** Actor: Mwila. Flow: choose "Tidy Downloads" preset, preview shows a folder tree (Documents 34, Images 58, Installers 9, Other 6), apply. Alternate: files modified under 2 minutes ago are excluded and flagged. Post: undo returns every file to Downloads.

**UC-03 Free up space.** Actor: Mwila. Pre: drive 93% full. Flow: Overview warns in plain language, user opens Storage, scans, filters files over 100 MB older than 90 days, selects 6, moves them to the holding area. Post: free space increases only after the holding area is emptied (the app says so); undo available until then.

**UC-04 Undo a mistake.** Actor: any. Flow: user opens History, selects the batch, previews what will be restored, confirms. Alternate: a destination file was moved since; the app lists it and restores the rest.

**UC-05 Speed up start-up.** Actor: Chola. Flow: open Startup, read advisory labels, switch off three entries, see a note that changes apply at next sign-in, switch one back on.

**UC-06 Auto-sort new downloads.** Actor: Chola. Flow: choose template "Sort new downloads by type", run a dry run on existing files, review, enable; a tray icon confirms the rule is active; later runs appear in History and can be undone.

**UC-07 Find a lost file.** Actor: Mwila. Flow: open Search, type part of the name, choose "This week", see results stream in, select the file, choose "Show in Explorer". Alternate: no results; the app suggests widening the date range and including other folders. Post: nothing on disk changed.

**UC-08 Find out why the computer is slow.** Actor: Mwila. Flow: open Performance, start the wizard, answer 3 questions, read ranked recommendations ("Drive is 93% full: free space", "11 programs start with Windows: review them"), choose one to open the tool. Alternate: no problem found; the app says so and suggests a restart if it has not been restarted in 7 days. Post: nothing changed by the wizard itself.

---

## 9. Verification and validation

| Level | Scope | Owner |
|---|---|---|
| Unit | Engine, planners, path guard, name sanitiser, rule evaluation | Developer |
| Integration | Real temporary folders, SQLite, registry (test hive), file watcher | Developer, QA |
| System / UI | Full flows through the interface using UI automation | QA |
| Non-functional | Performance, soak, fault injection, security, accessibility | QA |
| Usability | Moderated tests, heuristic review, SUS | HCI |
| Acceptance | Each Must requirement demonstrated against its ID | Product owner |

**Traceability.** Each requirement ID maps to at least one test case ID (format TC-<module>-<number>). The test management sheet shall list requirement, test, status, and last run. A Must requirement without a passing test blocks release.

**Release gates (R1):** all Must requirements verified; zero open defects of severity 1 or 2; engine coverage at least 90%; zero data-loss defects across the fuzz suite (at least 1,000 randomised apply and undo cycles); accessibility check passed; at least one usability round completed with task success at least 85%; clean install and uninstall verified on Windows 10 and 11.

**Acceptance tests for the v1.1 additions** (each also needs unit and integration tests per the implementation guide):

| ID | Case | Expected | Req |
|---|---|---|---|
| TC-FIND-001 | Search "invoice" with "this week" in user folders | Only matching files in scope, correct dates | FR-FIND-01, 02 |
| TC-FIND-002 | Search including a protected folder path | Protected folder excluded, user told why | FR-FIND-08 |
| TC-FIND-003 | First results time with and without index | Within NFR-PERF-06 | NFR-PERF-06 |
| TC-PERF-001 | Browser with many helper processes | Grouped under one entry with summed usage | FR-PERF-02 |
| TC-PERF-002 | Attempt to close a system or security process | Not offered; engine refuses | FR-PERF-05 |
| TC-PERF-003 | Run wizard; compare files, registry, processes before and after | No difference | FR-PERF-08 |
| TC-BAT-001 | Desktop PC without a battery | Battery page hidden | FR-BAT-01 |
| TC-BAT-002 | Report with design 50 Wh and full charge 38 Wh | Health 76%, "Reduced" | FR-BAT-02, 05 |
| TC-BAT-003 | Report missing cycle count | "Your device does not report this" | FR-BAT-03 |
| TC-AUT-004 | Import a rule pointing at a protected folder | Rejected with explanation | FR-AUT-10 |
| TC-AUT-005 | Enable any template | Dry run shown first, rule starts disabled until approved | FR-AUT-09 |
| TC-CTX-001 | Register then remove context menu | Registry returns to original state | FR-CTX-01, 05 |
| TC-CTX-002 | Select 200 files and use the menu | One window, 200 files preloaded | FR-CTX-03 |
| TC-MIG-001 | Destination smaller than selected folders | Copy blocked with plain message | FR-MIG-02 |
| TC-PTB-001 | Run portable edition, diff user profile and registry | No changes | FR-PTB-02, NFR-PORT-02 |
| TC-PTB-002 | Remove drive mid-batch, reconnect, start | Recovery prompt, consistent state | FR-PTB-04 |
| TC-CLI-001 | `oneclick rename` without `--apply` | Preview only, no changes | FR-CLI-02 |
| TC-CLI-002 | Run each exit-code scenario | Codes 0 to 4 as documented | FR-CLI-04 |
| TC-CLI-003 | Apply from CLI, undo in the app | Original state restored | FR-CLI-03 |

**Defect severity.** S1: data loss, corruption, or crash on core flow. S2: core function broken with no workaround. S3: function impaired with workaround. S4: cosmetic or minor.

---

## 10. Risks
| Risk | Impact | Mitigation |
|---|---|---|
| Bug causes data loss | Critical, destroys trust | Single engine, write-ahead journal, holding area, property-based and fault-injection testing, protected paths |
| Users misread previews | High | Clear previews, counts, sample confirmations, undo |
| Windows changes Settings URIs or registry behaviour | Medium | Graceful fallbacks, version checks, regression tests per Windows release |
| Antivirus flags the app (file moving, registry) | Medium | Code signing, reputation building, no obfuscation, documented behaviour |
| Slow on HDD and low-RAM machines | High | Streaming scans, bounded memory, background priority, performance budgets |
| Scope creep | High | Principles filter, MoSCoW, change control |
| Name "One Click" is generic and may conflict with existing marks | Medium | Trademark and domain search before public release |
| "Close this program" causes lost unsaved work | Medium | Normal close first, warning, second confirmation for force close, never system or security processes |
| Context-menu behaviour differs between Windows 10 and 11, or conflicts with other shell extensions | Medium | Per-user registration, clear Settings note, compatibility tests, easy removal |
| Battery or index data missing on some devices | Low | Show "not reported", never estimate; fall back to own scan |
| Imported rules or CLI arguments used to reach protected files | High | Same PathGuard checks, imported rules arrive disabled, arguments never executed |

---

## 11. Release plan
| Release | Contents | Gate |
|---|---|---|
| R0 | Prototype v0.1 (done): rename, organise, storage, undo | Demonstrates flow |
| R1 (MVP) | Engine, journal, undo, History, Rename, Organiser, Storage Analyser, Settings, onboarding, installer | Section 9 gates |
| R2 (1.0) | Duplicates, Startup Manager, Automation with template library, Smart Search, Performance snapshot and slow-computer wizard, Battery report, Guides (including phone and laptop guide), Security Checkup, Explorer context menu, migration checklist (guide only), presets, advanced rename, auto-update | SUS at least 80 |
| R3 | Backup Helper, migration copy, portable edition, command-line interface, scheduled tidy proposals, saved searches, battery trend, treemap, ARM64, additional languages | Per feature |

### 11.1 Priority order for post-MVP features
R1 scope is unchanged. After R1, build in this order, interleaved with the other R2 modules as shared dependencies allow.

| Rank | Feature | Requirement IDs | Target | Why this rank | Depends on |
|---|---|---|---|---|---|
| 1 | Smart Search | FR-FIND-01 to 09 | R2 | Solves the most common frustration with low risk, because it is read-only | Scanner and PathGuard from R1 |
| 2 | Performance snapshot and slow-computer wizard | FR-PERF-01 to 09 | R2 | Directly addresses the top research finding ("my laptop is slow") | Storage Analyser, Startup Manager |
| 3 | Rule template library | FR-AUT-08 to 12 | R2 | Makes automation approachable for non-technical users | Automation Rules (FR-AUT-01 to 07) |
| 4 | Battery report | FR-BAT-01 to 06 | R2 | Easy to build, high perceived value, no side effects | None |
| 5 | Explorer context menu | FR-CTX-01 to 06 | R2 (late) | Large convenience gain, moderate effort and testing cost | Single-instance handling, Rename, Organise |
| 6 | Laptop migration checklist | FR-MIG-01 to 04 | Checklist R2, copy R3 | Valued by students and graduates; the copy part needs the backup engine | Backup Helper (FR-BAK-02) |
| 7 | Phone and laptop guide | FR-QAC-04, 05 | R2 | Content only, quick to ship | Guides viewer (FR-QAC-01) |
| 8 | Portable edition and CLI | FR-PTB, FR-CLI | R3 | Helps adoption on shared machines and speeds testing; the CLI may be pulled forward because it reuses the engine | Engine, journal |

---

## 12. Change control
Changes to this document require a written request stating the reason, affected requirement IDs, impact on tests and schedule, and the product owner's approval. The version table at the top is updated on every approved change.

### 12.1 Change history
| Version | Date | Change |
|---|---|---|
| 1.0 | 6 October 2026 | Baseline specification |
| 1.1 | 8 October 2026 | Added sections 3.13 to 3.19 (Smart Search, Performance, Battery, Context Menu, Migration, Portable, CLI); added FR-AUT-08 to 12 and FR-QAC-04, 05; added NFR-PERF-06, 07, NFR-SEC-03, NFR-PORT-02; added data entities, use cases UC-07 and UC-08, acceptance tests, risks; changed UX-02 navigation order; added section 11.1 priority order. No existing requirement ID was renumbered or removed, and R1 scope is unchanged. |

---

## Appendix A. Glossary of user-facing terms
Use "holding area" (not trash or quarantine), "change" (not transaction), "undo" (not rollback), "preview" (not dry run, except in Advanced), "startup program" (not autorun entry).

## Appendix B. Protected locations
Never modified, scanned for cleanup, or written to (except the app's own data folder):
- Windows directory and `System32`, `SysWOW64`, `WinSxS`
- `Program Files`, `Program Files (x86)`, `ProgramData`
- Drive roots (for example `C:\`) and the user profile root itself (`C:\Users\<name>`)
- `AppData` of any user (except the app's own folder)
- Other users' profiles
- Recovery, boot, and system-volume-information folders
- Any path with the system or reparse-point attribute
- Cloud placeholder files not stored locally

Individual user folders (Downloads, Documents, Desktop, Pictures, Videos, Music) are allowed. Folder roots chosen by the user inside these are allowed; the guard checks every resolved path, not only the chosen root.

## Appendix C. Default file categories
| Category | Extensions |
|---|---|
| Images | jpg, jpeg, png, gif, bmp, webp, svg, heic, tiff |
| Documents | pdf, doc, docx, txt, rtf, odt, ppt, pptx, md |
| Spreadsheets | xls, xlsx, csv, ods |
| Video | mp4, mkv, mov, avi, wmv, webm |
| Audio | mp3, wav, flac, m4a, aac, ogg |
| Archives | zip, rar, 7z, tar, gz |
| Installers | exe, msi, msix, dmg, apk |
| Code | py, js, ts, html, css, java, cs, cpp, c, json, sql |
| Other | everything else |

## Appendix D. Message templates
- **Skipped item:** "3 files were skipped. They are open in another program. Close the program and try again."
- **Protected folder:** "One Click does not change this folder because it contains files your computer needs to run. Choose a folder such as Downloads or Documents."
- **Undo partial:** "12 of 14 files were restored. 2 files were moved or renamed since this change, so they were left where they are."
- **Disk almost full:** "This drive is 93% full. A nearly full drive makes the computer slow. Storage can show what is using the space."
- **Permission denied:** "Windows did not allow One Click to change this item. Your files were not affected."

## Appendix E. Open questions for the product owner
1. Pricing model (free, free with paid Pro features, one-time licence)?
2. Is the first public audience students, offices, or both?
3. Is a Microsoft Store listing wanted in addition to direct download?
4. Which languages after English?
5. Is a name and trademark check completed for "One Click"?
