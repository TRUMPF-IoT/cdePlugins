# Task 04 — Final Validation: Progress Details

## Clean rebuild
- Deleted `bin/Debug` and every `obj/` under `src/`, then ran `dotnet build src\cdePlugins.sln -nologo` (implicit restore).
- Result: **0 errors / 0 warnings**, 18 assemblies produced (all under `bin/Debug/<Project>/net10.0/`).
- Post-build packaging: 17 `.CDEX` plugin packages emitted by `cdePackager` (C-DEngine 7.110.1 DLL, `rollForward: LatestMajor`); 51 `.nupkg`/`.snupkg` via `GeneratePackageOnBuild` into the gitignored `src/Nugets`.
- `BuildTools/real.sign` (gitignored local marker) was moved aside during the build — AzureSignTool has no secrets on this machine — and **restored afterwards**. CI signing is unaffected.

## Tests
- Sole test project `src/078 - CDMyOPCUAClient/CDMyOPCUAClient.Tests` is a legacy .NET 4.5 non-SDK project outside the solution that references projects not present in this repo (`025 - C-DEngine`, `_OPCUA/*`, `CDMyOPCUAServer`, `CDMyFileService`). Not buildable before or after the upgrade; no tests were run.

## Package inventory (final)
| Package | Version | Note |
|---|---|---|
| C-DEngine | `7.*` / `7.*-*` → 7.110.1 | net10.0-only package; required for CDMyModbus types |
| cde*Shared (12 packages), cdePackager, cdePackagerTask, cdeLicenseTool, cdeASPNet, cdeIIS, CDMyNMIHtml5RT | `6.*-*` → 6.104.1 | latest available |
| Microsoft.Data.SqlClient | 7.1.1 | replaces System.Data.SqlClient |
| System.Security.Cryptography.Pkcs / System.IO.Ports / System.Management / System.Diagnostics.PerformanceCounter | 10.0.14 | |
| OPCFoundation.NetStandard.Opc.Ua.Client/Configuration/Core | **1.5.378.182** | **intentionally not 2.0.x** — 2.0 removes `Opc.Ua.Client.Controls`, `DataDictionary` and other types the plugin depends on |
| GitVersion.MsBuild | **5.12.0** | **intentionally not 6.x** — 6.x rejects the repo's `GitVersion.yml` (`{env:cdeBuildDate ?? private}`) |
| Microsoft.SourceLink.GitHub | 10.0.403 | |
| Newtonsoft.Json | 13.0.4 | |
| Discord.Net.Webhook | 3.20.2 | |
| prometheus-net | 8.2.1 | latest |
| WebSocket4Net | 0.15.2 | latest |
| IDisposableAnalyzers | 3.4.15 | latest |

Old-version references found under `src/178 - External Logger/DiscordLib/**` (Newtonsoft 13.0.1, System.Collections.Immutable 1.3.1, etc.) belong to a **gitignored vendored copy of Discord.Net** that is not part of the solution — out of scope.

## Deferred recommendations
1. **Central Package Management** — all 18 projects are SDK-style on a single TFM; a `Directory.Packages.props` can now be introduced without `VersionOverride` friction. Floating versions (`6.*-*`, `7.*`) would need to be pinned or kept as `VersionOverride`s.
2. **GitVersion 6** — rewrite `GitVersion.yml` to the v6 schema, then bump `GitVersion.MsBuild` in `Directory.Build.targets`.
3. **OPC UA 2.x** — requires replacing `Opc.Ua.Client.Controls`-derived helpers (`ClientUtils`) and the `DataDictionary`-based structured-type path; also an opportunity to make the plugin truly async instead of the current `GetAwaiter().GetResult()` bridging.
4. **cdePackager** — rebuild `BuildTools/cdePackager` for net10.0 instead of relying on `rollForward`.
5. **Runtime smoke test** of CDMyOPCUAClient against a live OPC UA server (sync-over-async bridging on the stack's new async paths).
6. **Test project** — either modernize `CDMyOPCUAClient.Tests` to an SDK-style net10.0 project with resolvable references or remove it.

## Commit
- Single commit on `upgrade-dotnet-10` containing all source/project/build-tool changes plus the `.github/upgrades/` scenario artifacts.
