# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade all projects in `src/cdePlugins.sln` from .NET Standard 2.0/2.1 to .NET 10 (net10.0), update all NuGet packages to latest stable, and resolve breaking changes.
**Scope**: 19 projects (18 SDK-style class libraries + 1 shared project), 95 distinct package references, 387 assessment findings concentrated in 4 projects (CDMyMSSQLStorage, C-DMyComputer, CDMyModbus, CDMyWebRelay).

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 19 projects, all on .NET Standard 2.0/2.1, flat dependency structure (single project reference CDMyMqttSender → GnatMQ.Std), no .NET Framework projects.

**Projects (single group)**
- Class libraries (C-DEngine plugins): C-DMyComputer, C-DMyVThings, CDMyOPCUAClient, C-DMyMessages, C-DMyNetwork, C-DMyVisitorLog, CDMyModbus, CDMyRulesEngine, CDMyMSSQLStorage, CDMyWebRelay, CDMyImages, CDMyThingProvisioning, CDMyLogger, CDMyMeshSender, CDMyMeshReceiver, CDMyMqttSender, CDMyPrometheusExporter
- Supporting library: GnatMQ.Std (netstandard2.0, referenced by CDMyMqttSender)
- Shared project: NModbus4Shared.shproj (consumed by CDMyModbus)

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | Flat graph of 19 SDK-style projects; user requested a single retarget → package-update → fix pass |
| Package Management | Per-Project (defer CPM to post-migration) | Floating `6.*-*` C-DEngine versions and divergent `System.*` polyfills being removed make mid-upgrade CPM churn-heavy |
| Unsupported API Handling | Fix Inline | 293 source-incompatible hits are concentrated in a few projects with known direct replacements |
| Windows Native APIs | No Compatibility Pack | WMI / PerformanceCounter / Registry usage is already covered by directly referenced packages with net10.0 builds |
| Nullable Reference Types | Leave Disabled | 19 projects with no existing annotations; enabling now would flood the upgrade with unrelated warnings |

## Tasks

### 01-verify-toolchain: Verify .NET 10 SDK and build toolchain prerequisites

Confirm the .NET 10 SDK is installed and usable from Visual Studio 2026 / `dotnet` CLI, check for any `global.json` that would pin an older SDK, and verify the solution restores and builds cleanly on its current netstandard targets so there is a known-good baseline before any changes. Also inventory the `BuildTools/signandpackage.cmd` post-build step used by every plugin project — it runs on every build and must keep working after the retarget (output paths change from `netstandard2.1` to `net10.0`).

Known risks: `GitVersion.MsBuild 5.6.6` is referenced by some projects and may need a newer version to work with the .NET 10 SDK; the legacy non-SDK projects `GnatMQ.WinRT`, `GnatMQ.NetCf39`, `GnatMQ.Net` (Net45) and `CDMyOPCUAClient.Tests` exist on disk — confirm whether they are part of the solution and therefore in scope.

**Done when**: .NET 10 SDK validated; no blocking `global.json`; baseline build result recorded; list of in-solution projects confirmed against assessment.

---

### 02-retarget-and-update-packages: Retarget all projects to net10.0 and update all packages to latest stable

Change `TargetFramework`/`TargetFrameworks` in all 18 SDK-style project files from `netstandard2.0`/`netstandard2.1` to `net10.0`, and adjust the `NModbus4Shared` shared project items as needed so `CDMyModbus` still compiles its shared sources. Preserve project-specific properties (`LangVersion`, `DefineConstants`, `ProjectName`, `GeneratePackageOnBuild`, `PackageOutputPath`) and the post-build sign-and-package target; update any paths that embed the old TFM string.

Then update every NuGet `PackageReference` across all projects to its latest stable version (per user request). Per the assessment: `Newtonsoft.Json` → 13.0.4, `System.Management` / `System.IO.Ports` / `System.Diagnostics.PerformanceCounter` → 10.0.x, plus Discord.Net 3.x, prometheus-net, Microsoft.Extensions.* 3.1.x, OPCFoundation.NetStandard.Opc.Ua.* (1.5.374.158 is flagged deprecated — research the current replacement package line), System.Data.SqlClient (CDMyMSSQLStorage — evaluate Microsoft.Data.SqlClient as the supported successor), System.Linq.Async/System.Interactive.Async, Microsoft.Build.* and GitVersion.MsBuild. Remove the mandatory-flagged `NETStandard.Library` reference from `GnatMQ.Std` and drop legacy `System.*` 4.x polyfill packages (System.Memory, System.Buffers, System.Runtime.*, System.Collections, System.Text.Encoding, System.ValueTuple, Microsoft.CSharp, Microsoft.NETCore.Platforms/Targets, etc.) that are part of the net10.0 framework. Keep the floating `6.*-*` versions on C-DEngine/cde*Shared packages. Use `get_supported_package_version` to resolve each latest stable version against net10.0.

**Done when**: All in-scope project files target `net10.0`; no project references `netstandard*`; every non-floating PackageReference is at its latest stable net10.0-compatible version; redundant framework-included packages removed; `dotnet restore` of the solution succeeds with no NU1xxx errors.

---

### 03-fix-breaking-changes: Build the solution and fix all compilation errors and warnings

Build the whole solution on net10.0 and resolve every compiler error and warning in a single bounded pass. The assessment flags 293 source-incompatible (Api.0002) and 85 behavioral (Api.0003) API hits: `CDMyMSSQLStorage` (127 — `System.Data.SqlClient`/ADO.NET surface), `C-DMyComputer` (91 — WMI via `System.Management`, `PerformanceCounter`), `CDMyModbus` (73 — `System.IO.Ports.SerialPort` and NModbus4 shared sources), `CDMyWebRelay` (47 — HTTP/networking APIs), `CDMyLogger`, `CDMyOPCUAClient`, `GnatMQ.Std`, `CDMyMqttSender`, `C-DMyMessages`, `C-DMyVisitorLog`, `CDMyPrometheusExporter`, `CDMyMeshReceiver`. Expect platform-compatibility analyzer warnings (CA1416) for Windows-only APIs — handle with proper `OperatingSystem.IsWindows()` guards or `SupportedOSPlatform` attributes rather than suppression. Expect obsolete-API warnings (SYSLIB*) from newer BCL and package versions (e.g., Opc.Ua, Discord.Net, Microsoft.Extensions.*) and apply the recommended replacements inline (Fix Inline option).

Research starting points: query the assessment per project for exact file/line hits; check `LangVersion=preview` projects for features that behave differently on C# 14; review `GnatMQ.Std` for `Thread`/`Socket` API behavioral changes; review `C-DMyComputer` WMI queries for `ManagementObjectSearcher` usage.

**Done when**: `src/cdePlugins.sln` builds with 0 errors and 0 warnings on net10.0; no `#pragma warning disable` / `NoWarn` suppressions added without user approval; all projects' post-build packaging step still succeeds.

---

### 04-final-validation: Full solution validation and deferred recommendations

Perform a clean full rebuild of the solution and run any discoverable test projects. Confirm every project's output is produced under `net10.0` and the plugin packaging (`signandpackage.cmd`, `GeneratePackageOnBuild`) still emits artifacts. Document deferred recommendations: Central Package Management can now be added cleanly since all projects are SDK-style and on a single TFM (`net10.0`) without `VersionOverride` friction; note any packages intentionally left at non-latest versions and why. Create the single end-of-upgrade commit on `upgrade-dotnet-10` per the commit strategy.

**Done when**: Clean rebuild of the solution succeeds with 0 errors / 0 warnings; tests (if any) pass; deferred recommendations recorded; all changes committed in one commit on the working branch.
