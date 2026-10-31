# 02-retarget-and-update-packages: Retarget all projects to net10.0 and update all packages to latest stable

Change `TargetFramework`/`TargetFrameworks` in all 18 SDK-style project files from `netstandard2.0`/`netstandard2.1` to `net10.0`, and adjust the `NModbus4Shared` shared project items as needed so `CDMyModbus` still compiles its shared sources. Preserve project-specific properties (`LangVersion`, `DefineConstants`, `ProjectName`, `GeneratePackageOnBuild`, `PackageOutputPath`) and the post-build sign-and-package target; update any paths that embed the old TFM string.

Then update every NuGet `PackageReference` across all projects to its latest stable version (per user request). Per the assessment: `Newtonsoft.Json` → 13.0.4, `System.Management` / `System.IO.Ports` / `System.Diagnostics.PerformanceCounter` → 10.0.x, plus Discord.Net 3.x, prometheus-net, Microsoft.Extensions.* 3.1.x, OPCFoundation.NetStandard.Opc.Ua.* (1.5.374.158 is flagged deprecated — research the current replacement package line), System.Data.SqlClient (CDMyMSSQLStorage — evaluate Microsoft.Data.SqlClient as the supported successor), System.Linq.Async/System.Interactive.Async, Microsoft.Build.* and GitVersion.MsBuild. Remove the mandatory-flagged `NETStandard.Library` reference from `GnatMQ.Std` and drop legacy `System.*` 4.x polyfill packages (System.Memory, System.Buffers, System.Runtime.*, System.Collections, System.Text.Encoding, System.ValueTuple, Microsoft.CSharp, Microsoft.NETCore.Platforms/Targets, etc.) that are part of the net10.0 framework. Keep the floating `6.*-*` versions on C-DEngine/cde*Shared packages. Use `get_supported_package_version` to resolve each latest stable version against net10.0.

**Done when**: All in-scope project files target `net10.0`; no project references `netstandard*`; every non-floating PackageReference is at its latest stable net10.0-compatible version; redundant framework-included packages removed; `dotnet restore` of the solution succeeds with no NU1xxx errors.

## Research Findings (confirmed against repo)

### TFM edits (18 csproj; no TFM in Directory.Build.props; no CPM — `Directory.Packages.props` absent)
- 16 projects: `<TargetFramework>netstandard2.1</TargetFramework>` → `net10.0` (keep singular).
- `C-DMyComputer.csproj`: `<TargetFrameworks>netstandard2.1</TargetFrameworks>` (plural, single value) → `<TargetFrameworks>net10.0</TargetFrameworks>` (keep plural — build output path `bin\...\net10.0\` is relied on by `signandpackage.cmd`/packager; all projects produce TFM folders anyway).
- `GnatMQ.Std.csproj`: `<TargetFrameworks>netstandard2.0</TargetFrameworks>` → `net10.0`. No PackageReferences at all (NETStandard.Library is implicit for netstandard2.0 and disappears with the retarget — satisfies the assessment's mandatory finding). `CDE_STANDARD` define from Directory.Build.props (netstandard2.0 only) will no longer apply → check `#if CDE_STANDARD` usage in GnatMQ in task 03.
- `NModbus4Shared.projitems`: plain `Compile Include` items, TFM-agnostic → no change needed.
- No csproj embeds the literal `netstandard2.1` string in paths (only `$(TargetFramework)` macros in ProjectName/Product/Description — auto-update).

### Package inventory (observed) → target
| Package | Current | Target | Projects |
|---|---|---|---|
| C-DEngine | `6.*-*` (→6.112.2, netstandard2.1, lacks Modbus types) | `7.*` (→7.110.1, net10.0-only) | all 17 plugin projects |
| cde*Shared (11 pkgs) | `6.*-*` (→6.104.1 = latest) | keep `6.*-*` | various |
| System.Management | 9.0.0 (nested `<Version>`) | 10.0.14 | C-DMyComputer |
| System.Diagnostics.PerformanceCounter | 9.0.0 (nested) | 10.0.14 | C-DMyComputer |
| System.IO.Ports | 10.0.1 | 10.0.14 | CDMyModbus |
| Newtonsoft.Json | 13.0.3 | 13.0.4 | CDMyMqttSender |
| Discord.Net.Webhook | 3.18.0 (nested) | 3.20.2 | CDMyLogger |
| prometheus-net | 8.2.1 | 8.2.1 (already latest) | CDMyPrometheusExporter |
| GitVersion.MsBuild (`Update`) | 6.5.0 | 6.8.2 | 8 projects |
| OPCFoundation.NetStandard.Opc.Ua.Client/Configuration/Core | 1.5.374.158 (deprecated) | 2.0.1 (lib net10.0/net9.0/net8.0/net48) | CDMyOPCUAClient |
| System.Data.SqlClient | 4.9.0 (obsolete → 56 CS0618) | **replace with Microsoft.Data.SqlClient 7.1.1** (lib net8.0/net9.0/netstandard2.0; namespace swap in task 03) | CDMyMSSQLStorage |
- No legacy System.* 4.x polyfills / NETStandard.Library / Microsoft.CSharp present in any csproj (assessment items referred to transitive deps of old packages) → nothing to remove.
- cde*Shared 6.104.1 declare dependency `C-DEngine >= 6.104.0` → compatible with 7.110.1. cdeEnergyContractsShared/cdeSensorTemplateShared are contentFiles-only (`cs/netstandard2.1`) → NuGet selects nearest compatible TFM for net10.0 → OK. Others ship `lib/netstandard2.1` + `contentFiles/any/any`.
- CS0436 in C-DMyVThings: its cde*Shared refs lack `IncludeAssets="ContentFiles"` so both the lib dll and the source are compiled → fix in task 03 by adding `IncludeAssets="ContentFiles"` (same pattern as all other projects).

### Build/validation
- `dotnet restore src\cdePlugins.sln` then `dotnet build` (signing disabled via renamed `BuildTools/real.sign`). Expect compile errors (OPC UA 2.0 API, SqlClient namespace, netstandard-only APIs) → task 03.
