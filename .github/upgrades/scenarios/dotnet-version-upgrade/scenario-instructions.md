# .NET Version Upgrade

## Strategy
**Selected**: All-At-Once
**Rationale**: 19 SDK-style projects (netstandard2.0/2.1) with an essentially flat dependency graph (single edge CDMyMqttSender → GnatMQ.Std); no .NET Framework projects; user requested a single retarget → package-update → fix-build pass.

### Execution Constraints
- Single atomic upgrade: all project files retargeted to net10.0 together, then all packages updated together — no tier ordering, no multi-targeting.
- Operation sequence: update project files → update package references → restore → build solution and fix all compilation errors in one bounded pass → verify 0 errors / 0 warnings.
- Final validation runs the full solution build (and any tests) after the atomic upgrade completes.
- Deferred recommendations (e.g., CPM adoption) are documented in the final validation task, not executed.

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0
- **Solution**: src/cdePlugins.sln
- **Package Updates**: Update all NuGet package references to latest stable versions (user request)

## Upgrade Options

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Package Management: Per-Project (defer CPM to post-migration)

### Compatibility
- Unsupported API Handling: Fix Inline
- Windows Native APIs: No Compatibility Pack

### Modernization
- Nullable Reference Types: Leave Disabled

## Source Control
- **Source Branch**: CM7.111.0
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Auto (Merge)

## Decisions
- Commit strategy changed from After Each Task to Single Commit at End — recommended for All-at-Once strategy (one atomic upgrade, one commit).
- **REVISED (task 01)**: `C-DEngine` floating `6.*-*` resolves to 6.112.2 (netstandard2.1) which lacks `TheDeviceTagMapping`/`TheDeviceDescription` used by CDMyModbus → baseline build on CM7.111.0 already FAILS. C-DEngine **7.110.1** (nuget.org, `lib/net10.0` only) contains these types. Decision: move `C-DEngine` to `7.*` (resolves 7.110.1) as part of task 02 — consistent with branch CM7.111.0 and user's "latest stable" request. cde*Shared packages latest = 6.104.1 (verify net10.0 content/compat in task 02).
- Baseline build: 6 CS0246 errors (CDMyModbus), 141 warnings (85 CS0436 type-conflict, 56 CS0618 System.Data.SqlClient obsolete). Post-build `signandpackage.cmd` → `signMeSha.bat` fails locally with `AzureSignTool error -1610612734` because `BuildTools/real.sign` exists locally (gitignored) but no signing secrets are configured → MSB3077 on every plugin project. This is an environment issue, NOT an upgrade regression. `/p:PostBuildEvent=` does NOT bypass it (custom target). Workaround in effect: `BuildTools/real.sign` moved to `%TEMP%\cdePlugins.real.sign.bak` — **restore it in task 04** (`Move-Item $env:TEMP\cdePlugins.real.sign.bak P:\github\cdePlugins\BuildTools\real.sign`).
cde*Shared kept at `6.*-*` (6.104.1 is latest; depends on C-DEngine >= 6.104.0).
- **Task 03 revisions**: OPC UA 2.0.1 rejected (removed `Opc.Ua.Client.Controls`, `DataDictionary`, etc.) → pinned **1.5.378.182** (ships net10.0; sync APIs obsolete → migrated to async bridged synchronously; telemetry via `ITelemetryContext`). GitVersion.MsBuild reverted **6.8.2 → 5.12.0** (6.x rejects existing `GitVersion.yml` format). `BuildTools/cdePackager/C-DEngine.dll` replaced with 7.110.1; `cdePackager.runtimeconfig.json` got `rollForward: LatestMajor`.
- **Task 04**: `BuildTools/real.sign` restored from `%TEMP%` after the final clean build (clean build must run with it moved aside locally since no signing secrets are configured).

## Build Tool Decisions
- All 18 csproj: `dotnet build` (SDK-style, no resx images/WPF/COM). SDKs installed: 10.0.403, 10.0.100-rc.1, 11.0.100-rc.3; no global.json. `Directory.Build.props` (repo root) sets `BaseOutputPath=..\bin\$(Configuration)\$(MSBuildProjectName)\` and defines `CDE_STANDARD` only for netstandard2.0 — no TargetFramework defined there.

## User Preferences
### Execution Style
- Preferred shell: PowerShell (Windows, Visual Studio Enterprise 2026)
