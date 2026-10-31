# Task 03 — Fix Breaking Changes: Progress Details

## Outcome
- `dotnet build src\cdePlugins.sln --no-incremental` → **0 errors / 0 warnings** (all 18 projects, net10.0).
- Post-build `cdePackager` ran for all 17 plugin projects and produced `.CDEX` packages.
- No `#pragma warning disable`, `<NoWarn>`, or `/nowarn` added anywhere.

## Build progression
| Build | Errors | Warnings | Notes |
|---|---|---|---|
| Baseline (CM7.111.0) | 6 | 141 | pre-existing CDMyModbus CS0246 + System.Data.SqlClient obsoletes |
| After retarget + packages | 52 | 149 | C-DEngine 7, OPC UA, SqlClient, GitVersion, packager failures |
| Mid-task | 7 | 54 | all remaining in CDMyOPCUAClient |
| Final | 0 | 0 | |

## Toolchain / build infrastructure fixes
- **GitVersion.MsBuild**: reverted to **5.12.0** (6.8.2 rejects the existing `GitVersion.yml` format `{env:cdeBuildDate ?? private}`; upgrading GitVersion config is out of scope).
- **BuildTools/cdePackager/C-DEngine.dll**: replaced with C-DEngine **7.110.1** so the packager can load plugins compiled against C-DEngine 7 (was 6.x → `FileLoadException`).
- **BuildTools/cdePackager/cdePackager.runtimeconfig.json**: added `"rollForward": "LatestMajor"` so the net8.0 packager runs on the .NET 10 runtime and can reflect over net10.0 plugin assemblies.

## Source changes by project
### C-DEngine 7.110.1 migration (all plugins)
- Replaced removed/renamed cache and registry APIs; fixed `TheDeviceTagMapping`/`TheDeviceDescription` usages in CDMyModbus (pre-existing baseline failure, now resolved by C-DEngine 7).
- Files: `cmyCom_Health.cs`, `TheISOlaterKPIs.cs`, `cmyComputerCommon.cs`, `cmyComputerService.cs`, `ModbusTCPDevice.cs`, `TheRSSLogger.cs`, `cde_WebRelayService.cs`, `TheMqttSender.cs`, `MqttNetworkChannel.cs`.

### CDMyMSSQLStorage — System.Data.SqlClient → Microsoft.Data.SqlClient 7.1.1
- Namespace swap in `TheStorageCommon.cs`, `TheStorageService.cs`, `Utils/TheSQLhelpers.cs`; adjusted connection-string/encryption defaults where behavior changed.

### CDMyOPCUAClient — OPC UA 1.5.374.158 → 1.5.378.182
- 2.0.1 was evaluated and rejected (removes `Opc.Ua.Client.Controls`, `DataDictionary`, and other types used throughout the plugin). 1.5.378.182 ships net10.0 and keeps the type surface, but obsoletes all synchronous service calls.
- All obsolete sync calls replaced with async equivalents bridged at the existing synchronous boundaries (`.GetAwaiter().GetResult()` with `CancellationToken.None`):
  - `Browse/BrowseNext/Read/Write/Call/TranslateBrowsePathsToNodeIds` → `*Async` + response objects (`ClientUtils.cs`, `cdeOPCdata.cs`, `cdeOPCMethod.cs`, `TheOPCUARemoteServer.cs`).
  - `Subscription.ApplyChanges/Create/Delete/ConditionRefresh` → `*Async` (`cdeOPCBase.cs`, `cdeOPCEvent.cs`, `TheOPCUARemoteServer*.cs`).
  - `Session.Create/Close/RemoveSubscription`, `NodeCache.Find/GetDisplayText`, `DiscoveryClient.Create/FindServers/GetEndpoints`, `ApplicationInstance.CheckApplicationInstanceCertificate` → async variants.
  - `UserIdentity(string,string)` → UTF-8 byte password overload.
  - Removed dead `DataDictionary`/`FindDataDictionary` usage (type removed; remaining consumer is under `#if OLD_UA`).
- **Telemetry**: the stack now logs through `ITelemetryContext` instead of the obsolete `Utils.SetTraceOutput/SetTraceLog/Utils.Tracing`. `cdeOPCUaClient` creates one `OpcTelemetry` via `DefaultTelemetry.Create(...)` with a custom `ILoggerProvider` that preserves the plugin's `EnableTracing` (file `opcclient.log`), `EnableTracingToLog` (C-DEngine SYSLOG) and `OPCTraceMask` filter semantics. `TheOPCUARemoteServer` forwards it to `CertificateValidator`, `ApplicationInstance`, `DefaultSessionFactory`, `SessionReconnectHandler` and `DiscoveryClient`.

### Package references (per-project)
- Other package bumps from task 02 compiled without source changes.

## Tests
- Only test project in the repo: `src/078 - CDMyOPCUAClient/CDMyOPCUAClient.Tests/CDMyOPCUAClient.Tests.csproj` — legacy non-SDK .NET 4.5 project, **not in the solution**, referencing projects that don't exist in this repo (`025 - C-DEngine`, `_OPCUA/*`, `CDMyOPCUAServer`, `CDMyFileService`). It cannot be built on any branch; left untouched.

## Deferred to task 04 / post-migration
- Restore `BuildTools/real.sign` from `%TEMP%\cdePlugins.real.sign.bak` (signing marker moved aside so the unsigned local build succeeds).
- GitVersion 6 upgrade (requires `GitVersion.yml` rewrite).
- Central Package Management adoption.
- Runtime validation of the OPC UA async bridging under load (behavior is unchanged in intent, but sync-over-async on the stack's new async paths should be smoke-tested against a live server).

## Housekeeping
- Deleted stray `src/078 - CDMyOPCUAClient/Nugets/` created by an earlier per-project build invoked without `SolutionDir` (normal output goes to the gitignored `src/Nugets`).
