# Execution Tasks — EditSF (.NET Framework 4.8 ? .NET 10)

> This file is managed by the execution tracker. Do not edit manually once execution starts.

## Status Dashboard

- Target framework:
  - WinForms projects: `net10.0-windows`
  - Non-UI projects: `net10.0`
- Branch:
  - Source: `master`
  - Upgrade: `upgrade-to-NET10`

## Tasks

### [?] TASK-001: Pre-flight checks
- [?] (1) Verify repo is on upgrade branch `upgrade-to-NET10` and working tree is clean
- [ ] (2) Verify .NET 10 SDK is installed
- [ ] (3) Verify `global.json` (if present) allows .NET 10 SDK

### [ ] TASK-002: Convert projects to SDK-style (**PARALLEL**)
**Projects:**
- `Library/7zip/7Zip.csproj`
- `CommonDialogs/CommonDialogs.csproj`
- `EsfLibrary/EsfLibrary.csproj`
- `EsfControl/EsfControl.csproj`
- `EsfTest/EsfTest.csproj`
- `EditSF/EditSF.csproj`

**Subtasks:**
- [ ] TASK-002.1: Convert `Library/7zip/7Zip.csproj` to SDK-style
- [ ] TASK-002.2: Convert `CommonDialogs/CommonDialogs.csproj` to SDK-style
- [ ] TASK-002.3: Convert `EsfLibrary/EsfLibrary.csproj` to SDK-style
- [ ] TASK-002.4: Convert `EsfControl/EsfControl.csproj` to SDK-style
- [ ] TASK-002.5: Convert `EsfTest/EsfTest.csproj` to SDK-style
- [ ] TASK-002.6: Convert `EditSF/EditSF.csproj` to SDK-style

**Actions After All Subtasks Complete:**
- [ ] (1) Verify all projects are SDK-style and still included in solution

### [ ] TASK-003: Update target frameworks and Windows desktop settings
- [ ] (1) Set TFMs:
  - WinForms: `CommonDialogs`, `EsfControl`, `EditSF` ? `net10.0-windows`
  - Non-UI: `EsfLibrary`, `EsfTest`, `7Zip` ? `net10.0`
- [ ] (2) Ensure WinForms projects have required SDK properties (e.g., `UseWindowsForms=true`)
- [ ] (3) Verify solution loads and restores successfully

### [ ] TASK-004: Fix compilation errors from SDK-style conversion and TFM upgrade
- [ ] (1) Build solution and capture errors
- [ ] (2) Fix project-level issues (resources/designer, references, output types)
- [ ] (3) Fix API incompatibilities flagged by assessment:
  - WinForms binary incompatible (`Api.0001`) in `CommonDialogs`, `EsfControl`, `EditSF`
  - Source incompatible (`Api.0002`) in `7Zip`, `EditSF`
  - Legacy configuration usage in `EditSF`, `7Zip`
- [ ] (4) Rebuild until solution builds with 0 errors

### [ ] TASK-005: Replace legacy WinForms controls in `EsfControl`
- [ ] (1) Replace controls per plan §4 and §6:
  - `StatusBar` ? `StatusStrip`
  - `MainMenu`/`MenuItem` ? `MenuStrip`/`ToolStripMenuItem`
  - `ContextMenu` ? `ContextMenuStrip`
  - `ToolBar` ? `ToolStrip`
  - `DataGrid` ? `DataGridView`
- [ ] (2) Verify `EsfControl` builds and designers compile

### [ ] TASK-006: Build and validation
- [ ] (1) Restore and build full solution (0 errors)
- [ ] (2) Discover and run tests (if any)
- [ ] (3) Record build/test results in execution log

### [ ] TASK-007: Commit upgrade changes
- [ ] (1) Commit all changes to `upgrade-to-NET10` with message `TASK-007: Upgrade solution to .NET 10`
- [ ] (2) Verify `git status` is clean

### [ ] TASK-008: Update execution log
- [ ] (1) Ensure `.github/upgrades/execution_log.md` summarizes completed tasks, validations, and commit hash
