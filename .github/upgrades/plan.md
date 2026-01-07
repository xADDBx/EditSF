# .NET Upgrade Plan — EditSF (.NET Framework 4.8 ? .NET 10)

> **Strategy**: **All-at-Once Strategy** — all projects upgraded simultaneously in a single coordinated operation (no intermediate target frameworks).

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Migration Strategy](#2-migration-strategy)
3. [Detailed Dependency Analysis](#3-detailed-dependency-analysis)
4. [Project-by-Project Plans](#4-project-by-project-plans)
5. [Package Update Reference](#5-package-update-reference)
6. [Breaking Changes Catalog](#6-breaking-changes-catalog)
7. [Testing & Validation Strategy](#7-testing--validation-strategy)
8. [Risk Management](#8-risk-management)
9. [Complexity & Effort Assessment](#9-complexity--effort-assessment)
10. [Source Control Strategy](#10-source-control-strategy)
11. [Success Criteria](#11-success-criteria)

---

## 1. Executive Summary

### Scenario
Upgrade the `EditSF.sln` solution from **.NET Framework 4.8** (classic, non-SDK-style projects) to **.NET 10**.

### Target state
- **Target frameworks (per assessment)**:
  - WinForms apps/controls: `net10.0-windows`
  - Libraries/console apps (non-UI): `net10.0`
- **Project format**: Convert all `*.csproj` to **SDK-style**.

### Scope
**Projects in scope (6 total):**
- `CommonDialogs/CommonDialogs.csproj` (Classic WinForms)
- `EditSF/EditSF.csproj` (Classic WinForms main app)
- `EsfControl/EsfControl.csproj` (Classic WinForms control library)
- `EsfLibrary/EsfLibrary.csproj` (Classic class library)
- `EsfTest/EsfTest.csproj` (Classic .NET app)
- `Library/7zip/7Zip.csproj` (Classic class library)

### Discovered metrics (from `assessment.md`)
- Total projects: **6**
- Total NuGet packages: **0** (no package upgrades required)
- Total code files: **61**
- Total LOC: **10,851**
- Estimated LOC to modify: **2,147+** (~**19.8%** of codebase)
- API incidents:
  - **Binary incompatible**: **2,143** (primarily Windows Forms)
  - **Source incompatible**: **4**
  - **Behavioral change**: 0

### Key compatibility findings
- Dominant modernization surface is **Windows Forms** API compatibility (not NuGet packages).
- Extra hotspots:
  - **WinForms legacy controls removed in modern .NET** (e.g., `StatusBar`, `DataGrid`, `ContextMenu`, `MainMenu`, `MenuItem`, `ToolBar`) flagged within `EsfControl`.
  - **Legacy configuration system** usage flagged in `EditSF` and `7Zip`.

### Complexity classification
**Medium complexity**
- 6 projects (small-medium)
- Clear dependency graph with depth ~3 and no reported cycles
- No NuGet package upgrade complexity
- Significant code touch surface due to WinForms API compatibility and legacy controls

### Selected Strategy
**All-at-Once Strategy** — all projects upgraded simultaneously in a single coordinated operation.

**Rationale:**
- Only **6** projects with a straightforward project dependency graph
- No NuGet packages to coordinate across frameworks
- WinForms upgrades typically require coordinated changes across UI projects; upgrading simultaneously avoids mismatched TFMs/references

### Security vulnerabilities
- None reported by analysis output.

### Plan generation approach (iterations)
This plan is built to support an atomic upgrade (single coordinated pass) with clear checkpoints:
- Preparation prerequisites (SDK / `global.json` validation)
- Atomic conversion + TFM upgrade + code remediation
- Unified solution build validation
- Test run validation

---

## 2. Migration Strategy

### Approach selection
**All-at-Once Strategy** (single coordinated upgrade).

Although the dependency graph suggests a bottom-up order, the intended execution is **atomic**: all project file conversions, TFMs, and remediation changes happen in one coordinated pass to avoid mixed-framework states.

### Prerequisites / environment assumptions
- .NET **10 SDK** installed (because `net10.0` / `net10.0-windows` is required).
- The solution is Windows desktop focused (WinForms), so build agents must be Windows.
- If a `global.json` exists, it must allow a .NET 10 SDK.

### Atomic upgrade operations (performed as one coordinated batch)
1. **Convert all projects to SDK-style**
   - `CommonDialogs/CommonDialogs.csproj`
   - `EditSF/EditSF.csproj`
   - `EsfControl/EsfControl.csproj`
   - `EsfLibrary/EsfLibrary.csproj`
   - `EsfTest/EsfTest.csproj`
   - `Library/7zip/7Zip.csproj`
2. **Update Target Framework Monikers (TFMs)** according to assessment proposed targets:
   - WinForms projects ? `net10.0-windows`
     - `CommonDialogs`
     - `EsfControl`
     - `EditSF`
   - Non-UI projects ? `net10.0`
     - `EsfLibrary`
     - `EsfTest`
     - `7Zip`
3. **Enable Windows Desktop support where needed**
   - Ensure WinForms projects have the appropriate SDK-style settings (see §6 Breaking Changes Catalog).
4. **Remediate compile-time breaking changes**
   - Replace/remove WinForms legacy controls not supported in modern .NET.
   - Address any API surface diffs flagged as binary/source incompatible.
   - Address legacy configuration usage to ensure it loads correctly on .NET 10.
5. **Solution-wide restore + build validation**
   - Ensure the full solution builds with no errors.

### Why not incremental?
Even though incremental is sometimes safer, upgrading these projects “one at a time” would create temporary states where `net48` projects reference `net10.0` projects (or vice versa), which is typically not viable for WinForms/classic project references and will complicate validation.

---

## 3. Detailed Dependency Analysis

### Project dependency graph (from `assessment.md`)
Edges below mean **A ? B** where **A depends on B**:
- `EditSF` ? `EsfControl`, `EsfLibrary`, `7Zip`, `CommonDialogs`
- `EsfControl` ? `EsfLibrary`, `CommonDialogs`
- `EsfLibrary` ? `7Zip`
- `EsfTest` ? `EsfLibrary`

### Leaf nodes (no project dependencies)
These can be upgraded “first” conceptually (even though execution is atomic):
- `Library/7zip/7Zip.csproj`
- `CommonDialogs/CommonDialogs.csproj`

### Intermediate nodes
- `EsfLibrary/EsfLibrary.csproj` (depends on `7Zip`)
- `EsfControl/EsfControl.csproj` (depends on `EsfLibrary`, `CommonDialogs`)

### Root nodes (applications / entry points / top of graph)
- `EditSF/EditSF.csproj` (main WinForms app)
- `EsfTest/EsfTest.csproj` (aux app; depends on `EsfLibrary`)

### Dependency depth and critical paths
- Max depth is ~3:
  - `EditSF` ? `EsfControl` ? `EsfLibrary` ? `7Zip`
  - `EditSF` ? `EsfLibrary` ? `7Zip`
- **Critical path projects** for the UI:
  - `CommonDialogs` and `7Zip` underpin libraries and the main app; breaks here propagate.

### Circular dependencies
- None reported in assessment.

---

## 4. Project-by-Project Plans

> Notes:
> - All projects are currently `net48` and **not** SDK-style.
> - Proposed TFMs are taken from `assessment.md`.
> - Because this is an all-at-once upgrade, treat each project plan as part of a single coordinated change set.

### Project: `Library/7zip/7Zip.csproj`
**Current state**: `net48`, ClassicClassLibrary, SDK-style = False
- Dependencies: none
- Dependants: `EsfLibrary`, `EditSF`
- Files/LOC: 30 files / 4,552 LOC
- API issues: 2 source incompatible

**Target state**: `net10.0`, SDK-style project

**Migration steps**:
1. Convert to SDK-style.
2. Update `TargetFramework` ? `net10.0`.
3. Resolve the 2 source-incompatible findings (see `assessment.md` incidents for this project).
4. Validate: project builds as part of solution build.

---

### Project: `CommonDialogs/CommonDialogs.csproj`
**Current state**: `net48`, ClassicWinForms, SDK-style = False
- Dependencies: none
- Dependants: `EsfControl`, `EditSF`
- Files/LOC: 11 files / 934 LOC
- API issues: 1,149 binary incompatible (WinForms)

**Target state**: `net10.0-windows`, SDK-style WinForms project

**Migration steps**:
1. Convert to SDK-style.
2. Update `TargetFramework` ? `net10.0-windows`.
3. Ensure WinForms support is enabled (see §6).
4. Resolve WinForms API incompatibilities surfaced during compilation (see §6 and `assessment.md`).
5. Validate: project builds as part of solution build.

---

### Project: `EsfLibrary/EsfLibrary.csproj`
**Current state**: `net48`, ClassicClassLibrary, SDK-style = False
- Dependencies: `7Zip`
- Dependants: `EditSF`, `EsfControl`, `EsfTest`
- Files/LOC: 19 files / 3,536 LOC
- API issues: none reported

**Target state**: `net10.0`, SDK-style project

**Migration steps**:
1. Convert to SDK-style.
2. Update `TargetFramework` ? `net10.0`.
3. Ensure project reference to `7Zip` remains valid after SDK-style conversion.
4. Validate: project builds as part of solution build.

---

### Project: `EsfControl/EsfControl.csproj`
**Current state**: `net48`, ClassicWinForms, SDK-style = False
- Dependencies: `EsfLibrary`, `CommonDialogs`
- Dependants: `EditSF`
- Files/LOC: 4 files / 542 LOC
- API issues: 476 binary incompatible (WinForms)
- Special feature: 196 issues flagged for **WinForms legacy controls**

**Target state**: `net10.0-windows`, SDK-style WinForms project

**Migration steps**:
1. Convert to SDK-style.
2. Update `TargetFramework` ? `net10.0-windows`.
3. Ensure WinForms support is enabled (see §6).
4. Replace legacy WinForms controls with modern equivalents:
   - `StatusBar` ? `StatusStrip`
   - `MainMenu`/`MenuItem` ? `MenuStrip`/`ToolStripMenuItem`
   - `ContextMenu` ? `ContextMenuStrip`
   - `ToolBar` ? `ToolStrip`
   - `DataGrid` ? `DataGridView`
5. Validate: project builds as part of solution build.

---

### Project: `EditSF/EditSF.csproj`
**Current state**: `net48`, ClassicWinForms (main app), SDK-style = False
- Dependencies: `EsfControl`, `EsfLibrary`, `7Zip`, `CommonDialogs`
- Dependants: none
- Files/LOC: 9 files / 776 LOC
- API issues: 518 binary incompatible, 2 source incompatible
- Special feature: Legacy configuration system usage flagged

**Target state**: `net10.0-windows`, SDK-style WinForms app

**Migration steps**:
1. Convert to SDK-style.
2. Update `TargetFramework` ? `net10.0-windows`.
3. Ensure WinForms support is enabled (see §6).
4. Resolve WinForms binary/source incompatibilities as they surface in compilation.
5. Address legacy configuration usage:
   - If using `app.config`, decide whether to (a) keep via `System.Configuration.ConfigurationManager` package as an interim bridge, or (b) migrate to modern configuration.
6. Validate: solution builds; app launches in debug as a manual check (manual validation criterion; not an automated task).

---

### Project: `EsfTest/EsfTest.csproj`
**Current state**: `net48`, ClassicDotNetApp, SDK-style = False
- Dependencies: `EsfLibrary`
- Dependants: none
- Files/LOC: 4 files / 511 LOC
- API issues: none reported

**Target state**: `net10.0`, SDK-style project

**Migration steps**:
1. Convert to SDK-style.
2. Update `TargetFramework` ? `net10.0`.
3. Validate: project builds as part of solution build.

---

## 5. Package Update Reference

### NuGet packages
Per `assessment.md`, there are **0 NuGet package dependencies** detected across the solution.

?? Requires validation: ensure none of the classic `packages.config`-style dependencies exist but were not detected (e.g., due to custom restore). If packages are later found, include them in this section with explicit current/suggested versions.

---

## 6. Breaking Changes Catalog

This catalog lists the **expected** breaking-change areas based on `assessment.md`. Exact fixes will be driven by compilation errors after TFMs/project formats are updated.

### 6.1 SDK-style conversion breaking-change categories
Common differences when converting classic `net48` projects to SDK-style:
- Implicit framework references vs explicit `Reference` items.
- Resource/designer generation settings for WinForms (`*.Designer.cs`, `*.resx`).
- Changes in default `OutputPath`/`IntermediateOutputPath` and assembly name inference.
- `app.config` handling and binding redirects (binding redirects are not used the same way in modern .NET).

### 6.2 WinForms on modern .NET (net10.0-windows)
**Required settings** for WinForms projects in SDK-style:
- Target Windows: `TargetFramework` should be `net10.0-windows`.
- WinForms enablement (one of the standard SDK-style forms):
  - `UseWindowsForms` set to `true` (typical for WinForms)
  - Or using WindowsDesktop SDK if necessary (assessment suggests `UseWindowsDesktop`/SDK options).

?? Requires validation: the exact MSBuild properties required will depend on how each project is structured today (classic csproj) and whether designers/resources are present.

### 6.3 Legacy WinForms controls removed/changed
Assessment flags legacy controls primarily in `EsfControl`.
Replacements to plan for:
- `StatusBar` ? `StatusStrip`
- `MainMenu` / `MenuItem` ? `MenuStrip` / `ToolStripMenuItem`
- `ContextMenu` ? `ContextMenuStrip`
- `ToolBar` ? `ToolStrip`
- `DataGrid` ? `DataGridView`

Expected follow-up fixes:
- Event model differences (e.g., click handlers, selection events).
- Layout differences (`Dock`, `Anchor` behavior is mostly the same but verify rendering).
- Designer-generated code changes.

### 6.4 Legacy configuration system
`EditSF` and `7Zip` are flagged for “Legacy Configuration System”. Common patterns include:
- `ConfigurationManager.AppSettings` / `ConfigurationManager.ConnectionStrings`

Options:
1. **Interim bridge**: add `System.Configuration.ConfigurationManager` package and keep `app.config` (recommended when you want minimal behavioral change initially).
2. **Modernize**: migrate to `Microsoft.Extensions.Configuration` (JSON / environment variables) and remove reliance on `app.config`.

Because `assessment.md` reports **0** NuGet packages, option (1) would introduce a new package; document and validate during execution.

---

## 7. Testing & Validation Strategy

### Test inventory
`assessment.md` does not list test projects explicitly and reports `EsfTest` as a `ClassicDotNetApp` (not necessarily a test project).

?? Requires validation: confirm whether there are any unit test projects in the solution. If found, include them here.

### Validation checkpoints (aligned to All-at-Once)
1. **Post-conversion validation (structure)**
   - All `*.csproj` are SDK-style.
   - WinForms projects target `net10.0-windows` and have WinForms enabled.
2. **Compilation validation (technical)**
   - Solution builds with **0 errors**.
3. **Functional validation (targeted)**
   - `EditSF` launches.
   - Key UI entry points: open dialogs (from `CommonDialogs`), load key controls (from `EsfControl`), read/write ESF operations (via `EsfLibrary`).
   - This is manual validation criteria (not an automatable task).

### Warning policy
- Prefer: no new warnings introduced by the upgrade.
- If warnings are introduced by new SDK analyzers, document and decide whether to:
  - Fix them immediately, or
  - Suppress/disable selectively with justification.

---

## 8. Risk Management

### High-risk areas
| Area | Risk level | Why | Mitigation |
|---|---|---|---|
| WinForms API surface across UI projects (`EditSF`, `CommonDialogs`, `EsfControl`) | High | Large number of API incidents (binary incompatible) | Keep changes compiling in one coordinated pass; use compiler errors to drive fixes; validate key UI flows manually after build |
| WinForms legacy controls (`EsfControl`) | High | Removed/changed legacy controls in modern .NET | Replace with `ToolStrip`/`MenuStrip`/`ContextMenuStrip`/`DataGridView` equivalents; re-wire event handlers |
| SDK-style conversion across all projects | Medium | Conversion can change build outputs, resource handling, and implicit references | Convert all projects together; confirm output types, WinForms designer resources, and project references |
| Legacy configuration system usage (`EditSF`, `7Zip`) | Medium | `app.config` patterns differ in modern .NET | Either migrate to modern configuration or add `System.Configuration.ConfigurationManager` as a compatibility bridge |

### Rollback / contingency
- Use a dedicated upgrade branch and a single PR.
- Roll back by reverting the PR if necessary.

---

## 9. Complexity & Effort Assessment

### Per-project complexity
| Project | Type | Target TFM | LOC | Est. LOC impact | Complexity |
|---|---|---:|---:|---:|---|
| `CommonDialogs` | WinForms | `net10.0-windows` | 934 | 1149+ | High |
| `EditSF` | WinForms app | `net10.0-windows` | 776 | 520+ | High |
| `EsfControl` | WinForms controls | `net10.0-windows` | 542 | 476+ | High |
| `EsfLibrary` | Library | `net10.0` | 3536 | 0+ | Low |
| `EsfTest` | App | `net10.0` | 511 | 0+ | Low |
| `7Zip` | Library | `net10.0` | 4552 | 2+ | Low |

### Overall complexity summary
- **High**: WinForms modernization + legacy control replacements dominate.
- **Low/Medium**: Libraries are mostly compatible but require SDK-style conversion.

---

## 10. Source Control Strategy

### Branching
- Source branch: `master`
- Upgrade branch: `upgrade-to-NET10`

### Commit / PR strategy (All-at-Once)
- Prefer a **single PR** containing the entire upgrade, because the plan’s strategy is an atomic conversion.
- Suggested commit structure inside the PR (still keeping a single PR):
  1. “Convert projects to SDK-style and update TFMs to .NET 10”
  2. “Fix compilation issues from WinForms + legacy controls + configuration”

### Review checklist
- Confirm all project references still resolve.
- Confirm WinForms resource/designer generation works.
- Confirm solution builds.

---

## 11. Success Criteria

### Technical criteria
- All projects target their proposed frameworks:
  - `EditSF`, `EsfControl`, `CommonDialogs` ? `net10.0-windows`
  - `EsfLibrary`, `EsfTest`, `7Zip` ? `net10.0`
- All projects are SDK-style.
- Solution builds successfully with **0 errors**.
- No dependency conflicts.

### Quality criteria
- Legacy WinForms controls replaced and UI compiles.
- Configuration reads required settings successfully at runtime (either via compatibility bridge or modern configuration).

### Security criteria
- No known vulnerable packages introduced.

### Process criteria
- Upgrade performed as a **single coordinated operation** consistent with the All-at-Once Strategy (no intermediate TFMs committed as final state).
