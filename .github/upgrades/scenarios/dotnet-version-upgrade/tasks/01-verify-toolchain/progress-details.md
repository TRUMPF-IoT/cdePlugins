# 01-verify-toolchain — Progress Details

## What was done
- Validated .NET 10 SDK (10.0.403 installed; also 10.0.100-rc.1 and 11.0.100-rc.3). No `global.json` in repo.
- Located shared MSBuild config: `P:\github\cdePlugins\Directory.Build.props` (output path + `CDE_STANDARD` define for netstandard2.0 only) and `NuGet.config` (nuget.org only).
- Confirmed solution membership: 18 csproj + NModbus4Shared.shproj (19 total, matches assessment). Legacy GnatMQ.WinRT/NetCf39/Net and CDMyOPCUAClient.Tests are not in the solution.
- Inventoried post-build chain: `SignAndPackage.cmd` → `signMeSha.bat` (AzureSignTool) → `cdePackager.exe`.
- Ran baseline `dotnet build src\cdePlugins.sln`.

## Baseline build result
- With local signing active: MSB3077 on every plugin project (AzureSignTool -1610612734 — local `BuildTools/real.sign` present but no signing secrets). Environment issue, not code.
- With signing disabled (renamed gitignored `BuildTools/real.sign` → `real.sign.disabled-for-upgrade`): **Build FAILED — 6 CS0246 errors in CDMyModbus** (`TheDeviceTagMapping`, `TheDeviceDescription` not in C-DEngine 6.112.2; present in C-DEngine 7.110.1 which ships `lib/net10.0` only).
- Warnings: 85 CS0436 (C-DMyVThings, contentFiles vs. compiled type conflicts from cde*Shared packages), 56 CS0618 (CDMyMSSQLStorage, System.Data.SqlClient obsolete).

## Decisions recorded (scenario-instructions.md)
- Move `C-DEngine` to `7.*` in task 02 (baseline is already broken on 6.x; 7.110.1 is net10.0-only and is the latest stable).
- Build tool: `dotnet build` for all projects.
- Keep `real.sign` renamed during the upgrade; restore before finishing (not a tracked file).

## Files modified
- `.github/upgrades/scenarios/dotnet-version-upgrade/scenario-instructions.md` (decisions + build tool decisions)
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/01-verify-toolchain/task.md` (research)
- No source/project files changed. `BuildTools/real.sign` renamed locally (gitignored).
