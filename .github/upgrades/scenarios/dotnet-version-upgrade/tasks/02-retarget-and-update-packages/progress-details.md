# 02-retarget-and-update-packages — Progress Details

## Target framework changes (18 csproj)
- 16 projects: `<TargetFramework>netstandard2.1</TargetFramework>` → `net10.0`
- `C-DMyComputer.csproj`: `<TargetFrameworks>netstandard2.1</TargetFrameworks>` → `<TargetFrameworks>net10.0</TargetFrameworks>` (kept plural as before)
- `GnatMQ.Std.csproj`: `<TargetFrameworks>netstandard2.0</TargetFrameworks>` → `net10.0` (implicit NETStandard.Library dropped automatically — mandatory assessment finding resolved)
- `NModbus4Shared.projitems`: no change required (TFM-agnostic Compile items)
- No csproj contained hard-coded `netstandard*` paths; `$(TargetFramework)` macros update automatically.

## Package updates
| Package | Before | After | Where |
|---|---|---|---|
| C-DEngine | `6.*-*` | `7.*` (→ 7.110.1, net10.0) | 17 csproj; `Directory.Build.targets` prerelease override → `7.*-*` |
| GitVersion.MsBuild | 6.5.0 (`Update`) / 5.6.6 (`Include` in Directory.Build.targets) | 6.8.2 | 8 csproj + Directory.Build.targets |
| Microsoft.SourceLink.GitHub | 1.0.0 | 10.0.403 | Directory.Build.props |
| System.Management | 9.0.0 | 10.0.14 | C-DMyComputer |
| System.Diagnostics.PerformanceCounter | 9.0.0 | 10.0.14 | C-DMyComputer |
| System.IO.Ports | 10.0.1 | 10.0.14 | CDMyModbus |
| Newtonsoft.Json | 13.0.3 | 13.0.4 | CDMyMqttSender |
| Discord.Net.Webhook | 3.18.0 | 3.20.2 | CDMyLogger |
| OPCFoundation.NetStandard.Opc.Ua.Client/Configuration/Core | 1.5.374.158 (deprecated) | 2.0.1 | CDMyOPCUAClient |
| System.Data.SqlClient | 4.9.0 | **Microsoft.Data.SqlClient 7.1.1** (+ explicit System.Security.Cryptography.Pkcs 10.0.14) | CDMyMSSQLStorage |
| prometheus-net | 8.2.1 | unchanged (latest) | CDMyPrometheusExporter |
| cde*Shared (11) | `6.*-*` | unchanged (6.104.1 latest) | various |

## Validation
- `dotnet restore src\cdePlugins.sln --force` → **0 errors, 0 warnings** (NU1904 System.Drawing.Common and NU1902/NU1903 Pkcs resolved by SourceLink bump and explicit Pkcs reference).
- C-DEngine resolves to 7.110.1 in all plugin projects.
- Build not yet run — compile fixes are task 03's scope (expected: Microsoft.Data.SqlClient namespace, OPC UA 2.0 API changes, CDE_STANDARD define no longer set for GnatMQ, CS0436 in C-DMyVThings).

## Environment note
- `BuildTools/real.sign` (gitignored local marker) moved to `%TEMP%\cdePlugins.real.sign.bak` so builds don't fail on local code signing; restore in task 04.

## Files modified
- Directory.Build.props, Directory.Build.targets
- All 18 in-solution .csproj files (see `git status`)
