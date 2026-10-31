# 03-fix-breaking-changes: Build the solution and fix all compilation errors and warnings

Build the whole solution on net10.0 and resolve every compiler error and warning in a single bounded pass. The assessment flags 293 source-incompatible (Api.0002) and 85 behavioral (Api.0003) API hits: `CDMyMSSQLStorage` (127 — `System.Data.SqlClient`/ADO.NET surface), `C-DMyComputer` (91 — WMI via `System.Management`, `PerformanceCounter`), `CDMyModbus` (73 — `System.IO.Ports.SerialPort` and NModbus4 shared sources), `CDMyWebRelay` (47 — HTTP/networking APIs), `CDMyLogger`, `CDMyOPCUAClient`, `GnatMQ.Std`, `CDMyMqttSender`, `C-DMyMessages`, `C-DMyVisitorLog`, `CDMyPrometheusExporter`, `CDMyMeshReceiver`. Expect platform-compatibility analyzer warnings (CA1416) for Windows-only APIs — handle with proper `OperatingSystem.IsWindows()` guards or `SupportedOSPlatform` attributes rather than suppression. Expect obsolete-API warnings (SYSLIB*) from newer BCL and package versions (e.g., Opc.Ua, Discord.Net, Microsoft.Extensions.*) and apply the recommended replacements inline (Fix Inline option).

Research starting points: query the assessment per project for exact file/line hits; check `LangVersion=preview` projects for features that behave differently on C# 14; review `GnatMQ.Std` for `Thread`/`Socket` API behavioral changes; review `C-DMyComputer` WMI queries for `ManagementObjectSearcher` usage.

**Done when**: `src/cdePlugins.sln` builds with 0 errors and 0 warnings on net10.0; no `#pragma warning disable` / `NoWarn` suppressions added without user approval; all projects' post-build packaging step still succeeds.

## Research (actual build findings on net10.0)

### Build-infrastructure blockers (fixed first)
- **GitVersion.MsBuild 6.8.2** fails on every project: `Unable to format AssemblyInformationalVersion ... 'private' is not a property` — `GitVersion.yml` uses the v5 `{env:cdeBuildDate ?? private}` syntax. Decision: stay on latest **5.x = 5.12.0** (Directory.Build.targets + 7 csproj `Update` items). Config migration to v6 is deferred (documented in task 04).
- **cdePackager** (`BuildTools/cdePackager/cdePackager.exe`, net8.0 runtimeconfig) reflects over plugin DLLs: (a) `System.Runtime 10.0.0.0` not found → add `"rollForward": "LatestMajor"` to runtimeconfig; (b) bundled `C-DEngine.dll` 6.122.0 ≠ referenced 7.110.1 → replace bundled DLL with 7.110.1 (tracked file; old copy at `%TEMP%\cdePlugins.C-DEngine.6.122.0.dll.bak`).

### Compiler errors found (build #2: 52 errors / 149 warnings)
| Project | Issue | Fix |
|---|---|---|
| CDMyMSSQLStorage | CS1069 `System.Data.SqlClient` types | `using Microsoft.Data.SqlClient;` + manifest file name |
| CDMyMSSQLStorage | `TheMirrorCache<T>.RemoveAnItemByKey/AddOrUpdateItemKey` removed in C-DEngine 7 | key-based API lives on public `TheMirrorCacheCore<T>` → change `MyBlobCache` type |
| CDMyMSSQLStorage | CS0618 `string.Copy` | plain assignment |
| CDMyModbus | CS0234 stray `using Microsoft.Build.Utilities` | removed |
| CDMyWebRelay | `TheCommonUtils.IsMono()` removed in C-DEngine 7 | collapse to the non-Mono branch |
| C-DMyComputer | `TheCDEngines.eventAllEnginesStarted` removed (retired V4) | `TheBaseEngine.WaitForEnginesStarted((t,o)=>…)` (pattern already used by OPC plugin) |
| C-DMyComputer | 56× CA1416 WMI/PerformanceCounter | `[SupportedOSPlatform("windows")]` on `TheCDMyComputerEngine`, `TheHealthMonitor`, `ThePerfCounter`, `ProcessCpuCounter`, `TheISOlaterKPIs` (plugin is Windows-only by design) |
| C-DMyVThings | 85× CS0436 contentFiles vs compiled cde*Shared | add `IncludeAssets="ContentFiles"` like all other plugins |
| CDMyLogger / CDMyWebRelay | CS8073 struct `!= null` | removed dead null checks |
| GnatMQ.Std | CS0618/SYSLIB0039 Ssl3/Tls/Tls11 | map to `SslProtocols.None` (OS default) |
| CDMyMqttSender | SYSLIB0057 `new X509Certificate(byte[])` | `X509CertificateLoader.LoadCertificate` |
| CDMyOPCUAClient | 22× CS0246 — OPC UA **2.0.1** removed `*Collection` types, `CertificateValidator` events etc. (major rewrite, 93 usages) | **Revised package decision**: use **1.5.378.182** (latest non-deprecated 1.5.x, ships net10.0) instead of 2.0.1 |

### CDMyOPCUAClient on 1.5.378.182 (build #5: 7 errors / 54 CS0618)
Stack deprecated all sync APIs in favour of `*Async`. Remaining work, all in this project:
- `CertificateValidator.Validate(X509Certificate2Collection)` no longer virtual → override `protected virtual Task InternalValidateAsync(certs, endpoint, ct)` (done).
- Errors: `DataDictionary`/`ISession.FindDataDictionary` removed; `Call(...)` needs `out diagnosticInfos`; `ApplicationInstance.CheckApplicationInstanceCertificate` → `CheckApplicationInstanceCertificatesAsync`; `Subscription.ApplyChanges` → `ApplyChangesAsync`.
- Warnings: `SubscriptionObsolete.ApplyChanges/Create/Delete/ConditionRefresh`, `SessionObsolete.Close/RemoveSubscription/FindDataDescription`, `DiscoveryClient.Create/FindServers/GetEndpoints`, `Session.Create`, `SessionClient.Browse/BrowseNext/Read/Write/TranslateBrowsePathsToNodeIds`, `NodeCacheObsolete.Find/GetDisplayText`, `SessionReconnectHandler` ctor, `Utils.SetTraceOutput/SetTraceLog/Tracing`, `ApplicationConfiguration.Validate`, `Attributes.GetIdentifiers`, `ApplicationInstance` ctor.
- Approach: replace each with the Async variant; where the caller is synchronous, bridge with `TheCommonUtils.cdeRunTaskAsync`-style patterns are not needed — use `.GetAwaiter().GetResult()` only at existing sync boundaries to keep behaviour identical (inline fix, no suppressions).
